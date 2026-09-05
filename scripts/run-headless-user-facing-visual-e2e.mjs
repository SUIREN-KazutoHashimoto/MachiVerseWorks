#!/usr/bin/env node

import { spawn } from 'node:child_process';
import { appendFile, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const [browserExecutable, targetUrl, artifactDirectory] = process.argv.slice(2);
if (!browserExecutable || !targetUrl || !artifactDirectory) {
  console.error('Usage: run-headless-user-facing-visual-e2e.mjs <browser> <url> <artifact-dir>');
  process.exit(2);
}

const manifestPath = process.env.MVW_USER_FACING_VISUAL_MANIFEST ?? 'src/view/tests/visual/user-facing/manifest.json';
const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
const scenes = Object.freeze((manifest.scenes ?? []).map((scene) => scene.id));
const viewport = manifest.capture?.viewport ?? {};
const browserDescriptor = manifest.capture?.browser ?? '';
const expectedBrowserVersion = browserDescriptor.split(/\s+/).at(-1) ?? '';
const expectedFontFamily = manifest.capture?.fontFamily ?? '';
const expectedFontPackageVersion = manifest.capture?.fontPackageVersion ?? '';

if (manifest.schemaVersion !== 1 || scenes.length === 0 || scenes.some((scene) => typeof scene !== 'string' || scene.length === 0)) {
  throw new Error(`Invalid VQ-0 user-facing manifest scene contract: ${manifestPath}.`);
}
if (!Number.isInteger(viewport.width) || !Number.isInteger(viewport.height) || viewport.width <= 0 || viewport.height <= 0 || viewport.devicePixelRatio !== 1) {
  throw new Error(`Invalid VQ-0 viewport contract in ${manifestPath}: ${JSON.stringify(viewport)}.`);
}
if (!expectedBrowserVersion || !expectedFontFamily || !expectedFontPackageVersion) {
  throw new Error(`VQ-0 browser/font contract is incomplete in ${manifestPath}.`);
}

const timeoutMs = 120_000;
const commandTimeoutMs = 30_000;
const profileDirectory = await mkdtemp(join(tmpdir(), 'machiverseworks-user-facing-visual-e2e-'));
const actualDirectory = join(artifactDirectory, 'actual');
const diagnosticsDirectory = join(artifactDirectory, 'diagnostics');
const browserLogPath = join(artifactDirectory, 'chrome.log');
let browser;
let devToolsSocket;

await mkdir(actualDirectory, { recursive: true });
await mkdir(diagnosticsDirectory, { recursive: true });

try {
  if (process.env.MVW_VISUAL_BROWSER_VERSION && process.env.MVW_VISUAL_BROWSER_VERSION !== expectedBrowserVersion) {
    throw new Error(`Installed visual browser contract disagrees with ${manifestPath}: manifest=${expectedBrowserVersion}, environment=${process.env.MVW_VISUAL_BROWSER_VERSION}.`);
  }
  if (process.env.MVW_VISUAL_FONT_FAMILY && process.env.MVW_VISUAL_FONT_FAMILY !== expectedFontFamily) {
    throw new Error(`Installed visual font family disagrees with ${manifestPath}: manifest=${expectedFontFamily}, environment=${process.env.MVW_VISUAL_FONT_FAMILY}.`);
  }
  if (process.env.MVW_VISUAL_FONT_PACKAGE_VERSION && process.env.MVW_VISUAL_FONT_PACKAGE_VERSION !== expectedFontPackageVersion) {
    throw new Error(`Installed visual font package disagrees with ${manifestPath}: manifest=${expectedFontPackageVersion}, environment=${process.env.MVW_VISUAL_FONT_PACKAGE_VERSION}.`);
  }

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
    `--force-device-scale-factor=${String(viewport.devicePixelRatio)}`,
    '--lang=en-US',
    '--hide-scrollbars',
    '--enable-unsafe-swiftshader',
    '--use-angle=swiftshader',
    `--window-size=${String(viewport.width)},${String(viewport.height)}`,
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
  await devToolsSocket.command('Emulation.setDeviceMetricsOverride', {
    width: viewport.width,
    height: viewport.height,
    deviceScaleFactor: viewport.devicePixelRatio,
    mobile: false,
    screenWidth: viewport.width,
    screenHeight: viewport.height,
    positionX: 0,
    positionY: 0,
    dontSetVisibleSize: false,
  });

  const browserVersion = await devToolsSocket.command('Browser.getVersion');
  if (!browserVersion.product?.endsWith(`/${expectedBrowserVersion}`)) {
    throw new Error(`User-facing visual browser version mismatch: expected ${expectedBrowserVersion}, actual ${browserVersion.product ?? 'unknown'}.`);
  }

  const initialDiagnostics = await waitForReady(devToolsSocket, browser, timeoutMs);
  const hiddenDebugChromeCount = await devToolsSocket.evaluate('window.__MACHIVERSE_USER_FACING_VISUAL_TEST__?.prepareCapture?.() ?? -1');
  if (!Number.isInteger(hiddenDebugChromeCount) || hiddenDebugChromeCount < 0) {
    throw new Error('User-facing visual capture preparation failed.');
  }

  const sceneDiagnostics = {};
  for (const scene of scenes) {
    sceneDiagnostics[scene] = await captureScene(
      devToolsSocket,
      browser,
      browserVersion,
      expectedBrowserVersion,
      expectedFontFamily,
      expectedFontPackageVersion,
      viewport,
      scene,
    );
  }

  const summary = {
    schemaVersion: 1,
    status: 'passed',
    source: 'actual Application -> MachiVerseConnection -> Server/Simulation runtime',
    role: 'user-facing reproducible visual baseline; not a Legacy parity pass/fail decision',
    manifest: manifestPath,
    viewport: { width: viewport.width, height: viewport.height, devicePixelRatio: viewport.devicePixelRatio },
    renderer: 'Chrome SwiftShader',
    browser: browserVersion,
    expectedBrowserVersion,
    visualFont: {
      family: expectedFontFamily,
      packageVersion: expectedFontPackageVersion,
    },
    hiddenDebugChromeCount,
    initialDiagnostics,
    scenes: sceneDiagnostics,
  };
  await writeFile(join(artifactDirectory, 'summary.json'), `${JSON.stringify(summary, null, 2)}\n`, 'utf8');
  console.log(`User-facing visual capture passed for ${scenes.length} VQ-0 scenes from ${manifestPath}.`);
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  console.error(normalized.stack ?? normalized.message);
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

async function waitForReady(client, browserProcess, timeout) {
  const deadline = Date.now() + timeout;
  let stableSignature = null;
  let stablePolls = 0;
  let latest = null;
  while (Date.now() < deadline) {
    ensureBrowserRunning(browserProcess);
    latest = await client.evaluate('window.__MACHIVERSE_USER_FACING_VISUAL_TEST__?.getDiagnostics?.() ?? null');
    if (latest?.ready === true) {
      const signature = [
        latest.terrainSampleCount,
        latest.settlementCount,
        latest.buildingCount,
        latest.roadSegmentCount,
        latest.pedestrianCount,
        latest.vehicleCount,
        latest.trainCount,
      ].join(':');
      if (signature === stableSignature) stablePolls += 1;
      else {
        stableSignature = signature;
        stablePolls = 1;
      }
      if (stablePolls >= 20) return latest;
    } else {
      stableSignature = null;
      stablePolls = 0;
    }
    await sleep(250);
  }
  throw new Error(`Timed out waiting for user-facing visual runtime readiness. Last diagnostics: ${JSON.stringify(latest)}`);
}

async function captureScene(client, browserProcess, browserVersion, expectedBrowserVersionValue, expectedFontFamilyValue, expectedFontPackageVersionValue, viewportValue, scene) {
  const before = await client.evaluate('window.__MACHIVERSE_USER_FACING_VISUAL_TEST__?.getDiagnostics?.() ?? null');
  const previousRoadSnapshotSequence = before?.roadSnapshotSequence ?? null;
  if (!Number.isInteger(previousRoadSnapshotSequence)) throw new Error(`Road snapshot sequence is unavailable before ${scene}.`);

  const positioned = await client.evaluate(`window.__MACHIVERSE_USER_FACING_VISUAL_TEST__?.setCheckpoint?.(${JSON.stringify(scene)}) ?? false`);
  if (positioned !== true) throw new Error(`Failed to position VQ-0 user-facing checkpoint: ${scene}.`);
  await waitForCheckpointSettle(client, browserProcess, previousRoadSnapshotSequence, commandTimeoutMs);
  await client.evaluate('new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => requestAnimationFrame(resolve))))');

  const diagnostics = await client.evaluate(`(() => {
    const selector = '.person-debug,.railway-debug,.transit-debug,.economy-debug,.performance-overlay';
    const expectedFontFamily = ${JSON.stringify(expectedFontFamilyValue)};
    let visibleDebugChromeCount = 0;
    for (const element of document.querySelectorAll(selector)) {
      const style = getComputedStyle(element);
      if (style.display !== 'none' && style.visibility !== 'hidden' && Number.parseFloat(style.opacity || '1') > 0) visibleDebugChromeCount += 1;
    }
    return {
      runtime: window.__MACHIVERSE_USER_FACING_VISUAL_TEST__?.getDiagnostics?.() ?? null,
      devicePixelRatio: window.devicePixelRatio,
      viewport: { width: window.innerWidth, height: window.innerHeight },
      visibleDebugChromeCount,
      japaneseFontReady: document.fonts.check('32px "' + expectedFontFamily + '"', '日本語漢字かなカナ'),
      location: location.href,
    };
  })()`);
  if (diagnostics.runtime?.ready !== true) throw new Error(`User-facing runtime diagnostics became unavailable at ${scene}.`);
  if (diagnostics.viewport.width !== viewportValue.width || diagnostics.viewport.height !== viewportValue.height || diagnostics.devicePixelRatio !== viewportValue.devicePixelRatio) {
    throw new Error(`User-facing viewport contract mismatch at ${scene}: ${JSON.stringify(diagnostics.viewport)} @ ${String(diagnostics.devicePixelRatio)}x.`);
  }
  if (diagnostics.visibleDebugChromeCount !== 0) throw new Error(`Debug-only chrome is visible in user-facing scene ${scene}.`);
  if (diagnostics.japaneseFontReady !== true) throw new Error(`Pinned Japanese font is not ready in user-facing scene ${scene}.`);

  diagnostics.browser = browserVersion;
  diagnostics.expectedBrowserVersion = expectedBrowserVersionValue;
  diagnostics.visualFont = {
    family: expectedFontFamilyValue,
    packageVersion: expectedFontPackageVersionValue,
  };
  await writeFile(join(diagnosticsDirectory, `${scene}.json`), `${JSON.stringify(diagnostics, null, 2)}\n`, 'utf8');

  const screenshot = await client.command('Page.captureScreenshot', {
    format: 'png',
    fromSurface: true,
    captureBeyondViewport: false,
  });
  await writeFile(join(actualDirectory, `${scene}.png`), Buffer.from(screenshot.data, 'base64'));
  return diagnostics;
}

async function waitForCheckpointSettle(client, browserProcess, previousSequence, timeout) {
  const deadline = Date.now() + timeout;
  const stableAfter = Date.now() + 2_000;
  let latest = null;
  let stableSignature = null;
  let stablePolls = 0;
  while (Date.now() < deadline) {
    ensureBrowserRunning(browserProcess);
    latest = await client.evaluate('window.__MACHIVERSE_USER_FACING_VISUAL_TEST__?.getDiagnostics?.() ?? null');
    if (latest?.ready === true) {
      const signature = [latest.roadSegmentCount, latest.pedestrianCount, latest.vehicleCount, latest.trainCount].join(':');
      if (signature === stableSignature) stablePolls += 1;
      else {
        stableSignature = signature;
        stablePolls = 1;
      }
      // A new subscription is emitted after the camera moves. Never let a late snapshot from
      // the previous composition satisfy this wait early: require both a new road snapshot and
      // a full settle window before the next scene is allowed to choose an observation anchor.
      if (Date.now() >= stableAfter && latest.roadSnapshotSequence > previousSequence && stablePolls >= 6) return latest;
    }
    await sleep(100);
  }
  throw new Error(`Timed out waiting for user-facing checkpoint subscription to settle. Previous road sequence=${String(previousSequence)}, latest=${JSON.stringify(latest)}.`);
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
  if (typeof WebSocket !== 'function') throw new Error('User-facing visual E2E requires Node.js with global WebSocket support.');
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
    throw new Error(`Chrome exited before user-facing visual E2E completion with code ${String(browserProcess.exitCode)}.`);
  }
}

function sleep(durationMs) {
  return new Promise((resolve) => setTimeout(resolve, durationMs));
}
