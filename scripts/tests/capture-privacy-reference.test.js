'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const capture = require('../capture-privacy-reference.js');

const repoRoot = path.resolve(__dirname, '..', '..');
const manifestPath = path.join(repoRoot, 'tests', 'privacy-test-pages', 'pages.json');

test('the checked-in manifest validates and names the privacy-protections pages', () => {
    const manifest = capture.loadManifest(manifestPath);
    const ids = manifest.pages.map(page => page.id);
    assert.ok(ids.includes('fingerprinting'));
    assert.ok(ids.includes('storage-blocking'));
    assert.ok(manifest.pages.every(page => page.path.startsWith('/privacy-protections/')));
});

test('page selection preserves the requested order and rejects unknown ids', () => {
    const manifest = capture.loadManifest(manifestPath);
    assert.deepEqual(
        capture.selectPages(manifest, ['gpc', 'fingerprinting']).map(page => page.id),
        ['gpc', 'fingerprinting']);
    assert.throws(() => capture.selectPages(manifest, ['not-in-the-corpus']), /Unknown page id/);
});

test('every page resolves to an https URL under the manifest origin', () => {
    const manifest = capture.loadManifest(manifestPath);
    for (const page of manifest.pages)
        assert.ok(capture.pageUrl(manifest, page).startsWith('https://privacy-test-pages.site/'));
});

test('the CLI parses repeatable page filters and the container browser override', () => {
    const options = capture.parseArgs([
        '--pages', 'gpc,surrogates',
        '--page', 'fingerprinting',
        '--timeout-seconds', '45',
        '--attempts', '1',
        '--poll-interval-ms', '250',
        '--executable-path', '/opt/pw-browsers/chromium',
    ]);
    assert.deepEqual(options.pages, ['gpc', 'surrogates', 'fingerprinting']);
    assert.equal(options.timeoutSeconds, 45);
    assert.equal(options.attempts, 1);
    assert.equal(options.pollIntervalMilliseconds, 250);
    assert.equal(options.executablePath, '/opt/pw-browsers/chromium');
});

test('an out-of-range or non-numeric option is rejected before a browser starts', () => {
    assert.throws(() => capture.parseArgs(['--attempts', '9']), capture.UsageError);
    assert.throws(() => capture.parseArgs(['--timeout-seconds', 'soon']), capture.UsageError);
    assert.throws(() => capture.parseArgs(['--surprise']), /Unknown option/);
});

test('page overrides win over the manifest defaults, and the CLI wins over both', () => {
    const manifest = capture.loadManifest(manifestPath);
    const page = { ...manifest.pages[0], timeoutSeconds: 90, startExpression: 'runIt()' };

    const fromManifest = capture.effectiveSettings(manifest, page, capture.parseArgs([]));
    assert.equal(fromManifest.timeoutSeconds, 90);
    assert.equal(fromManifest.startExpression, 'runIt()');
    assert.equal(fromManifest.resultsExpression, manifest.defaults.resultsExpression);

    const overridden = capture.effectiveSettings(
        manifest, page, capture.parseArgs(['--timeout-seconds', '10']));
    assert.equal(overridden.timeoutSeconds, 10);
});

test('the results expression is read as a payload only when it carries a results array', () => {
    assert.deepEqual(capture.parseResultsPayload(''), { payload: null, count: 0 });
    assert.deepEqual(capture.parseResultsPayload('not json'), { payload: null, count: 0 });
    assert.deepEqual(capture.parseResultsPayload('[1,2]'), { payload: null, count: 0 });
    assert.deepEqual(capture.parseResultsPayload('{"page":"x"}'), { payload: null, count: 0 });

    const observed = capture.parseResultsPayload('{"results":[{"id":"a","value":1}]}');
    assert.equal(observed.count, 1);
    assert.equal(observed.payload.results[0].id, 'a');
});

test('a manifest from another schema version is rejected rather than half-read', () => {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'privacy-manifest-'));
    try {
        const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
        manifest.schemaVersion = 2;
        const target = path.join(directory, 'pages.json');
        fs.writeFileSync(target, JSON.stringify(manifest), 'utf8');
        assert.throws(() => capture.loadManifest(target), /schemaVersion must be 1/);
    } finally {
        fs.rmSync(directory, { recursive: true, force: true });
    }
});

test('a duplicate page id is rejected so two captures cannot share one reference file', () => {
    const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    manifest.pages.push({ ...manifest.pages[0] });
    assert.throws(() => capture.validateManifest(manifest), /duplicates/);
});

test('the reference document keeps the page identity, the browser and the payload', () => {
    const manifest = capture.loadManifest(manifestPath);
    const page = manifest.pages.find(entry => entry.id === 'gpc');
    const browser = { playwrightVersion: '1.59.1', chromiumVersion: '141.0.0.0', executablePath: null };
    const attempt = {
        status: 'ok',
        reason: null,
        attempt: 1,
        testCount: 5,
        durationMilliseconds: 1234,
        settleState: 'stable',
        results: { results: [{ id: 'navigator.globalPrivacyControl', value: true }] },
        diagnostics: { consoleErrors: [], pageErrors: [], requestFailures: [] },
    };

    const document = capture.referenceDocument(manifest, page, browser, [{ attempt: 1 }], attempt);

    assert.equal(document.schemaVersion, 1);
    assert.equal(document.engine, 'chromium');
    assert.equal(document.page.id, 'gpc');
    assert.equal(document.page.url, capture.pageUrl(manifest, page));
    assert.equal(document.browser.chromiumVersion, '141.0.0.0');
    assert.equal(document.status, 'ok');
    assert.equal(document.testCount, 5);
    assert.equal(document.results.results[0].id, 'navigator.globalPrivacyControl');
});

test('--help is answered without loading Playwright or touching the network', async () => {
    assert.equal(await capture.main(['--help']), 0);
    assert.match(capture.usage(), /capture-privacy-reference\.js/);
});
