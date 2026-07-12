import { mkdir, writeFile } from 'node:fs/promises';
import { chromium, firefox } from '@playwright/test';

const url = process.env.BROILER_PHASE3_URL ?? 'http://127.0.0.1:8768/';
const evidenceDirectory = process.env.BROILER_PHASE3_EVIDENCE ?? 'artifacts/browser-wasm-phase3/browser-evidence';

await mkdir(evidenceDirectory, { recursive: true });

for (const [name, browserType] of [['chromium', chromium], ['firefox', firefox]]) {
  const errors = [];
  const browser = await browserType.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1000, height: 900 } });
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
    await page.locator('#ui-status[data-state="passed"]').waitFor({ timeout: 120_000 });
    const canvas = page.locator('#ui-frame');

    const state = () => page.evaluate(() => {
      const { test, ...snapshot } = globalThis.__broilerWasmPhase3;
      return snapshot;
    });
    const waitForFrameAfter = async frame => page.waitForFunction(
      previous => globalThis.__broilerWasmPhase3.frameIndex > previous,
      frame,
      { timeout: 120_000 });
    const point = async (control, fx = 0.5, fy = 0.5) => {
      const value = await state();
      const rect = await canvas.boundingBox();
      if (!rect)
        throw new Error('Phase 3 canvas has no browser bounding box.');
      const bounds = value.bounds[control];
      return {
        x: rect.x + ((bounds.x + bounds.width * fx) / value.logicalWidth) * rect.width,
        y: rect.y + ((bounds.y + bounds.height * fy) / value.logicalHeight) * rect.height
      };
    };
    const clickControl = async (control, fx = 0.5, fy = 0.5) => {
      const before = await state();
      const target = await point(control, fx, fy);
      await page.mouse.click(target.x, target.y);
      await waitForFrameAfter(before.frameIndex);
      return state();
    };

    const initial = await state();
    await page.waitForTimeout(600);
    const idle = await state();
    if (idle.frameIndex !== initial.frameIndex)
      errors.push(`Idle UI rendered ${idle.frameIndex - initial.frameIndex} unexpected frame(s).`);

    const afterButton = await clickControl('button');
    if (afterButton.buttonClicks !== 1 || afterButton.focusedControl !== 'button')
      errors.push(`Button workflow failed: ${JSON.stringify(afterButton)}`);

    await clickControl('edit');
    const beforeText = await state();
    await page.keyboard.insertText('-Browser');
    await waitForFrameAfter(beforeText.frameIndex);
    const afterText = await state();
    if (afterText.editText !== 'WASM-Browser' || afterText.focusedControl !== 'edit' || afterText.textEvents < 1)
      errors.push(`Basic text workflow failed: ${JSON.stringify(afterText)}`);

    const sliderStart = await point('slider', 0.25);
    const sliderEnd = await point('slider', 0.8);
    const beforeSlider = await state();
    await page.mouse.move(sliderStart.x, sliderStart.y);
    await page.mouse.down();
    await page.mouse.move(sliderEnd.x, sliderEnd.y, { steps: 4 });
    await page.mouse.up();
    await waitForFrameAfter(beforeSlider.frameIndex);
    const afterSlider = await state();
    if (afterSlider.sliderValue < 70 || afterSlider.capturedControl !== '')
      errors.push(`Slider drag workflow failed: ${JSON.stringify(afterSlider)}`);

    const scrollPoint = await point('scroll', 0.4, 0.5);
    const beforeWheel = await state();
    await page.mouse.move(scrollPoint.x, scrollPoint.y);
    await page.mouse.wheel(0, 420);
    await waitForFrameAfter(beforeWheel.frameIndex);
    const afterWheel = await state();
    if (afterWheel.scrollOffset <= 0)
      errors.push(`Wheel scrolling failed: ${JSON.stringify(afterWheel)}`);

    const afterList = await clickControl('list', 0.4, 0.135);
    if (afterList.selectedItem !== 'panel' || afterList.focusedControl !== 'list')
      errors.push(`List selection failed: ${JSON.stringify(afterList)}`);

    const menuBar = await point('menu', 0.025, 0.5);
    const beforeMenu = await state();
    await page.mouse.click(menuBar.x, menuBar.y);
    await waitForFrameAfter(beforeMenu.frameIndex);
    const openedMenu = await state();
    if (!openedMenu.menuOpen || openedMenu.capturedControl !== 'menu')
      errors.push(`Menu did not open with capture: ${JSON.stringify(openedMenu)}`);
    const menuItem = await point('menu', 0.025, 1.45);
    await page.mouse.click(menuItem.x, menuItem.y);
    await waitForFrameAfter(openedMenu.frameIndex);
    const afterMenu = await state();
    if (afterMenu.menuOpen || afterMenu.menuInvocations !== 1 || afterMenu.capturedControl !== '')
      errors.push(`Menu invoke/release failed: ${JSON.stringify(afterMenu)}`);

    await clickControl('button');
    let beforeKeyboard = await state();
    await page.keyboard.press('Tab');
    await waitForFrameAfter(beforeKeyboard.frameIndex);
    let keyboardState = await state();
    if (keyboardState.focusedControl !== 'edit')
      errors.push(`Tab did not move focus to edit: ${JSON.stringify(keyboardState)}`);
    beforeKeyboard = keyboardState;
    await page.keyboard.press('Tab');
    await waitForFrameAfter(beforeKeyboard.frameIndex);
    keyboardState = await state();
    const valueBeforeKey = keyboardState.sliderValue;
    await page.keyboard.press('ArrowRight');
    await waitForFrameAfter(keyboardState.frameIndex);
    keyboardState = await state();
    if (keyboardState.focusedControl !== 'slider' || keyboardState.sliderValue <= valueBeforeKey)
      errors.push(`Keyboard slider/focus workflow failed: ${JSON.stringify(keyboardState)}`);

    const beforeProjection = keyboardState;
    await canvas.dispatchEvent('keydown', { key: 'ArrowRight', code: 'Numpad6', repeat: true, location: 3, bubbles: true });
    await waitForFrameAfter(beforeProjection.frameIndex);
    const afterProjection = await state();
    if (afterProjection.keyboardRepeatEvents <= beforeProjection.keyboardRepeatEvents || afterProjection.lastKeyboardLocation !== 3)
      errors.push(`Neutral repeat/location retention failed: ${JSON.stringify(afterProjection)}`);

    const cancelStart = await point('slider', 0.4);
    const beforeCancel = await state();
    await page.mouse.move(cancelStart.x, cancelStart.y);
    await page.mouse.down();
    await page.evaluate(() => window.dispatchEvent(new Event('blur')));
    await page.mouse.up();
    await waitForFrameAfter(beforeCancel.frameIndex);
    const afterCancel = await state();
    if (afterCancel.cancelCleanups <= beforeCancel.cancelCleanups || afterCancel.capturedControl !== '')
      errors.push(`Blur capture cleanup failed: ${JSON.stringify(afterCancel)}`);

    await page.waitForTimeout(600);
    const settled = await state();
    await page.waitForTimeout(600);
    const settledAgain = await state();
    if (settledAgain.frameIndex !== settled.frameIndex || settledAgain.recursiveFrames !== 0)
      errors.push(`Scheduler did not become idle or rendered recursively: ${JSON.stringify(settledAgain)}`);

    await page.screenshot({ path: `${evidenceDirectory}/${name}.png`, fullPage: true });
    await page.evaluate(() => window.dispatchEvent(new PageTransitionEvent('pagehide')));
    const disposed = await state();
    if (disposed.listenersInstalled !== disposed.listenersRemoved)
      errors.push(`Browser listener cleanup failed: ${disposed.listenersInstalled} installed, ${disposed.listenersRemoved} removed.`);

    const evidence = {
      browser: name,
      initial,
      afterButton,
      afterText,
      afterSlider,
      afterWheel,
      afterList,
      afterMenu,
      afterProjection,
      afterCancel,
      settledAgain,
      disposed,
      errors
    };
    await writeFile(`${evidenceDirectory}/${name}.json`, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
    console.log(JSON.stringify(evidence, null, 2));
    if (errors.length > 0)
      throw new Error(`${name} Phase 3 smoke failed:\n${errors.join('\n')}`);
  } finally {
    await browser.close();
  }
}
