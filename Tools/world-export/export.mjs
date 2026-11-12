// Exports the models of DRONE VR for the Blender and Unity pipeline.
//   node export.mjs            writes out/glb/*.glb and out/textures/*.png
//   node export.mjs --preview  also out/preview/*.png, pictures of the models as three.js draws them
// It serves this folder with Vite, opens index.html in a headless Chromium (Playwright) and saves what the
// page builds. Needs: npm install, and Playwright with its Chromium (npm i -D playwright; npx playwright install chromium).
import { createServer } from 'vite';
import { createRequire } from 'node:module';
import { mkdir, writeFile, rm } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const out = path.join(here, 'out');
const require = createRequire(import.meta.url);
let chromium;
try { ({ chromium } = require('playwright')); } catch (e) { ({ chromium } = createRequire('/')('playwright')); }

await rm(out, { recursive: true, force: true });
const server = await createServer({ root: here, logLevel: 'warn', server: { port: 5199, strictPort: false, host: '127.0.0.1' } });
await server.listen();
const url = server.resolvedUrls.local[0];
// --preview also takes pictures of the models (out/preview): that needs WebGL, which a headless browser draws in software
const preview = process.argv.includes('--preview');
const browser = await chromium.launch(preview ? { args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] } : {});
const page = await browser.newPage();
const parts = new Map();
let finish; const done = new Promise((res) => { finish = res; });
await page.exposeFunction('__log', (t) => console.log(t));
await page.exposeFunction('__save', async (rel, i, n, b64) => {
  if (!parts.has(rel)) parts.set(rel, []);
  parts.get(rel)[i] = b64;
  if (parts.get(rel).filter(Boolean).length === n) {
    const file = path.join(out, rel);
    await mkdir(path.dirname(file), { recursive: true });
    await writeFile(file, Buffer.from(parts.get(rel).join(''), 'base64'));
    parts.delete(rel);
  }
});
await page.exposeFunction('__done', (status) => finish(status));
page.on('pageerror', (e) => console.error('page error:', e.message));
page.on('console', (m) => { if (m.type() === 'error') console.error('console error:', m.text()); });
await page.goto(url + (preview ? '?preview' : ''));
const status = await Promise.race([done, new Promise((res) => setTimeout(() => res('error: timed out'), 180000))]);
await browser.close();
await server.close();
console.log(status === 'ok' ? 'Export finished: ' + out : status);
process.exit(status === 'ok' ? 0 : 1);
