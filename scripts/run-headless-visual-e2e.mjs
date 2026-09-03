#!/usr/bin/env node

import { spawn } from 'node:child_process';
import { appendFile, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const [browserExecutable, targetUrl, artifactDirectory, inspectionName] = process.argv.slice(2);
if (!browserExecutable || !targetUrl || !artifactDirectory || !inspectionName) {
  console.error('Usage: run-headless-visual-e2e.mjs <browser> <url> <artifact-dir> <inspection-name>');
  process.exit(2);
}

const timeoutMs = 120_000;
const profileDirectory = await mkdtemp(join(tmpdir(), 'machiverseworks-visual-e2e-'));
const actualDirectory = join(artifactDirectory, 'actual');
const diagnosticsDirectory = join(artifactDirectory, 'diagnostics');
const browserHtmlPath = join(artifactDirectory, `${inspectionName}.browser.html`);
const browserLogPath = join(artifactDirectory, `${inspectionName}.chrome.log`);
const screenshotPath = join(actualDirectory, `${inspectionName}.png`);
const diagnosticsPath = join(diagnosticsDirectory, `${inspectionName}.json`);
let browser;
let devToolsSocket;
let resultText = '';

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
    '--enable-unsafe-swiftshader',
    '--use-angle=swiftshader',
    '--window-size=1920,1080',
    '--remote-debugging-port=0',
    `--user-data-dir=${profileDirectory}`,
    targetUrl,
  ], {
    stdio: ['ignore', 'ignore', 'pipe'],
  });

  browser.stderr.on('data', (chunk) => {
    void appendFile(browserLogPath, chunk);
  });

  const remoteDebuggingPort = await waitForDevToolsPort(profileDirectory, browser, timeoutMs);
  const page = await waitForPage(remoteDebuggingPort, targetUrl, browser, timeoutMs);
  devToolsSocket = await createDevToolsClient(page.webSocketDebuggerUrl);
  await devToolsSocket.command('Page.enable');
  await devToolsSocket.command('Runtime.enable');

  const deadline = Date.now() + timeoutMs;
  let status = 'running';
  while (status === 'running') {
    ensureBrowserRunning(browser);
    status = await devToolsSocket.evaluate(
      "document.querySelector('#result')?.dataset.status ?? 'running'",
    );

    if (status === 'passed' || status === 'failed') break;
    if (Date.now() >= deadline) {
      status = 'timeout';
      break;
    }
    await sleep(250);
  }

  const outerHtml = await devToolsSocket.evaluate('document.documentElement.outerHTML');
  resultText = await devToolsSocket.evaluate(
    "document.querySelector('#result')?.textContent ?? ''",
  );
  await writeFile(browserHtmlPath, `<!doctype html>\n${outerHtml}\n`, 'utf8');

  const diagnostics = await devToolsSocket.evaluate(`(() => {
    const result = document.querySelector('#result');
    const canvas = document.querySelector('#viewport canvas, #host canvas');
    const exposed = window.__MACHIVERSE_VISUAL_TEST__?.getSceneDiagnostics?.() ?? null;
    return {
      status: result?.dataset.status ?? 'unknown',
      result: result?.textContent ?? '',
      metrics: result instanceof HTMLElement ? { ...result.dataset } : {},
      canvas: canvas instanceof HTMLCanvasElement ? {
        width: canvas.width,
        height: canvas.height,
        clientWidth: canvas.clientWidth,
        clientHeight: canvas.clientHeight,
      } : null,
      scene: exposed,
      devicePixelRatio: window.devicePixelRatio,
      location: location.href,
    };
  })()`);
  await writeFile(diagnosticsPath, `${JSON.stringify(diagnostics, null, 2)}\n`, 'utf8');

  if (status === 'passed') {
    await devToolsSocket.evaluate(`(async () => {
      if (document.fonts?.ready) await document.fonts.ready;
      const result = document.querySelector('#result');
      if (result instanceof HTMLElement) result.style.display = 'none';
      await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    })()`);
  }

  const clip = await devToolsSocket.evaluate(`(() => {
    const canvas = document.querySelector('#viewport canvas, #host canvas');
    if (!(canvas instanceof HTMLCanvasElement)) throw new Error('Visual E2E canvas was not found.');
    const rect = canvas.getBoundingClientRect();
    return { x: rect.left, y: rect.top, width: rect.width, height: rect.height, scale: 1 };
  })()`);
  const screenshot = await devToolsSocket.command('Page.captureScreenshot', {
    format: 'png',
    fromSurface: true,
    captureBeyondViewport: false,
    clip,
  });
  await writeFile(screenshotPath, Buffer.from(screenshot.data, 'base64'));

  if (status !== 'passed') {
    throw new Error(`Visual browser E2E ended with status ${status}: ${resultText}`);
  }

  console.log(`${inspectionName}: ${resultText}`);
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  if (resultText) console.error(resultText);
  console.error(normalized.message);
  process.exitCode = 1;
} finally {
  devToolsSocket?.close();
  if (browser && browser.exitCode === null && browser.signalCode === null) {
    browser.kill('SIGTERM');
    await Promise.race([
      new Promise((resolve) => browser.once('exit', resolve)),
      sleep(2_000),
    ]);
    if (browser.exitCode === null && browser.signalCode === null) {
      browser.kill('SIGKILL');
      await Promise.race([
        new Promise((resolve) => browser.once('exit', resolve)),
        sleep(2_000),
      ]);
    }
  }
  await rm(profileDirectory, {
    recursive: true,
    force: true,
    maxRetries: 10,
    retryDelay: 100,
  });
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
          ?? targets.find((target) => target.type === 'page' && target.url.includes('/tests/browser/'));
        if (page?.webSocketDebuggerUrl) return page;
      }
    } catch {
      // Chrome may not have opened the remote debugging endpoint yet.
    }
    await sleep(100);
  }
  throw new Error(`Timed out waiting for Chrome DevTools page: ${url}`);
}

async function createDevToolsClient(webSocketUrl) {
  if (typeof WebSocket !== 'function') {
    throw new Error('This visual E2E runner requires a Node.js runtime with global WebSocket support.');
  }

  const socket = new WebSocket(webSocketUrl);
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', () => reject(new Error('Failed to connect to Chrome DevTools.')), { once: true });
  });

  let nextId = 1;
  const pending = new Map();
  socket.addEventListener('message', (event) => {
    const message = JSON.parse(typeof event.data === 'string' ? event.data : String(event.data));
    if (typeof message.id !== 'number') return;
    const waiter = pending.get(message.id);
    if (!waiter) return;
    pending.delete(message.id);
    if (message.error) waiter.reject(new Error(`Chrome DevTools error: ${JSON.stringify(message.error)}`));
    else waiter.resolve(message.result);
  });

  const command = (method, params = {}) => new Promise((resolve, reject) => {
    const id = nextId;
    nextId += 1;
    pending.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });

  return {
    command,
    async evaluate(expression) {
      const response = await command('Runtime.evaluate', {
        expression,
        returnByValue: true,
        awaitPromise: true,
      });
      if (response.exceptionDetails) {
        throw new Error(`Browser evaluation failed: ${JSON.stringify(response.exceptionDetails)}`);
      }
      return response.result?.value;
    },
    close() {
      socket.close();
    },
  };
}

function ensureBrowserRunning(browserProcess) {
  if (browserProcess.exitCode !== null || browserProcess.signalCode !== null) {
    throw new Error(`Chrome exited before visual E2E completion with code ${String(browserProcess.exitCode)}.`);
  }
}

function sleep(durationMs) {
  return new Promise((resolve) => setTimeout(resolve, durationMs));
}
