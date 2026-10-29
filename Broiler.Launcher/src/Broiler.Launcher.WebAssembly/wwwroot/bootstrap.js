// Deliberately independent of WASM and module imports: links and retry survive a broken runtime.
(() => {
    const fail = () => {
        document.body.dataset.failed = 'true';
        document.getElementById('error').hidden = false;
        document.getElementById('retry').hidden = false;
        document.getElementById('launcher-host').hidden = true;
        document.getElementById('launch-status').textContent = 'Office app links are available below.';
    };
    document.getElementById('retry').addEventListener('click', () => location.reload());
    window.addEventListener('error', event => { if (event.message || event.target?.tagName === 'SCRIPT') fail(); }, true);
    window.addEventListener('unhandledrejection', fail);
    window.addEventListener('broiler-failed', fail);
    const timeout = setTimeout(fail, 60000);
    window.addEventListener('broiler-ready', () => clearTimeout(timeout), { once: true });
})();
