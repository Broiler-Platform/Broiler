import { dotnet } from './_framework/dotnet.js';
import { createCanvasHost, createDocumentPort } from './broiler.hosting.webassembly.js';
const { setModuleImports, getAssemblyExports, runMain } = await dotnet.create();
const managed = (await getAssemblyExports('ContractSample')).Broiler.Hosting.ContractSample.Exports;
const canvas = document.querySelector('canvas'), input = document.querySelector('textarea');
window.events = [];
window.host = createCanvasHost({ canvas, input, container: document.querySelector('#host'),
    render() {
        managed.Frame();
        const state = managed.Snapshot(); document.querySelector('#state').textContent = state;
        const context = canvas.getContext('2d'); context.clearRect(0, 0, canvas.width, canvas.height);
        context.fillStyle = '#111'; context.font = '16px sans-serif'; context.fillText(state.split('|')[0], 8, 30);
        window.host.publishCaret({ active: true, x: 10, y: 35, width: 1, height: 20 });
    },
    dispatch: {
        text: value => managed.Text(value.text),
        composition: value => managed.Composition(value.phase, value.text),
        pointer: value => window.events.push(value),
        pointerCancel: () => managed.CancelPointer(),
        key: value => window.events.push(value),
        resize: value => {
            window.lastResize = value;
            canvas.width = Math.round(value.width * value.dpr); canvas.height = Math.round(value.height * value.dpr);
        },
        clipboard: value => { window.events.push({ clipboard: value.kind }); return { handled: true, text: managed.Copy(value.kind, value.text || '') }; },
    },
});
window.documents = createDocumentPort();
document.querySelector('#open').onclick = async () => { window.managedOpenResult = await managed.OpenDocument(); };
document.querySelector('#export').onclick = async () => { window.managedExportResult = await managed.ExportDocument(); };
setModuleImports('main.js', { sample: {
    ready() { document.body.dataset.ready = 'true'; window.host.invalidate(); },
    invalidate() { window.host.invalidate(); },
    async openDocument(accept, maximumBytes) {
        const result = window.openResult = await window.documents.open({ accept, maximumBytes });
        if (result.status !== 'Opened') return result.status;
        const document = result.document;
        const base64 = btoa(String.fromCharCode(...document.bytes)); // Sample is bounded to 64 bytes.
        return [result.status, document.name, document.contentType, base64].join('\0');
    },
    async exportDocument(name, contentType, base64) {
        const result = window.exportResult = await window.documents.export({ name, contentType, bytes: Uint8Array.from(atob(base64), character => character.charCodeAt(0)) });
        return result.status;
    },
} });
runMain().catch(error => { document.body.dataset.failed = String(error); console.error(error); });
