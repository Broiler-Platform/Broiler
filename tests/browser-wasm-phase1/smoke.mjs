import { mkdir } from 'node:fs/promises';
import { chromium, firefox } from '@playwright/test';

const url = process.env.BROILER_PHASE1_URL ?? 'http://127.0.0.1:8766/';
const evidenceDirectory = process.env.BROILER_PHASE1_EVIDENCE ?? 'artifacts/browser-wasm-phase1/browser-evidence';
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
    await page.locator('#phase1-status[data-state="passed"]').waitFor({ timeout: 120_000 });

    const evidence = await page.evaluate(() => ({
      state: globalThis.__broilerWasmPhase1,
      status: document.querySelector('#phase1-status')?.textContent,
      tests: document.querySelector('#phase1-tests')?.textContent,
      checksum: document.querySelector('#phase1-checksum')?.textContent,
      font: document.querySelector('#font')?.textContent
    }));

    if (evidence.status !== 'Phase 1 proof passed')
      errors.push(`Unexpected status: ${evidence.status}`);
    if (evidence.tests !== '8/8')
      errors.push(`Unexpected managed-test count: ${evidence.tests}`);
    if (evidence.checksum !== expectedChecksum)
      errors.push(`Checksum mismatch: ${evidence.checksum}`);
    if (!evidence.state?.ready || !evidence.state?.passed)
      errors.push('Global ready marker did not report a passing proof.');

    await page.screenshot({ path: `${evidenceDirectory}/${name}.png`, fullPage: true });
    console.log(JSON.stringify({ browser: name, evidence, errors }, null, 2));
    if (errors.length > 0)
      throw new Error(`${name} Phase 1 smoke failed:\n${errors.join('\n')}`);
  } finally {
    await browser.close();
  }
}
