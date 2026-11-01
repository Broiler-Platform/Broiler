import { chromium } from 'playwright';
import assert from 'node:assert/strict';

const browser = await chromium.launch();
try {
    const page = await browser.newPage();
    await page.goto(process.env.WRITER_URL || 'http://localhost:37472/');
    await page.waitForFunction(() => document.getElementById('writer-status')?.hidden);
    const canvas = page.locator('canvas');
    const zoom = () => page.locator('#writer-status').textContent();
    await canvas.dispatchEvent('keydown', { key: 'Control', code: 'ControlLeft', ctrlKey: true });
    await page.evaluate(() => window.dispatchEvent(new Event('blur')));
    const wheel = { clientX: 200, clientY: 200, deltaY: -100, deltaMode: 0 };
    await canvas.dispatchEvent('wheel', { ...wheel, ctrlKey: false });
    await page.waitForTimeout(150);
    assert.match(await zoom(), /100%/);
    await canvas.dispatchEvent('wheel', { ...wheel, ctrlKey: true });
    await page.waitForFunction(() => !document.getElementById('writer-status').textContent.includes('100%'));
    const after = await zoom();
    await canvas.dispatchEvent('wheel', { ...wheel, ctrlKey: false });
    await page.waitForTimeout(150);
    assert.equal(await zoom(), after);
    console.log('Wheel zoom follows event modifiers after lost key-up and focus changes.');
} finally {
    await browser.close();
}
