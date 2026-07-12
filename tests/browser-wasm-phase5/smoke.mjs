import { mkdir, writeFile } from 'node:fs/promises';
import { chromium, firefox } from '@playwright/test';

// Phase 5 smoke: drive the direct-Canvas 2D backend (Broiler.Graphics.WebAssembly)
// through the sample's Phase 5 section and assert it planned + replayed a real frame.
const url = process.env.BROILER_PHASE5_URL ?? 'http://127.0.0.1:8770/';
const evidenceDirectory = process.env.BROILER_PHASE5_EVIDENCE ?? 'artifacts/browser-wasm-phase5/browser-evidence';

await mkdir(evidenceDirectory, { recursive: true });

for (const [name, browserType] of [['chromium', chromium], ['firefox', firefox]]) {
  const errors = [];
  const browser = await browserType.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1000, height: 1000 } });
    page.on('console', message => {
      if (message.type() === 'error')
        errors.push(`console error: ${message.text()}`);
    });
    page.on('pageerror', error => errors.push(`pageerror: ${error.stack ?? error.message}`));
    page.on('response', response => {
      if (response.status() >= 400)
        errors.push(`HTTP ${response.status()}: ${response.url()}`);
    });

    await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 120_000 });
    await page.locator('#phase5-status[data-state="passed"]').waitFor({ timeout: 120_000 });

    const backendModuleLoaded = await page.evaluate(() =>
      Array.from(document.scripts).length >= 0 && typeof globalThis.__broilerGraphicsWasm === 'object');
    if (!backendModuleLoaded)
      errors.push('Backend replay module did not register its diagnostics object.');

    const snapshot = () => page.evaluate(() => ({
      managed: globalThis.__broilerWasmPhase5,
      backend: globalThis.__broilerGraphicsWasm,
    }));

    const initial = await snapshot();
    // The managed planner reported a real, non-fallback frame.
    if (!initial.managed.ready || initial.managed.error)
      errors.push(`Phase 5 managed state not ready: ${JSON.stringify(initial.managed)}`);
    if (initial.managed.opCount <= 0 || initial.managed.streamLength <= 0)
      errors.push(`Empty replay stream: ${JSON.stringify(initial.managed)}`);
    if (initial.managed.usedFallback)
      errors.push('Direct-Canvas frame unexpectedly used the CPU fallback.');
    // The backend module replayed and holds the one uploaded image resource.
    if (!initial.backend || initial.backend.frames < 1 || initial.backend.resourceCount !== 1 || initial.backend.lastError)
      errors.push(`Backend replay diagnostics unexpected: ${JSON.stringify(initial.backend)}`);
    if (initial.backend && initial.backend.lastOpCount !== initial.managed.opCount)
      errors.push(`Op-count mismatch: managed ${initial.managed.opCount} vs backend ${initial.backend.lastOpCount}.`);

    // The canvas actually painted: the navy header band is not the light clear color.
    const headerPixel = await page.evaluate(() => {
      const canvas = document.querySelector('#phase5-canvas');
      const ctx = canvas.getContext('2d');
      const data = ctx.getImageData(Math.floor(canvas.width * 0.5), Math.floor(canvas.height * 0.13), 1, 1).data;
      return { r: data[0], g: data[1], b: data[2], a: data[3] };
    });
    if (headerPixel.r > 120 || headerPixel.a !== 255)
      errors.push(`Header band did not render an opaque dark fill: ${JSON.stringify(headerPixel)}`);

    // Re-render advances the frame counter through the batched path.
    const framesBefore = initial.backend.frames;
    await page.locator('#phase5-rerender').click();
    await page.waitForFunction(
      previous => globalThis.__broilerGraphicsWasm.frames > previous,
      framesBefore,
      { timeout: 30_000 });
    const afterRerender = await snapshot();
    if (afterRerender.managed.usedFallback || afterRerender.managed.error)
      errors.push(`Re-render failed: ${JSON.stringify(afterRerender.managed)}`);

    await page.screenshot({ path: `${evidenceDirectory}/${name}.png`, fullPage: true });

    const evidence = { browser: name, initial, afterRerender, headerPixel, errors };
    await writeFile(`${evidenceDirectory}/${name}.json`, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
    console.log(JSON.stringify(evidence, null, 2));
    if (errors.length > 0)
      throw new Error(`${name} Phase 5 smoke failed:\n${errors.join('\n')}`);
  } finally {
    await browser.close();
  }
}
