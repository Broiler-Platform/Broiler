import { dotnet } from './_framework/dotnet.js';

const moduleStartedAt = performance.now();
globalThis.__broilerWasmPhase0 = {
    ready: false,
    moduleStartedAt,
    readyAt: null,
    startupMs: null,
    runtimeVersion: null,
    error: null
};

const { setModuleImports, runMain } = await dotnet.create();

setModuleImports('main.js', {
    baseline: {
        markReady: runtimeVersion => {
            const readyAt = performance.now();
            const state = globalThis.__broilerWasmPhase0;
            state.ready = true;
            state.readyAt = readyAt;
            state.startupMs = readyAt - moduleStartedAt;
            state.runtimeVersion = runtimeVersion;

            document.getElementById('status').textContent = 'Ready';
            document.getElementById('runtime').textContent = runtimeVersion;
            document.getElementById('startup').textContent = `${state.startupMs.toFixed(3)} ms`;
            globalThis.dispatchEvent(new CustomEvent('broiler-wasm-phase0-ready', { detail: state }));
        }
    }
});

runMain().catch(error => {
    const message = error instanceof Error ? error.stack ?? error.message : String(error);
    globalThis.__broilerWasmPhase0.error = message;
    document.getElementById('status').textContent = 'Failed: ' + message;
    console.error(error);
});
