#!/usr/bin/env node

import { spawn } from 'node:child_process';
import { appendFile, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const [browserExecutable, targetUrl, artifactDirectory] = process.argv.slice(2);
if (!browserExecutable || !targetUrl || !artifactDirectory) {
  console.error('Usage: run-headless-runtime-visual-e2e.mjs <browser> <url> <artifact-dir>');
  process.exit(2);
}

const timeoutMs = 120_000;
const commandTimeoutMs = 30_000;
const profileDirectory = await mkdtemp(join(tmpdir(), 'machiverseworks-runtime-visual-e2e-'));
const actualDirectory = join(artifactDirectory, 'actual');
const diagnosticsDirectory = join(artifactDirectory, 'diagnostics');
const browserLogPath = join(artifactDirectory, 'chrome.log');
let browser;
let devToolsSocket;

await mkdir(actualDirectory, { recursive: true });
await mkdir(diagnosticsDirectory, { recursive: true });

try {
  browser = spawn(browserExecutable, [
    '--headless=new',
    '--no-sandbox',
    '--disable-dev-shm-usage',
    '--disable-background-timer-throttling',
    '--disable-backgrounding-occluded-windows',
    '--disable-renderer-backgrounding',
    '--disable-lcd-text',
    '--font-render-hinting=none',
    '--force-color-profile=srgb',
    '--force-device-scale-factor=1',
    '--lang=en-US',
    '--hide-scrollbars',
    '--enable-unsafe-swiftshader',
    '--use-angle=swiftshader',
    '--window-size=1920,1080',
    '--remote-debugging-port=0',
    `--user-data-dir=${profileDirectory}`,
    targetUrl,
  ], { stdio: ['ignore', 'ignore', 'pipe'] });

  browser.stderr.on('data', (chunk) => { void appendFile(browserLogPath, chunk); });

  const remoteDebuggingPort = await waitForDevToolsPort(profileDirectory, browser, timeoutMs);
  const page = await waitForPage(remoteDebuggingPort, targetUrl, browser, timeoutMs);
  devToolsSocket = await createDevToolsClient(page.webSocketDebuggerUrl, commandTimeoutMs);
  await devToolsSocket.command('Page.enable');
  await devToolsSocket.command('Runtime.enable');

  const browserVersion = await devToolsSocket.command('Browser.getVersion');
  const expectedBrowserVersion = process.env.MVW_VISUAL_BROWSER_VERSION;
  if (expectedBrowserVersion && !browserVersion.product?.endsWith(`/${expectedBrowserVersion}`)) {
    throw new Error(`Runtime visual browser version mismatch: expected ${expectedBrowserVersion}, actual ${browserVersion.product ?? 'unknown'}.`);
  }

  const defaultDiagnostics = await waitForRuntimeReady(devToolsSocket, browser, timeoutMs);
  await captureCheckpoint(devToolsSocket, browser, browserVersion, expectedBrowserVersion, 'runtime-default', null);
  await captureCheckpoint(devToolsSocket, browser, browserVersion, expectedBrowserVersion, 'runtime-city-overview', 'city-overview');
  await captureCheckpoint(devToolsSocket, browser, browserVersion, expectedBrowserVersion, 'runtime-street-activity', 'street-activity');

  const summary = {
    status: 'passed',
    source: 'actual Application -> MachiVerseConnection -> Server/Simulation runtime',
    goldenComparisonEnabled: false,
    note: 'Runtime screenshots and structural diagnostics captured; the integrated Golden checker validates the required city contract next.',
    initialDiagnostics: defaultDiagnostics,
  };
  await writeFile(join(artifactDirectory, 'summary.json'), `${JSON.stringify(summary, null, 2)}\n`, 'utf8');

  console.log(
    `Runtime user-view capture passed: genericAgents=${String(defaultDiagnostics.genericAgentCount)}, `
      + `terrainSamples=${String(defaultDiagnostics.terrainSampleCount)}, settlements=${String(defaultDiagnostics.settlementCount)}, `
      + `buildings=${String(defaultDiagnostics.buildingCount)}, roads=${String(defaultDiagnostics.roadSegmentCount)}, `
      + `pedestrians=${String(defaultDiagnostics.pedestrianCount)}, vehicles=${String(defaultDiagnostics.vehicleCount)}, `
      + `trains=${String(defaultDiagnostics.trainCount)}, visibleDebugOverlays=${String(defaultDiagnostics.visibleDebugOverlayCount)}, `
      + `japaneseFontReady=${String(defaultDiagnostics.japaneseFontReady)}.`,
  );
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  console.error(normalized.message);
  process.exitCode = 1;
} finally {
  devToolsSocket?.close();
  if (browser && browser.exitCode === null && browser.signalCode === null) {
    browser.kill('SIGTERM');
    await Promise.race([new Promise((resolve) => browser.once('exit', resolve)), sleep(2_000)]);
    if (browser.exitCode === null && browser.signalCode === null) {
      browser.kill('SIGKILL');
      await Promise.race([new Promise((resolve) => browser.once('exit', resolve)), sleep(2_000)]);
    }
  }
  await rm(profileDirectory, { recursive: true, force: true, maxRetries: 10, retryDelay: 100 });
}

async function waitForRuntimeReady(client, browserProcess, timeout) {
  const deadline = Date.now() + timeout;
  let stableSignature = null;
  let stablePolls = 0;
  let latest = null;

  while (Date.now() < deadline) {
    ensureBrowserRunning(browserProcess);
    latest = await client.evaluate('window.__MACHIVERSE_RUNTIME_VISUAL_TEST__?.getDiagnostics?.() ?? null');
    if (latest?.ready === true) {
      const signature = [
        latest.terrainSampleCount,
        latest.settlementCount,
        latest.buildingCount,
        latest.roadSegmentCount,
        latest.pedestrianCount,
        latest.vehicleCount,
        latest.trainCount,
        latest.visibleDebugOverlayCount,
      ].join(':');
      if (signature === stableSignature) stablePolls += 1;
      else {
        stableSignature = signature;
        stablePolls = 1;
      }
      // Initial runtime snapshots can arrive in batches. Require five seconds of unchanged
      // city-layer counts so runtime-default represents a settled user-visible state.
      if (stablePolls >= 20) return latest;
    } else {
      stableSignature = null;
      stablePolls = 0;
    }
    await sleep(250);
  }

  throw new Error(`Timed out waiting for the actual runtime View to become stable. Last diagnostics: ${JSON.stringify(latest)}`);
}

async function captureCheckpoint(client, browserProcess, browserVersion, expectedBrowserVersion, inspectionName, checkpoint) {
  let previousRoadSnapshotSequence = null;
  if (checkpoint !== null) {
    const before = await client.evaluate('window.__MACHIVERSE_RUNTIME_VISUAL_TEST__?.getDiagnostics?.() ?? null');
    previousRoadSnapshotSequence = before?.roadSnapshotSequence ?? null;
    if (!Number.isInteger(previousRoadSnapshotSequence)) {
      throw new Error(`Runtime road snapshot sequence is unavailable before checkpoint: ${inspectionName}.`);
    }

    const positioned = await client.evaluate(`window.__MACHIVERSE_RUNTIME_VISUAL_TEST__?.setCheckpoint?.(${JSON.stringify(checkpoint)}) ?? false`);
    if (positioned !== true) throw new Error(`Failed to position actual runtime View checkpoint: ${checkpoint}.`);
    await waitForRoadSnapshotAfter(client, browserProcess, previousRoadSnapshotSequence, commandTimeoutMs, checkpoint);
  }

  await client.evaluate('new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => requestAnimationFrame(resolve))))');

  const diagnostics = await client.evaluate(`(() => ({
    runtime: window.__MACHIVERSE_RUNTIME_VISUAL_TEST__?.getDiagnostics?.() ?? null,
    devicePixelRatio: window.devicePixelRatio,
    viewport: { width: window.innerWidth, height: window.innerHeight },
    uiText: document.body.innerText,
    location: location.href,
  }))()`);
  if (diagnostics.runtime?.ready !== true) throw new Error(`Runtime diagnostics became unavailable at ${inspectionName}.`);
  diagnostics.browser = browserVersion;
  diagnostics.expectedBrowserVersion = expectedBrowserVersion ?? null;
  diagnostics.visualFont = {
    family: process.env.MVW_VISUAL_FONT_FAMILY ?? null,
    packageVersion: process.env.MVW_VISUAL_FONT_PACKAGE_VERSION ?? null,
  };
  await writeFile(join(diagnosticsDirectory, `${inspectionName}.json`), `${JSON.stringify(diagnostics, null, 2)}\n`, 'utf8');

  const screenshot = await client.command('Page.captureScreenshot', {
    format: 'png',
    fromSurface: true,
    captureBeyondViewport: false,
  });
  await writeFile(join(actualDirectory, `${inspectionName}.png`), Buffer.from(screenshot.data, 'base64'));
}

async function waitForRoadSnapshotAfter(client, browserProcess, previousSequence, timeout, checkpoint) {
  const deadline = Date.now() + timeout;
  let latest = null;
  while (Date.now() < deadline) {
    ensureBrowserRunning(browserProcess);
    latest = await client.evaluate('window.__MACHIVERSE_RUNTIME_VISUAL_TEST__?.getDiagnostics?.() ?? null');
    if (latest?.roadSnapshotSequence > previousSequence && latest.ready === true) return latest;
    await sleep(100);
  }
  throw new Error(
    `Timed out waiting for a Road snapshot from the ${checkpoint} subscription. `
      + `Previous sequence=${String(previousSequence)}, latest=${JSON.stringify(latest)}.`,
  );
}

async function waitForDevToolsPort(profileDirectoryValue, browserProcess, timeout) {
  const activePortPath = join(profileDirectoryValue, 'DevToolsActivePort');
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    ensureBrowserRunning(browserProcess);
    try {
      const contents = await readFile(activePortPath, 'utf8');
      const [portText] = contents.split(/\r?\n/, 1);
      const port = Number.parseInt(portText ?? '', 10);
      if (Number.isInteger(port) && port > 0 && port <= 65_535) return port;
      throw new Error(`Chrome wrote an invalid DevToolsActivePort value at ${activePortPath}.`);
    } catch (error) {
      if (!(error && typeof error === 'object' && 'code' in error && error.code === 'ENOENT')) throw error;
    }
    await sleep(50);
  }
  throw new Error(`Timed out waiting for Chrome DevToolsActivePort in ${profileDirectoryValue}.`);
}

async function waitForPage(port, url, browserProcess, timeout) {
  const deadline = Date.now() + timeout;
  const listUrl = `http://127.0.0.1:${String(port)}/json/list`;
  while (Date.now() < deadline) {
    ensureBrowserRunning(browserProcess);
    try {
      const response = await fetch(listUrl);
      if (response.ok) {
        const targets = await response.json();
        const page = targets.find((target) => target.type === 'page' && target.url === url)
          ?? targets.find((target) => target.type === 'page');
        if (page?.webSocketDebuggerUrl) return page;
      }
    } catch {
      // Chrome may not have opened the remote debugging endpoint yet.
    }
    await sleep(100);
  }
  throw new Error(`Timed out waiting for Chrome DevTools page: ${url}`);
}

async function createDevToolsClient(webSocketUrl, commandTimeout) {
  if (typeof WebSocket !== 'function') throw new Error('Runtime visual E2E requires Node.js with global WebSocket support.');
  const socket = new WebSocket(webSocketUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', () => reject(new Error('Failed to connect to Chrome DevTools.')), { once: true });
  });

  let nextId = 1;
  const pending = new Map();
  const rejectPending = (error) => {
    for (const waiter of pending.values()) {
      clearTimeout(waiter.timeout);
      waiter.reject(error);
    }
    pending.clear();
  };

  socket.addEventListener('message', (event) => {
    const message = JSON.parse(typeof event.data === 'string' ? event.data : String(event.data));
    if (typeof message.id !== 'number') return;
    const waiter = pending.get(message.id);
    if (!waiter) return;
    pending.delete(message.id);
    clearTimeout(waiter.timeout);
    if (message.error) waiter.reject(new Error(`Chrome DevTools error: ${JSON.stringify(message.error)}`));
    else waiter.resolve(message.result);
  });
  socket.addEventListener('close', () => rejectPending(new Error('Chrome DevTools WebSocket closed while commands were pending.')));
  socket.addEventListener('error', () => rejectPending(new Error('Chrome DevTools WebSocket failed while commands were pending.')));

  const command = (method, params = {}) => new Promise((resolve, reject) => {
    if (socket.readyState !== WebSocket.OPEN) {
      reject(new Error(`Chrome DevTools WebSocket is not open for command ${method}.`));
      return;
    }
    const id = nextId++;
    const timeout = setTimeout(() => {
      pending.delete(id);
      reject(new Error(`Chrome DevTools command timed out after ${String(commandTimeout)}ms: ${method}`));
    }, commandTimeout);
    pending.set(id, { resolve, reject, timeout });
    try {
      socket.send(JSON.stringify({ id, method, params }));
    } catch (error) {
      pending.delete(id);
      clearTimeout(timeout);
      reject(error instanceof Error ? error : new Error(String(error)));
    }
  });

  return {
    command,
    async evaluate(expression) {
      const response = await command('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
      if (response.exceptionDetails) throw new Error(`Browser evaluation failed: ${JSON.stringify(response.exceptionDetails)}`);
      return response.result?.value;
    },
    close() { socket.close(); },
  };
}

function ensureBrowserRunning(browserProcess) {
  if (browserProcess.exitCode !== null || browserProcess.signalCode !== null) {
    throw new Error(`Chrome exited before runtime visual E2E completion with code ${String(browserProcess.exitCode)}.`);
  }
}

function sleep(durationMs) {
  return new Promise((resolve) => setTimeout(resolve, durationMs));
}
