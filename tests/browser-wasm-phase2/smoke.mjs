import { mkdir, writeFile } from 'node:fs/promises';
import { chromium, firefox } from '@playwright/test';

const url = process.env.BROILER_PHASE2_URL ?? 'http://127.0.0.1:8767/';
const evidenceDirectory = process.env.BROILER_PHASE2_EVIDENCE ?? 'artifacts/browser-wasm-phase2/browser-evidence';
const expectedChecksum = '0724aed9b9f5f7dab4b52780bb718965359a5da7e0ecd7881b15c6ccaf901394';

await mkdir(evidenceDirectory, { recursive: true });

for (const [name, browserType] of [['chromium', chromium], ['firefox', firefox]]) {
  const errors = [];
  const browser = await browserType.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 960 } });
    page.on('console', message => {
      if (['error', 'warning', 'warn'].includes(message.type()))
        errors.push(`console ${message.type()}: ${message.text()}`);
    });
    page.on('pageerror', error => errors.push(`pageerror: ${error.stack ?? error.message}`));
    page.on('response', response => {
      if (response.status() >= 400)
        errors.push(`HTTP ${response.status()}: ${response.url()}`);
    });

    await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 120_000 });
    await page.locator('#status[data-state="passed"]').waitFor({ timeout: 120_000 });

    const resize = async (width, height, dpr) => page.evaluate(
      ([w, h, scale]) => {
        globalThis.__broilerWasmPhase2.test.resize(w, h, scale);
        return { ...globalThis.__broilerWasmPhase2, test: undefined };
      },
      [width, height, dpr]);

    const dprResults = [];
    for (const dpr of [1, 1.25, 1.5, 2]) {
      const result = await resize(320, 180, dpr);
      const expectedWidth = Math.ceil(320 * dpr);
      const expectedHeight = Math.ceil(180 * dpr);
      if (result.presenterStatus !== 'presented' || result.backingWidth !== expectedWidth || result.backingHeight !== expectedHeight)
        errors.push(`DPR ${dpr} produced ${result.backingWidth}x${result.backingHeight}, expected ${expectedWidth}x${expectedHeight}.`);
      dprResults.push({ dpr, backingWidth: result.backingWidth, backingHeight: result.backingHeight, oldBuffersAlive: result.oldBuffersAlive });
    }

    const lastValidBeforeLimits = await resize(640, 360, 1);
    const suspended = await resize(0, 0, 1);
    if (suspended.presenterStatus !== 'suspended')
      errors.push(`Zero-sized resize did not suspend: ${suspended.presenterStatus}`);
    if (suspended.lastValidFrame?.backingWidth !== lastValidBeforeLimits.backingWidth)
      errors.push('Zero-sized resize discarded the last valid surface.');

    const rejected = await resize(100000, 100000, 1);
    if (rejected.presenterStatus !== 'rejected')
      errors.push(`Oversized resize was not rejected: ${rejected.presenterStatus}`);
    if (rejected.lastValidFrame?.backingWidth !== lastValidBeforeLimits.backingWidth)
      errors.push('Rejected resize discarded the last valid surface.');

    const sequence = await page.evaluate(() => {
      globalThis.__broilerWasmPhase2.test.resizeSequence();
      // A separate same-size frame gives the AOT JIT no resize-local reference
      // to the last replaced bitmap before the forced retention collection.
      globalThis.__broilerWasmPhase2.test.renderCurrent();
      return { ...globalThis.__broilerWasmPhase2, test: undefined };
    });
    if (sequence.oldBuffersAlive !== 0)
      errors.push(`Repeated resize retained ${sequence.oldBuffersAlive} old bitmap(s).`);

    await resize(1280, 720, 1);
    const warmFrames = [];
    for (let index = 0; index < 7; index++) {
      const frame = await page.evaluate(() => {
        globalThis.__broilerWasmPhase2.test.renderCurrent();
        const value = globalThis.__broilerWasmPhase2;
        return {
          renderMs: value.renderMs,
          previousInteropMs: value.previousInteropMs,
          jsPresentMs: value.jsPresentMs,
          managedAllocatedBytes: value.managedAllocatedBytes,
          imageDataReused: value.imageDataReused,
          oldBuffersAlive: value.oldBuffersAlive
        };
      });
      warmFrames.push(frame);
    }

    const final = await page.evaluate(() => ({
      phase1: globalThis.__broilerWasmPhase1,
      phase2: { ...globalThis.__broilerWasmPhase2, test: undefined },
      canvas: {
        width: document.querySelector('#frame')?.width,
        height: document.querySelector('#frame')?.height,
        cssWidth: document.querySelector('#frame')?.style.width,
        cssHeight: document.querySelector('#frame')?.style.height
      },
      status: document.querySelector('#status')?.textContent,
      dynamicTests: document.querySelector('#dynamic-tests')?.textContent
    }));

    if (!final.phase1?.passed || final.phase1.rgbaSha256 !== expectedChecksum)
      errors.push('Phase 1 checksum oracle regressed.');
    if (!final.phase2?.passed || final.status !== 'Phase 2 presenter passed')
      errors.push(`Phase 2 final status failed: ${final.status}`);
    if (final.dynamicTests !== '3/3')
      errors.push(`Unexpected dynamic check count: ${final.dynamicTests}`);
    if (!warmFrames.every(frame => frame.imageDataReused && frame.oldBuffersAlive === 0))
      errors.push('Warm frames did not reuse ImageData or retained old managed buffers.');

    await page.screenshot({ path: `${evidenceDirectory}/${name}.png`, fullPage: true });
    const disposed = await page.evaluate(() => {
      globalThis.__broilerWasmPhase2.test.dispose();
      return {
        presenterStatus: globalThis.__broilerWasmPhase2.presenterStatus,
        oldBuffersAlive: globalThis.__broilerWasmPhase2.oldBuffersAlive
      };
    });
    if (disposed.presenterStatus !== 'disposed' || disposed.oldBuffersAlive !== 0)
      errors.push(`Dispose cleanup failed: ${JSON.stringify(disposed)}`);

    const evidence = { browser: name, dprResults, suspended, rejected, sequence, warmFrames, final, disposed, errors };
    await writeFile(`${evidenceDirectory}/${name}.json`, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
    console.log(JSON.stringify(evidence, null, 2));
    if (errors.length > 0)
      throw new Error(`${name} Phase 2 smoke failed:\n${errors.join('\n')}`);
  } finally {
    await browser.close();
  }
}
