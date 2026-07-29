'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const {
    DEFAULT_RENDER_TIMEOUT,
    StepTimeoutError,
    closeQuietly,
    contentTypeForExtension,
    createBrowserContextOptions,
    discoverTests,
    isBrowserClosedError,
    isNonTestFile,
    isWptHarnessScript,
    loadRerunTestPaths,
    parsePositiveIntegerEnv,
    renderTestWithWatchdog,
    requiresJavaScript,
    resolveRenderTimeoutMs,
    resolveRootRelativeResource,
    shardIndexForPath,
    waitForReftestReady,
    withTimeout,
} = require('../generate-wpt-references.js');

/** A promise that never settles — stands in for a wedged renderer. */
function never() {
    return new Promise(() => {});
}


test('regular reference generation enables Chromium JavaScript', () => {
    assert.equal(createBrowserContextOptions(false).javaScriptEnabled, true);
    assert.equal(createBrowserContextOptions(true).javaScriptEnabled, false);
});

test('non-JS policy matches Broiler.HTML candidate rules', () => {
    assert.equal(requiresJavaScript('<script></script>'), true);
    assert.equal(requiresJavaScript('<body onload="ready()">'), true);
    assert.equal(requiresJavaScript('<a href="javascript:ready()">'), true);
    assert.equal(requiresJavaScript('<link href="/resources/testdriver.js">'), true);
    assert.equal(requiresJavaScript('<html class="reftest-wait">'), true);
    assert.equal(requiresJavaScript('<html><body>static</body></html>'), false);
});

test('discovery includes xht and excludes WPT references and resources', () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'broiler-wpt-discovery-'));
    try {
        fs.mkdirSync(path.join(root, 'css', 'resources'), { recursive: true });
        fs.mkdirSync(path.join(root, 'css', 'reference'), { recursive: true });
        fs.writeFileSync(path.join(root, 'css', 'visual.html'), '<html></html>');
        fs.writeFileSync(path.join(root, 'css', 'visual.xht'), '<html></html>');
        fs.writeFileSync(path.join(root, 'css', 'visual-ref.html'), '<html></html>');
        fs.writeFileSync(path.join(root, 'css', 'fixture.src.xhtml'), '<html></html>');
        fs.writeFileSync(path.join(root, 'css', 'resources', 'fixture.html'), '<html></html>');
        fs.writeFileSync(path.join(root, 'css', 'reference', 'expected.html'), '<html></html>');

        const relativePaths = discoverTests(root)
            .map((file) => path.relative(root, file).replaceAll(path.sep, '/'))
            .sort();

        assert.deepEqual(relativePaths, ['css/visual.html', 'css/visual.xht']);
        assert.equal(isNonTestFile('visual-notref.xht'), true);
    } finally {
        fs.rmSync(root, { recursive: true, force: true });
    }
});

test('shard hash stays in lock-step with the C# runner', () => {
    assert.equal(shardIndexForPath('css/CSS2/foo.html', 8), 0x418AB4DC % 8);
});

test('root-relative resources resolve against the WPT root, like a real server', () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'broiler-wpt-serve-'));
    try {
        fs.mkdirSync(path.join(root, 'css', 'support'), { recursive: true });
        fs.mkdirSync(path.join(root, 'css', 'css-grid'), { recursive: true });
        fs.mkdirSync(path.join(root, 'fonts'), { recursive: true });
        const gridCss = path.join(root, 'css', 'support', 'grid.css');
        const ahem = path.join(root, 'fonts', 'ahem.css');
        const spacedFont = path.join(root, 'fonts', 'ahem space.css');
        const testDoc = path.join(root, 'css', 'css-grid', 'test.html');
        const relResource = path.join(root, 'css', 'css-grid', 'ref.png');
        fs.writeFileSync(gridCss, '.grid{display:grid}');
        fs.writeFileSync(ahem, '@font-face{}');
        fs.writeFileSync(spacedFont, '@font-face{}');
        fs.writeFileSync(testDoc, '<html></html>');
        fs.writeFileSync(relResource, 'x');

        // Root-relative support stylesheet / font → remapped under the WPT root.
        assert.equal(
            resolveRootRelativeResource(root, 'file:///css/support/grid.css'),
            gridCss);
        assert.equal(
            resolveRootRelativeResource(root, 'file:///fonts/ahem.css?v=2'),
            ahem);
        assert.equal(
            resolveRootRelativeResource(root, 'file:///fonts/ahem%20space.css'),
            spacedFont);

        // The test document and any path that resolves on disk as-is → null,
        // so Chromium loads it directly rather than being re-served.
        assert.equal(resolveRootRelativeResource(root, 'file://' + testDoc), null);
        assert.equal(resolveRootRelativeResource(root, 'file://' + relResource), null);

        // Missing resources and ../ escapes → null (Chromium 404s them).
        assert.equal(resolveRootRelativeResource(root, 'file:///css/support/missing.css'), null);
        assert.equal(resolveRootRelativeResource(root, 'file:///../../etc/passwd'), null);
        assert.equal(resolveRootRelativeResource(root, 'file:///css/support/%zz.css'), null);
        assert.equal(resolveRootRelativeResource(root, 'file:///css/support/%FF.css'), null);

        // Non-file schemes are never intercepted.
        assert.equal(resolveRootRelativeResource(root, 'https://example.test/x.css'), null);
    } finally {
        fs.rmSync(root, { recursive: true, force: true });
    }
});

test('WPT harness scripts are never served, matching the runner stubs', () => {
    assert.equal(isWptHarnessScript('/resources/testharness.js'), true);
    assert.equal(isWptHarnessScript('/resources/testharnessreport.js'), true);
    assert.equal(isWptHarnessScript('/resources/check-layout-th.js'), true);
    assert.equal(isWptHarnessScript('/css/support/grid.css'), false);
    assert.equal(isWptHarnessScript('/fonts/ahem.css'), false);
    assert.equal(isWptHarnessScript('/resources/testdriver.js'), false);
});

test('the reference generator refuses to serve harness scripts', () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'broiler-wpt-harness-'));
    try {
        fs.mkdirSync(path.join(root, 'resources'), { recursive: true });
        const harness = path.join(root, 'resources', 'testharness.js');
        const checkLayout = path.join(root, 'resources', 'check-layout-th.js');
        fs.writeFileSync(harness, 'window.test = function(){};');
        fs.writeFileSync(checkLayout, 'window.checkLayout = function(){};');

        // The harness scripts exist under the WPT root but must NOT be served:
        // Broiler.Wpt stubs them, so its render has no results table. Serving
        // them would make Chromium render the table and the test fail spuriously.
        assert.equal(
            resolveRootRelativeResource(root, 'file:///resources/testharness.js'), null);
        assert.equal(
            resolveRootRelativeResource(root, 'file:///resources/check-layout-th.js?v=1'), null);
    } finally {
        fs.rmSync(root, { recursive: true, force: true });
    }
});

test('content type is inferred from the file extension', () => {
    assert.equal(contentTypeForExtension('.css'), 'text/css; charset=utf-8');
    assert.equal(contentTypeForExtension('.js'), 'text/javascript; charset=utf-8');
    assert.equal(contentTypeForExtension('.ttf'), 'font/truetype');
    assert.equal(contentTypeForExtension('.png'), 'image/png');
    assert.equal(contentTypeForExtension('.unknown'), 'application/octet-stream');
});

test('browser-closed Playwright errors are detected for Chromium relaunch', () => {
    assert.equal(
        isBrowserClosedError(new Error('browser.newContext: Target page, context or browser has been closed')),
        true);
    assert.equal(
        isBrowserClosedError(new Error('Browser disconnected while creating a context')),
        true);
    assert.equal(
        isBrowserClosedError(new Error('page.goto: Timeout 10000ms exceeded')),
        false);
});

test('positive integer env parsing falls back for invalid values', () => {
    const name = 'BROILER_TEST_POSITIVE_INTEGER';
    const original = process.env[name];

    try {
        delete process.env[name];
        assert.equal(parsePositiveIntegerEnv(name, 3), 3);

        process.env[name] = '7';
        assert.equal(parsePositiveIntegerEnv(name, 3), 7);

        process.env[name] = '0';
        assert.equal(parsePositiveIntegerEnv(name, 3), 3);

        process.env[name] = 'not-a-number';
        assert.equal(parsePositiveIntegerEnv(name, 3), 3);

        process.env[name] = '7x';
        assert.equal(parsePositiveIntegerEnv(name, 3), 3);
    } finally {
        if (original === undefined) {
            delete process.env[name];
        } else {
            process.env[name] = original;
        }
    }
});

test('loadRerunTestPaths collects manifest relative paths, testPath fallback', () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'broiler-wpt-rerun-'));
    try {
        const manifest = path.join(dir, 'failed-tests.json');
        fs.writeFileSync(manifest, JSON.stringify({
            results: [
                { relativeTestPath: 'css/a/one.html', passed: false, skipped: false },
                { relativeTestPath: 'html/b/two.xht', passed: false, skipped: false },
                { testPath: 'css/c/three.html' },        // fallback when no relativeTestPath
                { relativeTestPath: '', passed: false },  // ignored: empty
                'not-an-object',                          // ignored: not a dict
            ],
        }));

        const paths = loadRerunTestPaths(manifest);
        assert.deepEqual(
            [...paths].sort(),
            ['css/a/one.html', 'css/c/three.html', 'html/b/two.xht'],
        );
    } finally {
        fs.rmSync(dir, { recursive: true, force: true });
    }
});

test('loadRerunTestPaths rejects a manifest without a results array', () => {
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'broiler-wpt-rerun-bad-'));
    try {
        const manifest = path.join(dir, 'bad.json');
        fs.writeFileSync(manifest, JSON.stringify({ summary: {} }));
        assert.throws(() => loadRerunTestPaths(manifest), /no top-level 'results' array/);
    } finally {
        fs.rmSync(dir, { recursive: true, force: true });
    }
});

// ---------------------------------------------------------------------------
// Hang containment
//
// A WPT test that wedges its renderer main thread after `load` (an infinite loop
// started from a timer, a runaway rAF chain) used to park a worker forever on the
// unbounded page.evaluate in waitForReftestReady, so Promise.all(workers) never
// settled and the shard sat silent until the CI job timed out hours later. Every
// await that can reach the renderer is now bounded.
// ---------------------------------------------------------------------------

test('withTimeout rejects an operation that overruns its budget', async () => {
    await assert.rejects(
        () => withTimeout(never, 10, 'wedged step'),
        (err) => err instanceof StepTimeoutError && /wedged step exceeded 10ms/.test(err.message),
    );
});

test('withTimeout passes through a result and a rejection inside budget', async () => {
    assert.equal(await withTimeout(async () => 'ok', 5_000, 'fast step'), 'ok');
    await assert.rejects(
        () => withTimeout(async () => { throw new Error('boom'); }, 5_000, 'failing step'),
        /boom/,
    );
    // A synchronous throw in the thunk is reported as a rejection, not thrown at
    // the call site (which would escape the worker's try/catch).
    await assert.rejects(
        () => withTimeout(() => { throw new Error('sync boom'); }, 5_000, 'throwing step'),
        /sync boom/,
    );
});

test('withTimeout does not leave an abandoned operation as an unhandled rejection', async () => {
    const unhandled = [];
    const record = (reason) => unhandled.push(reason);
    process.on('unhandledRejection', record);
    try {
        let rejectLate;
        const late = new Promise((_, reject) => { rejectLate = reject; });
        await assert.rejects(() => withTimeout(() => late, 10, 'late step'), StepTimeoutError);
        rejectLate(new Error('renderer died after the watchdog gave up'));
        // Two macrotask turns: long enough for Node to report an unhandled rejection.
        await new Promise((resolve) => setTimeout(resolve, 50));
    } finally {
        process.off('unhandledRejection', record);
    }
    assert.deepEqual(unhandled, []);
});

test('waitForReftestReady gives up on a renderer that never answers the probe', async () => {
    // page.evaluate has no timeout of its own: unbounded, this returns never.
    const page = { evaluate: never, waitForFunction: never };
    const started = Date.now();
    await waitForReftestReady(page, 20);
    assert.ok(Date.now() - started < 5_000, 'probe must be bounded, not left pending');
});

test('waitForReftestReady still waits for a page that signals ready', async () => {
    const calls = [];
    const page = {
        evaluate: async () => { calls.push('probe'); return true; },
        waitForFunction: async () => { calls.push('wait'); },
    };
    await waitForReftestReady(page, 5_000);
    assert.deepEqual(calls, ['probe', 'wait']);
});

test('waitForReftestReady skips the signal wait for a page that is not reftest-wait', async () => {
    const calls = [];
    const page = {
        evaluate: async () => { calls.push('probe'); return false; },
        waitForFunction: async () => { calls.push('wait'); },
    };
    await waitForReftestReady(page, 5_000);
    assert.deepEqual(calls, ['probe']);
});

test('the per-test watchdog frees a worker held by a wedged navigation', async () => {
    const page = { goto: never, evaluate: never, waitForFunction: never, screenshot: never };
    await assert.rejects(
        () => renderTestWithWatchdog(page, '/wpt/css/wedged.html', '/out/wedged.png', 'css/wedged.html', 20),
        (err) => err instanceof StepTimeoutError &&
            /rendering css\/wedged\.html exceeded 20ms/.test(err.message),
    );
});

test('the per-test watchdog leaves a healthy render untouched', async () => {
    const seen = {};
    const page = {
        goto: async (url, options) => { seen.url = url; seen.gotoOptions = options; },
        evaluate: async () => false,
        waitForFunction: never,
        screenshot: async (options) => { seen.screenshotOptions = options; },
    };
    await renderTestWithWatchdog(page, __filename, '/out/ok.png', 'ok.html', 5_000);
    assert.match(seen.url, /^file:\/\//);
    assert.equal(seen.gotoOptions.waitUntil, 'load');
    assert.equal(seen.screenshotOptions.path, '/out/ok.png');
    assert.equal(seen.screenshotOptions.fullPage, false);
    // Spelled out rather than left to Playwright's ambient default, so the
    // watchdog budget stays derivable from this file's constants.
    assert.ok(seen.screenshotOptions.timeout > 0);
});

test('the watchdog budget covers every step of a worst-case legitimate render', () => {
    // load (10s) + reftest-wait probe and signal wait (5s each) + screenshot (30s).
    assert.ok(DEFAULT_RENDER_TIMEOUT > 10_000 + 2 * 5_000 + 30_000);
    assert.equal(resolveRenderTimeoutMs({}), DEFAULT_RENDER_TIMEOUT);
    assert.equal(
        resolveRenderTimeoutMs({ BROILER_WPT_REFERENCE_RENDER_TIMEOUT_SECONDS: '90' }),
        90_000);
    // Junk and non-positive overrides fall back to the derived default.
    for (const raw of ['0', '-5', 'abc', '']) {
        assert.equal(
            resolveRenderTimeoutMs({ BROILER_WPT_REFERENCE_RENDER_TIMEOUT_SECONDS: raw }),
            DEFAULT_RENDER_TIMEOUT);
    }
});

test('closeQuietly survives a teardown that fails or wedges', async () => {
    let closed = false;
    await closeQuietly({ close: async () => { closed = true; } }, 'ordinary close');
    assert.equal(closed, true);

    // A close that throws must not propagate: cleanup failure is never a reason
    // to fail a shard.
    await closeQuietly(
        { close: async () => { throw new Error('already gone'); } }, 'failing close');

    // A close that never returns must not park the worker either.
    const started = Date.now();
    await closeQuietly({ close: never }, 'wedged close', 20);
    assert.ok(Date.now() - started < 5_000, 'a wedged close must be bounded');
});
