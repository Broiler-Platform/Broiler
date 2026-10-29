import { chromium, firefox, webkit } from 'playwright';
import { createServer } from 'node:http';
import { readFile, stat, mkdir, writeFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import { createHash } from 'node:crypto';

const roots = {
  writer: resolve(process.env.WRITER_WWWROOT || '../../artifacts/web-w0/writer/wwwroot'),
  cell: resolve(process.env.CELL_WWWROOT || '../../artifacts/web-w0/cell/wwwroot'),
};
const output = resolve('../../artifacts/web-w0/browser');
await mkdir(output, { recursive: true });
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css', '.svg': 'image/svg+xml' };
const report = { generated: new Date().toISOString(), roots, assets: {}, runs: [] };
for (const [app, root] of Object.entries(roots)) {
  const html = await readFile(resolve(root, 'index.html'), 'utf8');
  if (html.includes('#[')) throw new Error(`${app}: unresolved fingerprint`);
  const mapText = html.match(/<script type="importmap">([\s\S]*?)<\/script>/)?.[1];
  if (!mapText?.trim()) throw new Error(`${app}: missing published import map`);
  const map = JSON.parse(mapText);
  const entry = html.match(/<script type="module" src="([^"]+)"/)?.[1];
  if (!entry) throw new Error(`${app}: missing module entry`);
  const paths = new Set([entry, 'broiler.graphics.webassembly.js', ...Object.values(map.imports || {})]);
  for (const relative of paths) {
    if (/^(https?:|data:)/.test(relative)) throw new Error(`Unexpected external asset ${relative}`);
    await stat(resolve(root, relative));
  }
  for (const [relative, integrity] of Object.entries(map.integrity || {})) {
    const bytes = await readFile(resolve(root, map.imports?.[relative] || relative));
    const [algorithm, digest] = integrity.split('-');
    if (createHash(algorithm).update(bytes).digest('base64') !== digest) throw new Error(`Integrity mismatch: ${relative}`);
  }
  report.assets[app] = { entry, checked: paths.size, replaySha256: createHash('sha256').update(await readFile(resolve(root, 'broiler.graphics.webassembly.js'))).digest('hex') };
}
const server = createServer(async (req, res) => {
  try {
    const parts = new URL(req.url, 'http://localhost').pathname.split('/').filter(Boolean);
    const [app, version, ...tail] = parts;
    if (!roots[app] || version !== 'baseline') { res.writeHead(404).end(); return; }
    const relative = decodeURIComponent(tail.join('/') || 'index.html');
    const path = resolve(roots[app], relative);
    if (!path.startsWith(roots[app] + sep)) { res.writeHead(403).end(); return; }
    let body;
    let encoding;
    if (req.headers['accept-encoding']?.includes('br')) {
      try { body = await readFile(path + '.br'); encoding = 'br'; } catch { /* uncompressed fallback */ }
    }
    body ??= await readFile(path);
    res.writeHead(200, { 'Content-Type': mime[extname(path)] || 'application/octet-stream', 'Cache-Control': 'public, max-age=3600', 'Vary': 'Accept-Encoding', ...(encoding ? { 'Content-Encoding': encoding } : {}) });
    res.end(body);
  } catch { res.writeHead(404).end(); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
let failed = false;
try {
  for (const [name, engine] of Object.entries({ chromium, firefox, webkit })) {
    const browser = await engine.launch();
    try {
      for (const app of Object.keys(roots)) {
        const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
        for (const temperature of ['cold', 'warm']) {
          // Fresh tab, same context/cache: measure warm startup independently of unload prompts.
          const page = await context.newPage();
          page.on('dialog', dialog => dialog.accept());
          const errors = [], missing = [];
          const onError = error => errors.push(String(error));
          const onConsole = message => { if (message.type() === 'error') errors.push(message.text()); };
          const onResponse = response => { if (response.status() >= 400) missing.push(`${response.status()} ${response.url()}`); };
          page.on('pageerror', onError); page.on('response', onResponse);
          page.on('console', onConsole);
          const run = { engine: name, version: browser.version(), app, temperature, errors, missing };
          try {
            await page.goto(`${origin}/${app}/baseline/`, { waitUntil: 'domcontentloaded' });
            await page.waitForFunction(app => document.getElementById(`${app === 'cell' ? 'cells' : app}-status`)?.hidden === true, app, { timeout: 60000 });
            run.readyMs = await page.evaluate(() => performance.now());
            const canvas = page.locator('canvas');
            await canvas.click({ position: { x: 200, y: app === 'cell' ? 82 : 200 } });
            await page.waitForFunction(app => document.activeElement?.id === `${app === 'cell' ? 'cells' : app}-text-input`, app);
            await page.evaluate(() => {
              window.baselineInput = '';
              document.addEventListener('beforeinput', event => {
                if (event.inputType === 'insertText') window.baselineInput += event.data || '';
              });
            });
            await page.keyboard.type('W0 baseline');
            run.committedInput = await page.evaluate(() => window.baselineInput);
            await page.keyboard.press('Enter');
            await page.waitForTimeout(300);
            run.inputTarget = await page.evaluate(() => document.activeElement?.id);
            run.editingBridgeFocused = run.inputTarget === `${app === 'cell' ? 'cells' : app}-text-input`;
            run.network = await page.evaluate(() => ({ transferBytes: performance.getEntriesByType('resource').reduce((sum, item) => sum + item.transferSize, 0), resources: performance.getEntriesByType('resource').length }));
            await page.screenshot({ path: resolve(output, `${app}-${name}-${temperature}.png`) });
            run.bootPass = errors.length === 0 && missing.length === 0 && await page.locator('#error-panel').isHidden();
            run.pass = run.bootPass && run.editingBridgeFocused && run.committedInput === 'W0 baseline';
          } catch (error) { run.pass = false; run.failure = String(error); }
          page.off('pageerror', onError); page.off('response', onResponse);
          page.off('console', onConsole);
          failed ||= !run.pass;
          report.runs.push(run);
          console.log(JSON.stringify(run));
          await page.close();
        }
        await context.close();
      }
      const page = await browser.newPage();
      let missingEntryObserved = false;
      await page.route(`**/${report.assets.cell.entry}`, route => {
        missingEntryObserved = true;
        return route.fulfill({ status: 404, body: 'intentional missing entry' });
      });
      await page.goto(`${origin}/cell/baseline/`);
      await page.waitForTimeout(1000);
      const detected = missingEntryObserved && await page.locator('#cells-status').evaluate(element => !element.hidden);
      report.runs.push({ engine: name, negativeMissingEntryDetected: detected });
      failed ||= !detected;
      await page.close();
    } finally { await browser.close(); }
  }
} finally {
  await new Promise(resolve => server.close(resolve));
  await writeFile(resolve(output, 'report.json'), JSON.stringify(report, null, 2));
}
if (failed) process.exitCode = 1;
