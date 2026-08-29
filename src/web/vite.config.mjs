import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

const currentDirectory = dirname(fileURLToPath(import.meta.url));
const version = readFileSync(resolve(currentDirectory, '../../VERSION'), 'utf8').trim();

export default defineConfig({
  define: {
    __APP_VERSION__: JSON.stringify(version),
  },
});
