import { chromium, firefox, webkit } from 'playwright';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import assert from 'node:assert/strict';

const root = resolve('../../artifacts/web-w1/sample/wwwroot');
const output = resolve('../../artifacts/web-w1/contracts');
await mkdir(output, { recursive: true });
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
const server = createServer(async (req, res) => {
    try {
        const url = new URL(req.url, 'http://localhost');
        if (!url.pathname.startsWith('/contracts/v1/')) { res.writeHead(404).end(); return; }
        const path = resolve(root, decodeURIComponent(url.pathname.slice('/contracts/v1/'.length)) || 'index.html');
        if (!path.startsWith(root + sep)) { res.writeHead(403).end(); return; }
        res.writeHead(200, { 'Content-Type': mime[extname(path)] || 'application/octet-stream' });
        res.end(await readFile(path));
    } catch { res.writeHead(404).end(); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const url = `http://127.0.0.1:${server.address().port}/contracts/v1/`;
const results = [];
let failed = false;
try {
    for (const [name, engine] of Object.entries({ chromium, firefox, webkit })) {
        const browser = await engine.launch();
        const result = { engine: name, version: browser.version(), checks: [] };
        try {
            const context = await browser.newContext({ viewport: { width: 1000, height: 700 }, deviceScaleFactor: 2, acceptDownloads: true });
            const page = await context.newPage();
            const errors = [];
            page.on('pageerror', error => errors.push(String(error)));
            page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
            page.on('response', response => { if (response.status() >= 400) errors.push(`${response.status()} ${response.url()}`); });
            await page.goto(url);
            await page.waitForFunction(() => document.body.dataset.ready === 'true');
            result.checks.push('managed ready; isolated browser launcher; replay ABI accepted');
            await page.locator('canvas').click({ position: { x: 100, y: 80 } });
            await page.waitForFunction(() => document.activeElement?.id === 'input');
            await page.keyboard.type('A B'); await page.keyboard.insertText('äΩ');
            await page.waitForFunction(() => document.querySelector('#state').textContent.startsWith('A BäΩ|'));
            result.checks.push('committed Unicode and spaces reach managed state');
            const previousCommits = Number((await page.locator('#state').textContent()).split('|')[3]);
            await page.evaluate(() => {
                const input = document.querySelector('#input');
                for (const [type, data] of [['compositionstart', ''], ['compositionupdate', '漢'], ['compositionend', '漢']]) input.dispatchEvent(new CompositionEvent(type, { data, bubbles: true }));
                input.dispatchEvent(new InputEvent('input', { data: '漢', inputType: 'insertCompositionText', bubbles: true }));
            });
            await page.waitForFunction(() => document.querySelector('#state').textContent.startsWith('A BäΩ漢|'));
            assert.equal(Number(await page.locator('#state').textContent().then(text => text.split('|')[3])), previousCommits + 1);
            result.checks.push('composition commit occurs once; native input echo suppressed');
            const clipboardCount = await page.evaluate(() => window.events.filter(item => item.clipboard).length);
            await page.evaluate(() => document.querySelector('#input').dispatchEvent(new ClipboardEvent('copy', { bubbles: true })));
            assert.equal(await page.evaluate(() => window.events.filter(item => item.clipboard).length), clipboardCount);
            await page.keyboard.press('Control+c');
            await page.waitForFunction(count => window.events.filter(item => item.clipboard === 'copy').length > count, clipboardCount);
            result.checks.push('synthetic clipboard rejected; trusted copy reaches managed scope');
            const frameDelta = await page.evaluate(async () => {
                const before = Number(document.querySelector('#state').textContent.split('|')[2]);
                for (let index = 0; index < 10; index++) window.host.invalidate();
                await new Promise(requestAnimationFrame);
                return Number(document.querySelector('#state').textContent.split('|')[2]) - before;
            });
            assert.equal(frameDelta, 1);
            assert.equal(await page.locator('#input').evaluate(element => element.style.left), '10px');
            await page.evaluate(() => {
                const input = document.querySelector('#input');
                input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Shift', code: 'ShiftLeft', bubbles: true }));
                input.dispatchEvent(new CompositionEvent('compositionstart', { data: '', bubbles: true }));
                window.dispatchEvent(new Event('blur'));
            });
            await page.waitForFunction(() => Number(document.querySelector('#state').textContent.split('|')[4]) > 0);
            assert.ok(await page.evaluate(() => window.events.some(item => item.code === 'ShiftLeft' && item.down === false)));
            result.checks.push('frame requests coalesced; CSS caret coordinates; blur cancels composition and releases held keys');
            await page.mouse.move(120, 140); await page.mouse.down();
            await page.evaluate(() => {
                const id = window.events.findLast(item => item.kind === 'down').id;
                document.querySelector('canvas').dispatchEvent(new PointerEvent('pointercancel', { pointerId: id, bubbles: true }));
            });
            await page.mouse.up();
            await page.waitForFunction(() => Number(document.querySelector('#state').textContent.split('|')[1]) > 0);
            await page.evaluate(() => document.querySelector('#host').style.width = '400px');
            await page.waitForFunction(() => window.lastResize.width === 400 && window.lastResize.dpr === 2);
            result.checks.push('pointer cancellation; resize in CSS pixels with DPR=2');
            const chooser = page.waitForEvent('filechooser'); await page.locator('#open').click();
            await (await chooser).setFiles({ name: 'sheet.csv', mimeType: 'text/csv', buffer: Buffer.from('a,b\n1,2\n') });
            await page.waitForFunction(() => window.openResult?.status === 'Opened');
            await page.waitForFunction(() => window.managedOpenResult === 'Opened|sheet.csv|8');
            assert.deepEqual(await page.evaluate(() => ({ name: window.openResult.document.name, bytes: [...window.openResult.document.bytes] })), { name: 'sheet.csv', bytes: [...Buffer.from('a,b\n1,2\n')] });
            const oversized = page.waitForEvent('filechooser'); await page.locator('#open').click();
            await (await oversized).setFiles({ name: 'large.csv', mimeType: 'text/csv', buffer: Buffer.alloc(65) });
            await page.waitForFunction(() => window.openResult?.status === 'Failed');
            const aborted = await page.evaluate(async () => {
                const controller = new AbortController();
                const pending = window.documents.open({}, controller.signal);
                const busy = await window.documents.open(); controller.abort();
                return { busy: busy.status, cancelled: (await pending).status, remaining: document.querySelectorAll('input[type=file]').length };
            });
            assert.deepEqual(aborted, { busy: 'Failed', cancelled: 'Cancelled', remaining: 0 });
            result.checks.push('file bytes/name; oversized file rejected; concurrent picker rejected; abort cleans picker');
            await page.evaluate(() => { window.contractBeforeUnload = event => { event.preventDefault(); event.returnValue = ''; }; window.addEventListener('beforeunload', window.contractBeforeUnload); });
            const download = page.waitForEvent('download'); await page.locator('#export').click();
            const file = await download; assert.equal(file.suggestedFilename(), 'demo.csv');
            assert.equal(await readFile(await file.path(), 'utf8'), 'a,b\n1,2\n');
            await page.waitForFunction(() => window.exportResult?.status === 'DownloadInitiated');
            await page.waitForFunction(() => window.managedExportResult === 'DownloadInitiated');
            await page.evaluate(() => window.removeEventListener('beforeunload', window.contractBeforeUnload));
            result.checks.push('export bytes and safe filename; result is DownloadInitiated');
            await page.screenshot({ path: resolve(output, `${name}.png`) });
            await page.evaluate(() => window.host.dispose());
            const before = await page.locator('#state').textContent();
            await page.evaluate(() => document.querySelector('#input').dispatchEvent(new InputEvent('beforeinput', { data: 'ignored', inputType: 'insertText', bubbles: true })));
            await page.waitForTimeout(100); assert.equal(await page.locator('#state').textContent(), before);
            assert.deepEqual(errors, []); result.checks.push('dispose detaches input and cancels frames; no console/network failures');
            await context.close();
            const negative = await browser.newPage();
            await negative.route('**/broiler.graphics.webassembly.js', route => route.fulfill({ contentType: 'text/javascript', body: "export function getProtocolVersion(){return 'broiler.canvas/incompatible';}" }));
            await negative.goto(url);
            await negative.waitForFunction(() => document.body.dataset.failed?.includes('protocol mismatch'));
            assert.notEqual(await negative.getAttribute('body', 'data-ready'), 'true');
            result.checks.push('incompatible replay ABI fails managed startup');
            result.pass = true;
        } catch (error) { result.pass = false; result.error = String(error); failed = true; }
        finally { await browser.close(); results.push(result); console.log(JSON.stringify(result)); }
    }
} finally {
    await new Promise(resolve => server.close(resolve));
    await writeFile(resolve(output, 'report.json'), JSON.stringify(results, null, 2));
}
if (failed) process.exitCode = 1;
