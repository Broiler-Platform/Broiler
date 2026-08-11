'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const generator = require('../generate-real-world-references.js');

const repoRoot = path.resolve(__dirname, '..', '..');
const manifestPath = path.join(repoRoot, 'tests', 'real-world-sites', 'sites.json');

test('checked-in manifest validates and contains the requested sites', () => {
    const manifest = generator.loadManifest(manifestPath);
    const ids = manifest.sites.map(site => site.id);
    assert.ok(ids.includes('google-search'));
    assert.ok(ids.includes('seven-zip'));
    assert.ok(ids.includes('heise'));
});

test('site selection preserves requested order and rejects unknown IDs', () => {
    const manifest = generator.loadManifest(manifestPath);
    assert.deepEqual(
        generator.selectSites(manifest, ['heise', 'seven-zip']).map(site => site.id),
        ['heise', 'seven-zip']);
    assert.throws(
        () => generator.selectSites(manifest, ['not-in-the-corpus']),
        /Unknown site ID/);
});

test('CLI parses repeatable and comma-separated site filters without Playwright', () => {
    const options = generator.parseArgs([
        '--site', 'google-search,seven-zip',
        '--site', 'heise',
        '--width', '1280',
        '--height', '720',
        '--timeout-seconds', '20',
        '--settle-ms', '500',
    ]);
    assert.deepEqual(options.sites, ['google-search', 'seven-zip', 'heise']);
    assert.equal(options.width, 1280);
    assert.equal(options.height, 720);
    assert.equal(options.timeoutSeconds, 20);
    assert.equal(options.settleMilliseconds, 500);
});

test('effective settings apply site locale and command-line viewport overrides', () => {
    const manifest = generator.loadManifest(manifestPath);
    const heise = manifest.sites.find(site => site.id === 'heise');
    const options = generator.parseArgs(['--width', '800', '--height', '600']);
    const settings = generator.effectiveSettings(manifest, heise, options);

    assert.deepEqual(settings.viewport, { width: 800, height: 600 });
    assert.equal(settings.locale, 'de-DE');
    assert.equal(settings.timezoneId, 'Europe/Berlin');
});

test('effective settings honor a per-site viewport when the CLI does not override it', () => {
    const manifest = generator.loadManifest(manifestPath);
    const site = { ...manifest.sites[0], viewport: { width: 900, height: 700 } };
    const settings = generator.effectiveSettings(manifest, site, generator.parseArgs([]));

    assert.deepEqual(settings.viewport, { width: 900, height: 700 });
});

test('standalone manifest validation rejects unknown site fields', () => {
    const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    manifest.sites[0].surprise = true;
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'broiler-real-sites-'));
    const temporaryManifest = path.join(directory, 'sites.json');
    try {
        fs.writeFileSync(temporaryManifest, JSON.stringify(manifest));
        assert.throws(() => generator.loadManifest(temporaryManifest), /unknown field/);
    } finally {
        fs.rmSync(directory, { recursive: true, force: true });
    }
});
