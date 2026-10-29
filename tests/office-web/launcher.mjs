import { chromium, firefox, webkit } from 'playwright';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import { createHash, randomUUID } from 'node:crypto';
import assert from 'node:assert/strict';

const roots = { 'demo.broiler.app': resolve('../../artifacts/web-w2/launcher/wwwroot'), 'writer.broiler.app': resolve(process.env.WRITER_WWWROOT || '../../artifacts/web-w0/writer/wwwroot'), 'cell.broiler.app': resolve(process.env.CELL_WWWROOT || '../../artifacts/web-w0/cell/wwwroot') };
const output = resolve('../../artifacts/web-w2/browser'); await mkdir(output, { recursive: true });
const prefix = '/preview/office-1/';
const home = 'https://demo.broiler.app' + prefix;
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css' };
const html = await readFile(resolve(roots['demo.broiler.app'], 'index.html'), 'utf8');
const entry = html.match(/<script type="module" src="([^"]+)"/)?.[1];
assert.ok(entry && !html.includes('#['));
const replay = await readFile(resolve(roots['demo.broiler.app'], 'broiler.graphics.webassembly.js'));
const replayManifest = JSON.parse(await readFile(resolve(roots['demo.broiler.app'], 'broiler.graphics.replay.json'), 'utf8'));
assert.equal(createHash('sha256').update(replay).digest('hex'), replayManifest.sha256.toLowerCase());
const graph = JSON.parse(await readFile(resolve('../../artifacts/web-w2/launcher-project.assets.json'), 'utf8'));
assert.equal(replayManifest.packageVersion, Object.keys(graph.libraries).find(name => name.startsWith('Broiler.Graphics.WebAssembly/')).split('/')[1]);
assert.ok(!Object.keys(graph.libraries).some(name => /^(Broiler\.Launcher\.Core|Broiler\.Native|Broiler\.Hosting\.(Windows|Linux|Android)|Broiler\.Graphics\.(Windows|Linux))(\/|\.)/.test(name)));

async function routeArtifacts(context, options = {}) {
    const unexpected = [];
    await context.route('**/*', async route => {
        const url = new URL(route.request().url());
        const root = roots[url.hostname];
        if (!root) { unexpected.push(url.href); return route.abort(); }
        let relative = decodeURIComponent(url.pathname);
        if (url.hostname === 'demo.broiler.app' && relative.startsWith(prefix)) relative = relative.slice(prefix.length);
        else relative = relative.replace(/^\//, '');
        relative ||= 'index.html';
        if (url.hostname === 'demo.broiler.app') {
            if ((options.failure === 'entry' && relative === entry) || (options.failure === 'runtime' && relative.endsWith('.wasm'))) return route.fulfill({ status: 404, body: 'intentional fixture' });
            if (options.failure === 'replay' && relative === 'broiler.graphics.webassembly.js') return route.fulfill({ contentType: 'text/javascript', body: "export function getProtocolVersion(){return 'incompatible';}" });
            if (relative === 'office-web.json' && options.metadata) return route.fulfill({ contentType: 'application/json', body: JSON.stringify(options.metadata) });
        }
        const path = resolve(root, relative);
        if (!path.startsWith(root + sep)) return route.fulfill({ status: 403, body: '' });
        try { return route.fulfill({ status: 200, contentType: mime[extname(path)] || 'application/octet-stream', body: await readFile(path) }); }
        catch { return route.fulfill({ status: 404, body: relative }); }
    });
    return unexpected;
}
async function launcherReady(page) {
    await page.waitForFunction(() => document.body.dataset.ready === 'true');
    await page.waitForTimeout(150);
}
async function editorReady(page, id) {
    await page.waitForURL(`https://${id === 'writer' ? 'writer' : 'cell'}.broiler.app/`);
    await page.waitForFunction(id => document.getElementById(`${id === 'writer' ? 'writer' : 'cells'}-status`)?.hidden === true, id);
    assert.equal(await page.getByRole('link', { name: 'Return to Office' }).getAttribute('href'), 'https://demo.broiler.app/');
}
async function returnToOffice(page) { await page.getByRole('link', { name: 'Return to Office' }).click(); await launcherReady(page); }
async function canvasButton(page, index) {
    const box = await page.locator('canvas').boundingBox(); const columns = box.width >= 700 ? 2 : 1;
    const cardWidth = (box.width - 48 - (columns - 1) * 16) / columns;
    await page.locator('canvas').click({ position: { x: 24 + index % columns * (cardWidth + 16) + 70, y: 162 + Math.floor(index / columns) * 182 + 130 } });
}
const report = { generated: new Date().toISOString(), deployment: 'local artifact routes; public DNS/hosting not changed', runs: [] };
let failed = false;
for (const [name, engine] of Object.entries({ chromium, firefox, webkit })) {
    const persistent = process.env.OFFICE_WEB_PERSISTENT_PROFILES === '1';
    const browser = persistent ? null : await engine.launch();
    const result = { engine:name, checks:[] }, ownedContexts=[];
    const createContext = async (options={}) => {
        const context = persistent
            ? await engine.launchPersistentContext(resolve(output,name+'-profile-'+randomUUID()),{...options,...(name==='chromium'?{channel:'chromium'}:{})})
            : await browser.newContext(options);
        ownedContexts.push(context); result.version = context.browser().version(); return context;
    };
    try {
        const context = await createContext({ viewport: { width: 1100, height: 1000 } });
        const unexpected = await routeArtifacts(context);
        const page = context.pages()[0] || await context.newPage(); const errors = [];
        page.on('pageerror', error => errors.push(String(error)));
        page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
        await page.goto(home); await launcherReady(page);
        assert.equal(await page.locator('#deployment').textContent(), 'Local preview');
        await page.screenshot({ path: resolve(output, `${name}-desktop.png`) });
        const original = page.url();
        await canvasButton(page, 0); await canvasButton(page, 3);
        await page.locator('canvas').focus(); await page.keyboard.press('Control+1'); await page.keyboard.press('Control+4');
        assert.equal(page.url(), original); assert.equal(context.pages().length, 1);
        result.checks.push('version-prefix boot; native services absent; Browser/Plate mouse and shortcuts inert');
        await canvasButton(page, 1); await editorReady(page, 'writer'); await returnToOffice(page);
        await canvasButton(page, 2); await editorReady(page, 'cell'); await returnToOffice(page);
        result.checks.push('Canvas mouse opens actual published Writer/Cell; both return to Office');
        for (const [key, id] of [['2', 'writer'], ['3', 'cell']]) {
            await page.locator('canvas').focus(); await page.keyboard.press(`Control+${key}`); await editorReady(page, id); await returnToOffice(page);
        }
        await page.locator('canvas').focus(); await page.keyboard.press('Escape');
        assert.equal(await page.evaluate(() => document.activeElement?.getAttribute('data-product')), 'writer');
        await page.keyboard.press('Enter');
        await editorReady(page, 'writer'); await returnToOffice(page);
        result.checks.push('keyboard shortcuts and accessible Enter navigation');
        await page.locator('canvas').focus(); await page.keyboard.press('Control+2'); await editorReady(page, 'writer');
        await page.goBack(); await launcherReady(page);
        await page.locator('canvas').click({ position: { x: 70, y: 120 } });
        await page.waitForFunction(() => document.body.dataset.theme === 'dark');
        await page.getByText('Preferences', { exact: true }).click();
        await page.getByLabel('Open apps in new tabs').check();
        await page.reload(); await launcherReady(page);
        assert.equal(await page.getAttribute('body', 'data-theme'), 'dark');
        assert.equal(await page.locator('#new-tab').isChecked(), true);
        await page.screenshot({ path: resolve(output, `${name}-dark.png`) });
        const popupPromise = page.waitForEvent('popup'); await page.getByRole('link', { name: 'Open Cell', exact: true }).click();
        const popup = await popupPromise; await editorReady(popup, 'cell');
        assert.equal(await popup.evaluate(() => window.opener === null), true); await popup.close();
        await page.evaluate(() => { window.open = () => null; });
        await page.getByRole('link', { name: 'Open Writer', exact: true }).click();
        await page.locator('#open-current').waitFor({ state: 'visible' });
        assert.equal(new URL(page.url()).hostname, 'demo.broiler.app');
        await page.locator('#open-current').click(); await editorReady(page, 'writer');
        result.checks.push('persisted theme/new-tab preferences; opener isolation; denied-popup current-tab fallback');
        for (const id of ['writer', 'cell']) { await page.goto(`https://${id}.broiler.app/`); await editorReady(page, id); }
        assert.deepEqual(errors, []); assert.deepEqual(unexpected, []); await context.close();
        result.checks.push('direct editor visits; no unexpected network origins or fatal console errors');

        const approved = await createContext({ viewport: { width: 390, height: 1000 } });
        await routeArtifacts(approved, { metadata: { schemaVersion: 1, status: 'approved', channel: 'preview', catalogVersion: 'office-test-1', products: [
            { id: 'writer', url: 'https://writer.broiler.app/', version: 'writer-test-8' }, { id: 'cell', url: 'https://cell.broiler.app/', version: 'cell-test-2' },
        ] } });
        const small = await approved.newPage(); await small.goto(home); await launcherReady(small);
        assert.match(await small.locator('#deployment').textContent(), /Writer writer-test-8 · Cell cell-test-2/);
        await small.screenshot({ path: resolve(output, `${name}-narrow.png`), fullPage: true });
        await canvasButton(small, 1); await editorReady(small, 'writer'); await approved.close();
        result.checks.push('narrow layout; approved fixture has independently versioned metadata');

        const storageDenied = await createContext();
        await routeArtifacts(storageDenied);
        await storageDenied.addInitScript(() => Object.defineProperty(window, 'localStorage', { get() { throw new DOMException('denied', 'SecurityError'); } }));
        const noStorage = await storageDenied.newPage(); await noStorage.goto(home); await launcherReady(noStorage);
        await noStorage.getByText('Preferences', { exact: true }).click(); await noStorage.getByLabel('Dark theme').check();
        await noStorage.waitForFunction(() => document.body.dataset.theme === 'dark'); await storageDenied.close();
        result.checks.push('storage denial retains working in-page preferences');
        for (const failure of ['entry', 'runtime', 'replay', 'after-ready']) {
            const broken = await createContext(); const options = { failure }; await routeArtifacts(broken, options);
            const bad = await broken.newPage(); await bad.goto(home);
            if (failure === 'after-ready') {
                await launcherReady(bad);
                await bad.evaluate(() => window.dispatchEvent(new ErrorEvent('error', { message: 'intentional post-ready failure' })));
            }
            await bad.locator('#error').waitFor({ state: 'visible' });
            assert.equal(await bad.getByRole('link', { name: 'Open Writer', exact: true }).getAttribute('href'), 'https://writer.broiler.app/');
            if (failure === 'entry') { options.failure = null; await bad.getByRole('button', { name: 'Retry launcher' }).click(); await launcherReady(bad); }
            else { await bad.getByRole('link', { name: 'Open Writer', exact: true }).focus(); await bad.keyboard.press('Enter'); await editorReady(bad, 'writer'); }
            await broken.close();
        }
        result.checks.push('entry/runtime/replay and post-ready failure links survive; retry recovers startup');
        const noScript = await createContext({ javaScriptEnabled: false }); await routeArtifacts(noScript);
        const basic = await noScript.newPage(); await basic.goto(home); await basic.getByRole('link', { name: 'Open Cell', exact: true }).click();
        assert.equal(basic.url(), 'https://cell.broiler.app/'); await noScript.close();
        result.checks.push('JavaScript-disabled HTML navigation remains available');
        result.pass = true;
    } catch (error) { result.pass = false; result.error = String(error); failed = true; }
    finally { for(const context of ownedContexts) await context.close().catch(()=>{}); await browser?.close().catch(()=>{}); report.runs.push(result); console.log(JSON.stringify(result)); }
}
await writeFile(resolve(output, 'report.json'), JSON.stringify(report, null, 2));
if (failed) process.exitCode = 1;
