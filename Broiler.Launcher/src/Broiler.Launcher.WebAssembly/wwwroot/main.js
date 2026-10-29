import { dotnet } from './_framework/dotnet.js';
export const officeInputProtocolVersion = 1;
import { createCanvasHost, hostProtocolVersion } from './broiler.hosting.webassembly.js';

const allowed = new Set(['https://writer.broiler.app/', 'https://cell.broiler.app/']);
const memory = new Map();
let managed, host, metadata = '{}', ready = false;
function fail(error) {
    ready = false;
    try { host?.dispose(); } catch { /* The runtime may already be unavailable. */ }
    host = null;
    console.error(error); window.dispatchEvent(new Event('broiler-failed'));
}
function preference(key) {
    if (memory.has(key)) return memory.get(key);
    try { return localStorage.getItem(key) ?? ''; }
    catch { return ''; }
}
function writePreference(key, value) {
    memory.set(key, value);
    try { localStorage.setItem(key, value); } catch { /* Preferences still apply for this page. */ }
    document.getElementById('new-tab').checked = preference('office.web.openNewTab') === 'true';
}
function navigate(url, newTab) {
    if (!allowed.has(url)) throw new Error('Unsupported Office destination.');
    const fallback = document.getElementById('open-current'); fallback.href = url;
    if (!newTab) { location.assign(url); return; }
    // Avoid exposing window.opener. A denied popup leaves an ordinary same-tab link.
    const opened = window.open('about:blank', '_blank');
    if (opened) { opened.opener = null; opened.location.replace(url); fallback.hidden = true; }
    else {
        fallback.hidden = false;
        document.getElementById('launch-status').textContent = 'The new tab was blocked. Open the app using the current-tab link.';
        fallback.focus();
    }
}
for (const link of document.querySelectorAll('[data-product]')) link.addEventListener('click', event => {
    if (!ready || document.body.dataset.failed === 'true' || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault(); managed.Launch(link.dataset.product);
});
document.getElementById('dark-theme').addEventListener('change', event => { if (ready) managed.SetTheme(event.target.checked); });
document.getElementById('new-tab').addEventListener('change', event => { if (ready) managed.SetNewTab(event.target.checked); });
try {
    if (hostProtocolVersion !== 1) throw new Error('Browser host protocol mismatch.');
    const metadataAbort = new AbortController();
    const metadataTimeout = setTimeout(() => metadataAbort.abort(), 3000);
    try {
        const response = await fetch(/^\/(?:demo\/)?releases\//.test(location.pathname) ? '/office-web.json' : 'office-web.json', { cache: 'no-cache', credentials: 'omit', signal: metadataAbort.signal });
        if (response.ok) {
            const text = await response.text();
            if (text.length <= 65536) {
                const catalog = JSON.parse(text);
                const products = catalog.products;
                if (catalog.schemaVersion === 1 && catalog.status === 'approved' && Array.isArray(products) && products.length === 2 &&
                    ['writer', 'cell'].every(id => products.filter(p => p?.id === id && typeof p.version === 'string' && /^[A-Za-z0-9.+-]{1,80}$/.test(p.version) &&
                        (p.url === `https://${id}.broiler.app/` || p.url === `https://${id}.broiler.app/releases/${p.version}/` || p.url === `https://broiler-platform.github.io/${id}/releases/${p.version}/`)).length === 1)) {
                    metadata = text;
                    for (const p of products) {
                        allowed.add(p.url);
                        document.querySelector(`[data-product="${p.id}"]`).href = p.url;
                    }
                }
            }
        }
    } catch { /* Missing deployment metadata is a local preview, never a readiness probe. */ }
    finally { clearTimeout(metadataTimeout); }
    const { setModuleImports, getAssemblyExports, runMain } = await dotnet.create();
    managed = (await getAssemblyExports('Broiler.Launcher.WebAssembly')).Broiler.Launcher.WebAssembly.LauncherExports;
    setModuleImports('main.js', { launcher: {
        invalidate: () => host?.invalidate(),
        ready: summary => {
            host = createCanvasHost({
                canvas: document.getElementById('launcher-canvas'), input: document.getElementById('launcher-input'), container: document.getElementById('launcher-host'),
                consumedChords: ['1', '2', '3', '4'],
                render: () => { try { managed.Render(); } catch (error) { fail(error); } },
                dispatch: {
                    resize: value => managed.Resize(value.width, value.height, value.dpr),
                    pointer: value => managed.Pointer(value.kind, value.x, value.y, value.buttons, value.button, value.time),
                    pointerCancel: () => managed.CancelPointer(),
                    key: value => {
                        if (value.key === 'Escape' && value.down) { document.querySelector('[data-product]')?.focus(); return; }
                        managed.Key(value.key, value.down, (value.shift ? 1 : 0) | (value.ctrl ? 2 : 0) | (value.alt ? 4 : 0) | (value.meta ? 8 : 0), value.repeat, value.time);
                    },
                },
            });
            ready = true; document.body.dataset.ready = 'true';
            document.getElementById('deployment').textContent = summary;
            document.getElementById('launch-status').textContent = 'Choose an app to begin.';
            document.getElementById('new-tab').checked = preference('office.web.openNewTab') === 'true';
            window.dispatchEvent(new Event('broiler-ready')); host.invalidate();
        },
        failed: fail, navigate, readPreference: preference, writePreference,
        applyTheme: theme => { document.body.dataset.theme = theme; document.getElementById('dark-theme').checked = theme === 'dark'; },
        metadata: () => metadata,
    } });
    window.addEventListener('pagehide', () => { ready = false; host?.dispose(); managed.Dispose(); }, { once: true });
    window.addEventListener('pageshow', event => { if (event.persisted) location.reload(); });
    runMain().catch(fail);
} catch (error) { fail(error); }
