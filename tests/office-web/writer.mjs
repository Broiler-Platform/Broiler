import { chromium, firefox, webkit } from 'playwright';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import assert from 'node:assert/strict';

const root = resolve(process.env.WRITER_WWWROOT || '../../artifacts/web-w3/writer/wwwroot');
const fixtures = resolve('../../artifacts/web-w3/fixtures');
const output = resolve('../../artifacts/web-w3/browser'); await mkdir(output, { recursive: true });
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm' };
const server = createServer(async (req, res) => {
    try {
        const url = new URL(req.url, 'http://localhost');
        if (!url.pathname.startsWith('/writer/v1/')) { res.writeHead(404).end(); return; }
        const path = resolve(root, decodeURIComponent(url.pathname.slice('/writer/v1/'.length)) || 'index.html');
        if (!path.startsWith(root + sep)) { res.writeHead(403).end(); return; }
        const bytes = await readFile(path); res.writeHead(200, { 'Content-Type': mime[extname(path)] || 'application/octet-stream' }); res.end(bytes);
    } catch { res.writeHead(404).end(); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}/writer/v1/`;
const report = { generated: new Date().toISOString(), runs: [] };
let failed = false;
async function ready(page) {
    await page.goto(origin); await page.waitForFunction(() => document.getElementById('writer-status')?.hidden === true);
    await page.locator('#writer-info summary').click();
}
async function focusEditor(page) {
    await page.locator('canvas').click({ position: { x: 120, y: 140 } });
    await page.waitForFunction(() => document.activeElement?.id === 'writer-text-input');
}
async function waitText(page, text) { await page.waitForFunction(text => document.getElementById('writer-preview').textContent === text, text); }
async function outcome(page, value) {
    try { await page.waitForFunction(value => document.getElementById('writer-operation').dataset.outcome === value, value); }
    catch (error) { throw new Error(String(error) + ' Expected ' + value + '; actual: ' + await page.locator('#writer-operation').textContent()); }
}
async function dialogButton(page, index) {
    await page.waitForTimeout(150);
    const box = await page.locator('canvas').boundingBox();
    await page.locator('canvas').click({ position: { x: box.width / 2 - 38 + index * 104, y: box.height / 2 + 77 } });
}
async function openFile(page, file) {
    await focusEditor(page);
    const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
    await (await picker).setFiles(typeof file === 'string' ? resolve(fixtures, file) : file);
}
async function exportAs(page, filename, path) {
    const download = page.waitForEvent('download');
    const [result] = await Promise.all([download, chooseExport(page, filename)]);
    await result.saveAs(path); assert.equal(result.suggestedFilename(), filename.split(/[\\/]/).at(-1));
    await outcome(page, 'ExportInitiated');
}
async function chooseExport(page, filename) {
    await focusEditor(page); await page.keyboard.press('Control+Shift+s');
    const dialog = page.getByRole('dialog', { name: 'Export document' });
    await dialog.getByLabel('Filename', { exact: true }).fill(filename);
    await dialog.getByRole('button', { name: 'Export', exact: true }).click();
}
for (const [engineName, engine] of Object.entries({ chromium, firefox, webkit })) {
    if (process.env.W3_ENGINES && !process.env.W3_ENGINES.split(',').includes(engineName)) continue;
    const browser = await engine.launch(); const result = { engine: engineName, version: browser.version(), checks: [] };
    const mark = text => { result.checks.push(text); console.log(engineName + ': ' + text); };
    const directory = resolve(output, engineName); await mkdir(directory, { recursive: true });
    const errors = [], requests = [], slowFrames = [];
    let page;
    try {
        const context = await browser.newContext({ viewport: { width: 1280, height: 960 }, acceptDownloads: true });
        await context.addInitScript(() => {
            window.writerDownloadObservations = [];
            const click = HTMLAnchorElement.prototype.click;
            HTMLAnchorElement.prototype.click = function () {
                window.writerDownloadObservations.push({ name: this.download, target: this.target, active: navigator.userActivation?.isActive, href: this.href.slice(0, 80) });
                return click.call(this);
            };
        });
        context.on('page', active => {
            active.on('pageerror', error => errors.push(String(error)));
            active.on('console', message => { if (message.type() === 'error') errors.push(message.text()); if (message.text().startsWith('Writer frame exceeded')) slowFrames.push(message.text()); });
            active.on('request', request => { if (!request.url().startsWith(origin) && !request.url().startsWith('blob:')) requests.push(request.url()); });
        });
        page = await context.newPage(); await ready(page); await focusEditor(page);
        page.on('dialog', async dialog => { result.unexpectedDialogs ??= []; result.unexpectedDialogs.push(dialog.type()); await dialog.dismiss(); });
        await page.evaluate(() => {
            window.writerTestKeys = [];
            document.addEventListener('keydown', event => window.writerTestKeys.push({ key: event.key, ctrl: event.ctrlKey, shift: event.shiftKey, target: event.target.id }), true);
        });
        await page.keyboard.press('Control+n'); await outcome(page, 'New'); await waitText(page, '');
        await focusEditor(page); await page.keyboard.insertText('Alpha ÄΩ'); await waitText(page, 'Alpha ÄΩ');
        await page.keyboard.press('Control+a');
        await page.evaluate(() => document.getElementById('writer-text-input').addEventListener('copy', event => { window.ownCopiedText = event.clipboardData?.getData('text/plain'); }));
        await page.keyboard.press('Control+c');
        await page.waitForFunction(() => window.ownCopiedText === 'Alpha ÄΩ');
        await page.keyboard.press('Control+x'); await waitText(page, '');
        await page.keyboard.press('Control+v'); await waitText(page, 'Alpha ÄΩ');
        await page.keyboard.press('Control+a');
        for (const key of ['b', 'i', 'u']) await page.keyboard.press(`Control+${key}`);
        await exportAs(page, 'styled.rtf', resolve(directory, 'styled.rtf'));
        assert.equal(await page.getAttribute('body', 'data-modified'), 'true');
        for (let index = 0; index < 3; index++) await page.keyboard.press('Control+z');
        await exportAs(page, 'undone.rtf', resolve(directory, 'undone.rtf'));
        for (let index = 0; index < 3; index++) await page.keyboard.press('Control+y');
        await exportAs(page, 'redone.rtf', resolve(directory, 'redone.rtf'));
        mark('New, Unicode, select, trusted plain-text copy/cut/paste, emphasis, undo/redo; downloads retain modified state');
        await focusEditor(page); await page.keyboard.press('Control+n');
        await page.screenshot({ path: resolve(directory, 'unsaved-prompt.png') });
        const exporting = page.waitForEvent('download'); await dialogButton(page, 0);
        await (await exporting).saveAs(resolve(directory, 'save-before-new.rtf')); await outcome(page, 'ExportInitiated');
        await waitText(page, 'Alpha ÄΩ'); assert.equal(await page.getAttribute('body', 'data-modified'), 'true');
        await focusEditor(page); await page.keyboard.press('Control+n'); await page.keyboard.press('Escape'); await waitText(page, 'Alpha ÄΩ');
        await page.keyboard.press('Control+n'); await dialogButton(page, 1); await outcome(page, 'New'); await waitText(page, '');
        mark('Export-before-New keeps the document; cancel preserves it; explicit Discard permits New');
        for (const format of ['rtf', 'docx', 'odt', 'html', 'md']) {
            await openFile(page, 'basic.' + format); await outcome(page, 'Opened');
            assert.match(await page.locator('#writer-preview').textContent(), /Round trip ÄΩ/);
            await exportAs(page, 'roundtrip.' + format, resolve(directory, 'roundtrip.' + format));
            assert.equal(await page.getAttribute('body', 'data-modified'), 'false');
        }
        mark('RTF/DOCX/ODT/HTML/Markdown open/export round trips; actual downloads captured for independent model inspection');
        await openFile(page, 'basic.html'); await outcome(page, 'Opened');
        await exportAs(page, 'lossy.md', resolve(directory, 'lossy.md'));
        assert.match(await page.locator('#writer-notes').textContent(), /markdown\.subset/);
        const before = await page.locator('#writer-preview').textContent();
        for (const file of ['broken.docx', 'expanded.docx', 'too-much-text.md', 'bad-image.docx']) {
            await openFile(page, file); await outcome(page, 'Failed'); await waitText(page, before);
        }
        await openFile(page, { name: 'too-large.rtf', mimeType: 'application/rtf', buffer: Buffer.alloc(8 * 1024 * 1024 + 1) });
        await outcome(page, 'Failed'); await waitText(page, before);
        await focusEditor(page); const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+o'); await picker;
        await page.evaluate(() => document.querySelector('input[type=file]:not([aria-label])').dispatchEvent(new Event('cancel')));
        await outcome(page, 'Canceled'); await waitText(page, before);
        await focusEditor(page); await page.keyboard.press('Control+Shift+s'); await page.getByRole('dialog', { name: 'Export document' }).getByRole('button', { name: 'Cancel', exact: true }).click();
        await outcome(page, 'Canceled'); await waitText(page, before);
        await chooseExport(page, 'unsupported.pdf');
        await outcome(page, 'Failed'); await waitText(page, before);
        await page.evaluate(() => { window.originalObjectUrl = URL.createObjectURL; URL.createObjectURL = () => { throw new Error('download fixture failure'); }; });
        await chooseExport(page, 'blocked.rtf');
        await outcome(page, 'Failed'); await waitText(page, before);
        await page.evaluate(() => { URL.createObjectURL = window.originalObjectUrl; });
        mark('conversion loss has visible notes; failed/oversized/expanded/limited reads and canceled pickers preserve the document');
        await openFile(page, 'unsupported.html'); await outcome(page, 'Opened');
        await page.screenshot({ path: resolve(directory, 'format-notes.png') });
        await openFile(page, 'external-image.html'); await outcome(page, 'Opened');
        await openFile(page, 'font.html'); await outcome(page, 'Opened');
        await page.locator('canvas').click({ position: { x: 405, y: 50 } });
        await page.keyboard.press('Escape');
        await exportAs(page, 'font.rtf', resolve(directory, 'font.rtf'));
        await openFile(page, 'image.html'); await outcome(page, 'Opened');
        assert.match(await page.locator('#writer-notes').textContent(), /html\.skip\.external/);
        await openFile(page, 'image.docx'); await outcome(page, 'Opened');
        await page.waitForFunction(() => {
            const canvas = document.querySelector('canvas'); const data = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data;
            let red = 0; for (let index = 0; index < data.length; index += 4) if (data[index] > 240 && data[index + 1] < 20 && data[index + 2] < 20) red++;
            return red >= 16;
        });
        await exportAs(page, 'image.docx', resolve(directory, 'image.docx'));
        await page.screenshot({ path: resolve(directory, 'image.png') });
        await openFile(page, 'long.md'); await outcome(page, 'Opened');
        assert.match(await page.locator('#writer-preview').textContent(), /Paragraph 0/);
        await exportAs(page, 'long.md', resolve(directory, 'long.md'));
        mark('unsupported content, embedded/external images and a 1,000-paragraph fixture exercised without external resource requests');
        await context.close();

        // File-handle transport providers are controlled doubles: no OS files or grants are touched.
        const handles = await browser.newContext({ viewport: { width: 1280, height: 960 } });
        handles.on('page', active => active.on('pageerror', error => errors.push(String(error))));
        await handles.addInitScript(() => {
            window.fileMode = 'success'; window.savedWrites = [];
            window.showSaveFilePicker = async () => {
                if (window.fileMode === 'cancel') throw new DOMException('canceled', 'AbortError');
                if (window.fileMode === 'permission') throw new DOMException('denied', 'NotAllowedError');
                return { name: 'handle.rtf', async createWritable() {
                    return { async write(bytes) { window.savedWrites.push([...bytes]); },
                        async close() {
                            if (window.fileMode === 'close-fail') throw new DOMException('write failed', 'NotAllowedError');
                            if (window.fileMode === 'deferred') await new Promise(resolve => { window.finishWrite = resolve; });
                        }, async abort() { window.abortedWrite = true; } };
                } };
            };
        });
        page = await handles.newPage(); await ready(page); await focusEditor(page); await page.keyboard.press('Control+n'); await outcome(page, 'New');
        await focusEditor(page); await page.keyboard.insertText('Handle text'); await waitText(page, 'Handle text');
        for (const mode of ['cancel', 'permission', 'close-fail']) {
            await page.evaluate(mode => { window.fileMode = mode; }, mode); await page.locator('#writer-save-file').click();
            await outcome(page, mode === 'cancel' ? 'Canceled' : 'Failed'); assert.equal(await page.getAttribute('body', 'data-modified'), 'true');
        }
        await page.evaluate(() => { window.fileMode = 'deferred'; }); await page.locator('#writer-save-file').click();
        await page.waitForFunction(() => typeof window.finishWrite === 'function');
        await focusEditor(page); await page.keyboard.press('Control+End'); await page.keyboard.insertText(' newer'); await waitText(page, 'Handle text newer');
        await page.evaluate(() => window.finishWrite()); await outcome(page, 'SavedToHandle'); assert.equal(await page.getAttribute('body', 'data-modified'), 'true');
        await page.evaluate(() => { window.fileMode = 'success'; }); await page.locator('#writer-save-file').click();
        await page.waitForFunction(() => document.body.dataset.modified === 'false');
        assert.equal(await page.locator('#writer-operation').getAttribute('data-outcome'), 'SavedToHandle');
        await writeFile(resolve(directory, 'handle.rtf'), Buffer.from(await page.evaluate(() => window.savedWrites.at(-1))));
        await focusEditor(page); await page.keyboard.press('Control+n'); await outcome(page, 'New'); await waitText(page, '');
        mark('handle cancel/permission/close failures retain dirty state; newer edits survive delayed close; confirmed current write permits New');
        await focusEditor(page); await page.keyboard.insertText('Reload text'); await waitText(page, 'Reload text');
        page.once('dialog', dialog => dialog.accept()); await page.reload();
        await page.waitForFunction(() => document.getElementById('writer-status')?.hidden === true);
        mark('accepted edited-document reload reaches ready again');
        await handles.close();
        assert.deepEqual(errors, []); assert.deepEqual(requests, []);
        result.pass = true;
    } catch (error) {
        failed = true; result.pass = false; result.error = String(error);
        if (process.env.W3_DOWNLOAD_DIAG) {
            for (const target of ['_self', '_blank']) {
                try {
                    const download = page.waitForEvent('download', { timeout: 3000 });
                    const action = page.evaluate(target => { const anchor = document.createElement('a'); anchor.href = URL.createObjectURL(new Blob(['hello'], { type: 'application/octet-stream' })); anchor.download = 'debug.rtf'; anchor.target = target; document.body.append(anchor); anchor.click(); }, target);
                    await Promise.all([download, action]); console.log('Direct page download worked: ' + target);
                } catch (diagnosticError) { console.log('Direct page download failed: ' + target + ': ' + diagnosticError); }
            }
        }
        try { result.details = await page.evaluate(() => ({ status: document.getElementById('writer-operation')?.textContent, fatal: document.getElementById('error')?.textContent, keys: window.writerTestKeys?.slice(-12), focus: document.activeElement?.id, hasFocus: document.hasFocus(), downloads: window.writerDownloadObservations })); result.details.errors = errors; result.details.slowFrames = slowFrames; result.details.pages = page.context().pages().map(page => page.url()); } catch { }
        try { await page?.screenshot({ path: resolve(directory, 'failure.png') }); } catch { }
    } finally { await browser.close(); report.runs.push(result); console.log(JSON.stringify(result)); }
}
await new Promise(resolve => server.close(resolve));
await writeFile(resolve(output, 'report.json'), JSON.stringify(report, null, 2));
if (failed) process.exitCode = 1;
