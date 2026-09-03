#!/usr/bin/env node

import { readFile, writeFile } from 'node:fs/promises';
import { deflateSync, inflateSync } from 'node:zlib';

const [expectedPath, actualPath, diffPath, reportPath] = process.argv.slice(2);
if (!expectedPath || !actualPath || !diffPath || !reportPath) {
  console.error('Usage: compare-visual-png.mjs <expected.png> <actual.png> <diff.png> <report.json>');
  process.exit(2);
}

const perChannelThreshold = Number.parseInt(process.env.MVW_VISUAL_CHANNEL_THRESHOLD ?? '8', 10);
const maximumChangedPixelRatio = Number.parseFloat(process.env.MVW_VISUAL_MAX_CHANGED_RATIO ?? '0.001');
if (!Number.isInteger(perChannelThreshold) || perChannelThreshold < 0 || perChannelThreshold > 255) {
  throw new Error('MVW_VISUAL_CHANNEL_THRESHOLD must be an integer from 0 to 255.');
}
if (!Number.isFinite(maximumChangedPixelRatio) || maximumChangedPixelRatio < 0 || maximumChangedPixelRatio > 1) {
  throw new Error('MVW_VISUAL_MAX_CHANGED_RATIO must be between 0 and 1.');
}

const expected = decodePng(await readFile(expectedPath));
const actual = decodePng(await readFile(actualPath));
if (expected.width !== actual.width || expected.height !== actual.height) {
  const report = {
    passed: false,
    reason: 'dimension-mismatch',
    expected: { width: expected.width, height: expected.height },
    actual: { width: actual.width, height: actual.height },
  };
  await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
  throw new Error(`Visual dimensions differ: expected ${expected.width}x${expected.height}, actual ${actual.width}x${actual.height}.`);
}

const totalPixels = expected.width * expected.height;
const diffPixels = Buffer.alloc(totalPixels * 4);
let changedPixels = 0;
let maximumObservedDelta = 0;
let totalAbsoluteDelta = 0;

for (let pixel = 0; pixel < totalPixels; pixel += 1) {
  const offset = pixel * 4;
  let pixelDelta = 0;
  for (let channel = 0; channel < 4; channel += 1) {
    const delta = Math.abs(expected.data[offset + channel] - actual.data[offset + channel]);
    totalAbsoluteDelta += delta;
    pixelDelta = Math.max(pixelDelta, delta);
    maximumObservedDelta = Math.max(maximumObservedDelta, delta);
  }

  if (pixelDelta > perChannelThreshold) {
    changedPixels += 1;
    diffPixels[offset] = 255;
    diffPixels[offset + 1] = 0;
    diffPixels[offset + 2] = 255;
    diffPixels[offset + 3] = 255;
  } else {
    const luminance = Math.round(
      actual.data[offset] * 0.299
      + actual.data[offset + 1] * 0.587
      + actual.data[offset + 2] * 0.114,
    );
    diffPixels[offset] = luminance;
    diffPixels[offset + 1] = luminance;
    diffPixels[offset + 2] = luminance;
    diffPixels[offset + 3] = 160;
  }
}

const changedPixelRatio = totalPixels === 0 ? 0 : changedPixels / totalPixels;
const meanAbsoluteChannelDelta = totalPixels === 0 ? 0 : totalAbsoluteDelta / (totalPixels * 4);
const passed = changedPixelRatio <= maximumChangedPixelRatio;
const report = {
  passed,
  width: expected.width,
  height: expected.height,
  totalPixels,
  changedPixels,
  changedPixelRatio,
  maximumObservedDelta,
  meanAbsoluteChannelDelta,
  thresholds: {
    perChannel: perChannelThreshold,
    maximumChangedPixelRatio,
  },
};

await writeFile(diffPath, encodePng(expected.width, expected.height, diffPixels));
await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
console.log(JSON.stringify(report));
if (!passed) {
  throw new Error(`Visual regression detected: ${(changedPixelRatio * 100).toFixed(4)}% changed pixels exceeds ${(maximumChangedPixelRatio * 100).toFixed(4)}%.`);
}

function decodePng(buffer) {
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  if (buffer.length < 8 || !buffer.subarray(0, 8).equals(signature)) throw new Error('Input is not a PNG file.');

  let offset = 8;
  let width = 0;
  let height = 0;
  let bitDepth = 0;
  let colorType = -1;
  let interlace = -1;
  const idat = [];

  while (offset + 12 <= buffer.length) {
    const length = buffer.readUInt32BE(offset);
    const type = buffer.toString('ascii', offset + 4, offset + 8);
    const dataStart = offset + 8;
    const dataEnd = dataStart + length;
    if (dataEnd + 4 > buffer.length) throw new Error('PNG chunk exceeds file length.');
    const data = buffer.subarray(dataStart, dataEnd);
    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
      interlace = data[12];
    } else if (type === 'IDAT') {
      idat.push(data);
    } else if (type === 'IEND') {
      break;
    }
    offset = dataEnd + 4;
  }

  if (width <= 0 || height <= 0) throw new Error('PNG has invalid dimensions.');
  if (bitDepth !== 8 || ![2, 6].includes(colorType) || interlace !== 0) {
    throw new Error(`Unsupported PNG format: bitDepth=${bitDepth}, colorType=${colorType}, interlace=${interlace}.`);
  }

  const bytesPerPixel = colorType === 6 ? 4 : 3;
  const rowBytes = width * bytesPerPixel;
  const inflated = inflateSync(Buffer.concat(idat));
  if (inflated.length !== height * (rowBytes + 1)) throw new Error('PNG decompressed size does not match dimensions.');
  const raw = Buffer.alloc(height * rowBytes);

  for (let y = 0; y < height; y += 1) {
    const sourceRow = y * (rowBytes + 1);
    const targetRow = y * rowBytes;
    const filter = inflated[sourceRow];
    for (let x = 0; x < rowBytes; x += 1) {
      const encoded = inflated[sourceRow + 1 + x];
      const left = x >= bytesPerPixel ? raw[targetRow + x - bytesPerPixel] : 0;
      const up = y > 0 ? raw[targetRow + x - rowBytes] : 0;
      const upLeft = y > 0 && x >= bytesPerPixel ? raw[targetRow + x - rowBytes - bytesPerPixel] : 0;
      let value;
      if (filter === 0) value = encoded;
      else if (filter === 1) value = (encoded + left) & 0xff;
      else if (filter === 2) value = (encoded + up) & 0xff;
      else if (filter === 3) value = (encoded + Math.floor((left + up) / 2)) & 0xff;
      else if (filter === 4) value = (encoded + paeth(left, up, upLeft)) & 0xff;
      else throw new Error(`Unsupported PNG filter ${filter}.`);
      raw[targetRow + x] = value;
    }
  }

  if (colorType === 6) return { width, height, data: raw };
  const rgba = Buffer.alloc(width * height * 4);
  for (let pixel = 0; pixel < width * height; pixel += 1) {
    rgba[pixel * 4] = raw[pixel * 3];
    rgba[pixel * 4 + 1] = raw[pixel * 3 + 1];
    rgba[pixel * 4 + 2] = raw[pixel * 3 + 2];
    rgba[pixel * 4 + 3] = 255;
  }
  return { width, height, data: rgba };
}

function encodePng(width, height, rgba) {
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;
  ihdr[9] = 6;
  ihdr[10] = 0;
  ihdr[11] = 0;
  ihdr[12] = 0;

  const rowBytes = width * 4;
  const scanlines = Buffer.alloc(height * (rowBytes + 1));
  for (let y = 0; y < height; y += 1) {
    const rowStart = y * (rowBytes + 1);
    scanlines[rowStart] = 0;
    rgba.copy(scanlines, rowStart + 1, y * rowBytes, (y + 1) * rowBytes);
  }

  return Buffer.concat([
    signature,
    pngChunk('IHDR', ihdr),
    pngChunk('IDAT', deflateSync(scanlines, { level: 9 })),
    pngChunk('IEND', Buffer.alloc(0)),
  ]);
}

function pngChunk(type, data) {
  const typeBuffer = Buffer.from(type, 'ascii');
  const output = Buffer.alloc(data.length + 12);
  output.writeUInt32BE(data.length, 0);
  typeBuffer.copy(output, 4);
  data.copy(output, 8);
  output.writeUInt32BE(crc32(Buffer.concat([typeBuffer, data])), data.length + 8);
  return output;
}

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit += 1) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function paeth(left, up, upLeft) {
  const estimate = left + up - upLeft;
  const leftDistance = Math.abs(estimate - left);
  const upDistance = Math.abs(estimate - up);
  const upLeftDistance = Math.abs(estimate - upLeft);
  if (leftDistance <= upDistance && leftDistance <= upLeftDistance) return left;
  if (upDistance <= upLeftDistance) return up;
  return upLeft;
}
