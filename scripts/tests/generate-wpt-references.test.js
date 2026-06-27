'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const {
    createBrowserContextOptions,
    discoverTests,
    isNonTestFile,
    requiresJavaScript,
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
