'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const {
    contentTypeForExtension,
    createBrowserContextOptions,
    discoverTests,
    isNonTestFile,
    isWptHarnessScript,
    requiresJavaScript,
    resolveRootRelativeResource,
    shardIndexForPath,
} = require('../generate-wpt-references.js');


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
        const testDoc = path.join(root, 'css', 'css-grid', 'test.html');
        const relResource = path.join(root, 'css', 'css-grid', 'ref.png');
        fs.writeFileSync(gridCss, '.grid{display:grid}');
        fs.writeFileSync(ahem, '@font-face{}');
        fs.writeFileSync(testDoc, '<html></html>');
        fs.writeFileSync(relResource, 'x');

        // Root-relative support stylesheet / font → remapped under the WPT root.
        assert.equal(
            resolveRootRelativeResource(root, 'file:///css/support/grid.css'),
            gridCss);
        assert.equal(
            resolveRootRelativeResource(root, 'file:///fonts/ahem.css?v=2'),
            ahem);

        // The test document and any path that resolves on disk as-is → null,
        // so Chromium loads it directly rather than being re-served.
        assert.equal(resolveRootRelativeResource(root, 'file://' + testDoc), null);
        assert.equal(resolveRootRelativeResource(root, 'file://' + relResource), null);

        // Missing resources and ../ escapes → null (Chromium 404s them).
        assert.equal(resolveRootRelativeResource(root, 'file:///css/support/missing.css'), null);
        assert.equal(resolveRootRelativeResource(root, 'file:///../../etc/passwd'), null);

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
