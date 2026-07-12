import { mkdir, writeFile } from 'node:fs/promises';
import { chromium, firefox } from '@playwright/test';

const url = process.env.BROILER_PHASE4_URL ?? 'http://127.0.0.1:8769/';
const evidenceDirectory = process.env.BROILER_PHASE4_EVIDENCE ?? 'artifacts/browser-wasm-phase4/browser-evidence';

await mkdir(evidenceDirectory, { recursive: true });

for (const [name, browserType] of [['chromium', chromium], ['firefox', firefox]]) {
  const errors = [];
  const browser = await browserType.launch({ headless: true });
  try {
    const context = await browser.newContext({ viewport: { width: 1000, height: 900 } });
    try {
      await context.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: new URL(url).origin });
    } catch {
      // Firefox may not expose these Playwright permission names. The test
      // records the denied-capability path below instead of claiming support.
    }
    const page = await context.newPage();
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

    const snapshot = () => page.evaluate(() => {
      const { test, ...value } = globalThis.__broilerWasmPhase4;
      return value;
    });
    const waitForFrameAfter = frame => page.waitForFunction(
      previous => globalThis.__broilerWasmPhase4.frameIndex > previous,
      frame,
      { timeout: 120_000 });

    const initial = await snapshot();
    const semanticDom = await page.locator('#ui-semantic-layer').getByRole('button').count();
    const passwordValue = await page.locator('#ui-password-input').inputValue();
    const pageText = await page.locator('body').innerText();
    if (initial.semanticCount !== 9 || initial.semanticPrivacyLeaks !== 0 || semanticDom < 2)
      errors.push(`Semantic baseline failed: ${JSON.stringify(initial)}`);
    if (passwordValue !== '' || pageText.includes('p@ssw0rd') || JSON.stringify(initial.semanticSnapshot).includes('p@ssw0rd'))
      errors.push('Password value leaked into the DOM, diagnostics, or semantic snapshot.');

    const actionButton = page.getByRole('button', { name: 'Run action', exact: true });
    await actionButton.focus();
    let before = await snapshot();
    await page.keyboard.press('Enter');
    await waitForFrameAfter(before.frameIndex);
    const afterAction = await snapshot();
    if (afterAction.buttonClicks !== 1 || afterAction.focusedControl !== 'button')
      errors.push(`Semantic button action failed: ${JSON.stringify(afterAction)}`);

    const edit = page.getByRole('textbox', { name: 'Text editor', exact: true });
    await edit.focus();
    before = await snapshot();
    await page.keyboard.press('X');
    await waitForFrameAfter(before.frameIndex);
    const afterKeyText = await snapshot();
    if (afterKeyText.editText !== 'WASMX')
      errors.push(`Trusted key text failed: ${JSON.stringify(afterKeyText)}`);

    before = afterKeyText;
    await page.getByTestId('phase4-compose').click();
    await waitForFrameAfter(before.frameIndex);
    const afterComposition = await snapshot();
    const cjkOccurrences = afterComposition.editText.split('日本').length - 1;
    if (afterComposition.compositionCommits !== before.compositionCommits + 1 || cjkOccurrences !== 1 || afterComposition.compositionSelectionStart !== 1 || afterComposition.compositionSelectionLength !== 1)
      errors.push(`Composition did not commit exactly once with retained neutral selection diagnostics: ${JSON.stringify(afterComposition)}`);

    before = afterComposition;
    await page.getByTestId('phase4-unicode').click();
    await waitForFrameAfter(before.frameIndex);
    const afterUnicode = await snapshot();
    if (!afterUnicode.editText.includes('é🙂'))
      errors.push(`Combining/emoji committed text failed: ${JSON.stringify(afterUnicode)}`);

    const rtl = page.getByRole('textbox', { name: 'RTL text editor', exact: true });
    await rtl.focus();
    before = await snapshot();
    await page.keyboard.insertText('مرحبا');
    await waitForFrameAfter(before.frameIndex);
    const afterRtl = await snapshot();
    if (!afterRtl.rtlText.includes('مرحبا') || afterRtl.focusedControl !== 'rtl')
      errors.push(`RTL text workflow failed: ${JSON.stringify(afterRtl)}`);

    const password = page.getByRole('textbox', { name: 'Password field', exact: true });
    await password.focus();
    before = await snapshot();
    await page.keyboard.press('Control+A');
    await page.keyboard.press('Control+C');
    await waitForFrameAfter(before.frameIndex);
    const afterPasswordCopy = await snapshot();
    if (afterPasswordCopy.passwordCopyBlocks <= before.passwordCopyBlocks || afterPasswordCopy.clipboardWrites !== before.clipboardWrites || afterPasswordCopy.lastClipboardWrite.includes('p@ssw0rd'))
      errors.push(`Password copy privacy failed: ${JSON.stringify(afterPasswordCopy)}`);

    await edit.focus();
    before = await snapshot();
    await page.keyboard.press('Control+A');
    await page.keyboard.press('Control+C');
    await waitForFrameAfter(before.frameIndex);
    const afterCopy = await snapshot();
    if (afterCopy.clipboardWrites <= before.clipboardWrites || afterCopy.lastClipboardWrite !== before.editText)
      errors.push(`Trusted copy event failed: ${JSON.stringify(afterCopy)}`);

    let clipboardSeeded = false;
    try {
      await page.evaluate(() => navigator.clipboard.writeText('Pasted-日本'));
      clipboardSeeded = true;
    } catch {
      clipboardSeeded = false;
    }
    if (clipboardSeeded) {
      before = afterCopy;
      await page.keyboard.press('Control+V');
      await waitForFrameAfter(before.frameIndex);
      const afterPaste = await snapshot();
      if (afterPaste.clipboardReads <= before.clipboardReads || !afterPaste.editText.includes('Pasted-日本'))
        errors.push(`Trusted paste event failed: ${JSON.stringify(afterPaste)}`);
    } else {
      const deniedBefore = await snapshot();
      await page.getByTestId('phase4-denied-clipboard').click();
      const deniedAfter = await snapshot();
      if (deniedAfter.clipboardDenied <= deniedBefore.clipboardDenied)
        errors.push('Clipboard denial was not reported predictably.');
    }

    await edit.focus();
    before = await snapshot();
    await actionButton.focus();
    await waitForFrameAfter(before.frameIndex);
    const focusTransfer = await snapshot();
    const activeContexts = await page.locator('.ui-native-editor[data-active="true"]').count();
    if (focusTransfer.focusedControl !== 'button' || activeContexts !== 0)
      errors.push(`Text context did not clear on focus transfer: ${JSON.stringify(focusTransfer)}`);

    await actionButton.focus();
    before = await snapshot();
    await page.keyboard.press('Tab');
    await waitForFrameAfter(before.frameIndex);
    const tabFocus = await snapshot();
    if (tabFocus.focusedControl !== 'edit')
      errors.push(`DOM Tab order did not synchronize managed focus: ${JSON.stringify(tabFocus)}`);

    await page.waitForTimeout(700);
    const settled = await snapshot();
    await page.waitForTimeout(700);
    const settledAgain = await snapshot();
    if (settledAgain.frameIndex !== settled.frameIndex || settledAgain.recursiveFrames !== 0)
      errors.push(`Phase 4 scheduler failed to become idle: ${JSON.stringify(settledAgain)}`);

    await page.screenshot({ path: `${evidenceDirectory}/${name}.png`, fullPage: true });
    await page.evaluate(() => window.dispatchEvent(new PageTransitionEvent('pagehide')));
    const disposed = await snapshot();
    if (disposed.listenersInstalled !== disposed.listenersRemoved)
      errors.push(`Listener cleanup failed: ${disposed.listenersInstalled} installed, ${disposed.listenersRemoved} removed.`);

    const evidence = {
      browser: name,
      initial,
      afterAction,
      afterKeyText,
      afterComposition,
      afterUnicode,
      afterRtl,
      afterPasswordCopy,
      afterCopy,
      clipboardSeeded,
      focusTransfer,
      tabFocus,
      settledAgain,
      disposed,
      errors
    };
    await writeFile(`${evidenceDirectory}/${name}.json`, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
    console.log(JSON.stringify(evidence, null, 2));
    if (errors.length > 0)
      throw new Error(`${name} Phase 4 smoke failed:\n${errors.join('\n')}`);
  } finally {
    await browser.close();
  }
}
