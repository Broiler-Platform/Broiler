#!/usr/bin/env node
/*
 * Capture Chromium references for the real-world website render suite.
 *
 * Playwright is loaded lazily so `--help` and manifest validation work before
 * `npm ci` has populated tests/wpt/node_modules. CI sets NODE_PATH to that
 * pinned dependency rather than installing a second, drifting browser package.
 */

'use strict';

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const ID_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const MAX_DIAGNOSTICS = 50;
const MAX_DIAGNOSTIC_LENGTH = 800;
const SNAPSHOT_TIMEOUT_MS = 15_000;
const SCREENSHOT_TIMEOUT_MS = 30_000;

class UsageError extends Error {}
class StepTimeoutError extends Error {}

function usage() {
    return `Usage: node scripts/generate-real-world-references.js [OPTIONS]

Options:
  --manifest <FILE>          Site manifest (default: tests/real-world-sites/sites.json)
  --output-dir <DIR>         Result root (default: artifacts/real-world-render-tests)
  --site <ID[,ID...]>        Capture only these IDs; repeatable (default: all)
  --sites <ID[,ID...]>       Alias for --site
  --width <PX>               Override manifest viewport width
  --height <PX>              Override manifest viewport height
  --timeout-seconds <N>      Override navigation timeout
  --settle-ms <N>            Override bounded post-navigation settle time
  -h, --help                 Show this help
`;
}

function parseInteger(value, name, minimum, maximum) {
    if (!/^\d+$/.test(String(value)))
        throw new UsageError(`${name} must be an integer, got ${JSON.stringify(value)}`);
    const parsed = Number(value);
    if (!Number.isSafeInteger(parsed) || parsed < minimum || parsed > maximum)
        throw new UsageError(`${name} must be between ${minimum} and ${maximum}, got ${value}`);
    return parsed;
}

function parseArgs(argv) {
    const repoRoot = path.resolve(__dirname, '..');
    const options = {
        manifest: path.join(repoRoot, 'tests', 'real-world-sites', 'sites.json'),
        outputDir: path.join(repoRoot, 'artifacts', 'real-world-render-tests'),
        sites: [],
        width: null,
        height: null,
        timeoutSeconds: null,
        settleMilliseconds: null,
        help: false,
    };

    for (let index = 0; index < argv.length; index++) {
        const arg = argv[index];
        const requireValue = () => {
            if (index + 1 >= argv.length)
                throw new UsageError(`${arg} requires a value`);
            return argv[++index];
        };
        switch (arg) {
            case '--manifest':
                options.manifest = path.resolve(requireValue());
                break;
            case '--output-dir':
                options.outputDir = path.resolve(requireValue());
                break;
            case '--site':
            case '--sites':
                options.sites.push(...requireValue().split(',').map(value => value.trim()).filter(Boolean));
                break;
            case '--width':
                options.width = parseInteger(requireValue(), '--width', 320, 4096);
                break;
            case '--height':
                options.height = parseInteger(requireValue(), '--height', 240, 4096);
                break;
            case '--timeout-seconds':
                options.timeoutSeconds = parseInteger(requireValue(), '--timeout-seconds', 1, 300);
                break;
            case '--settle-ms':
                options.settleMilliseconds = parseInteger(requireValue(), '--settle-ms', 0, 30_000);
                break;
            case '-h':
            case '--help':
                options.help = true;
                break;
            default:
                throw new UsageError(`Unknown option: ${arg}`);
        }
    }
    return options;
}

function requireObject(value, label) {
    if (value === null || typeof value !== 'object' || Array.isArray(value))
        throw new UsageError(`${label} must be an object`);
    return value;
}

function requireString(value, label) {
    if (typeof value !== 'string' || value.trim() === '')
        throw new UsageError(`${label} must be a non-empty string`);
    return value;
}

function validatePercentage(value, label) {
    if (typeof value !== 'number' || !Number.isFinite(value) || value < 0 || value > 100)
        throw new UsageError(`${label} must be a number between 0 and 100`);
}

function loadManifest(manifestPath) {
    let parsed;
    try {
        parsed = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    } catch (error) {
        throw new UsageError(`Cannot read manifest ${manifestPath}: ${error.message}`);
    }
    requireObject(parsed, 'manifest');
    const allowedTopLevel = new Set(['$schema', 'schemaVersion', 'defaults', 'sites']);
    const unknownTopLevel = Object.keys(parsed).filter(key => !allowedTopLevel.has(key));
    if (unknownTopLevel.length)
        throw new UsageError(`manifest has unknown field(s): ${unknownTopLevel.join(', ')}`);
    if (parsed.schemaVersion !== 1)
        throw new UsageError(`manifest.schemaVersion must be 1, got ${JSON.stringify(parsed.schemaVersion)}`);
    if (parsed.$schema !== undefined)
        requireString(parsed.$schema, 'manifest.$schema');

    const defaults = requireObject(parsed.defaults, 'manifest.defaults');
    const configKeys = new Set([
        'viewport', 'navigationTimeoutSeconds', 'settleMilliseconds',
        'locale', 'timezoneId', 'pixelTolerance', 'thresholds',
    ]);
    const unknownDefaults = Object.keys(defaults).filter(key => !configKeys.has(key));
    if (unknownDefaults.length)
        throw new UsageError(`manifest.defaults has unknown field(s): ${unknownDefaults.join(', ')}`);
    const viewport = requireObject(defaults.viewport, 'manifest.defaults.viewport');
    const defaultViewportKeys = Object.keys(viewport);
    if (defaultViewportKeys.length !== 2 || !defaultViewportKeys.includes('width') || !defaultViewportKeys.includes('height'))
        throw new UsageError('manifest.defaults.viewport must contain only width and height');
    parseInteger(viewport.width, 'manifest.defaults.viewport.width', 320, 4096);
    parseInteger(viewport.height, 'manifest.defaults.viewport.height', 240, 4096);
    parseInteger(defaults.navigationTimeoutSeconds, 'manifest.defaults.navigationTimeoutSeconds', 1, 300);
    parseInteger(defaults.settleMilliseconds, 'manifest.defaults.settleMilliseconds', 0, 30_000);
    requireString(defaults.locale, 'manifest.defaults.locale');
    requireString(defaults.timezoneId, 'manifest.defaults.timezoneId');
    parseInteger(defaults.pixelTolerance, 'manifest.defaults.pixelTolerance', 0, 255);
    const thresholds = requireObject(defaults.thresholds, 'manifest.defaults.thresholds');
    const thresholdKeys = new Set(['pixelMatchPercent', 'contentMatchPercent', 'structureMatchPercent']);
    const unknownThresholds = Object.keys(thresholds).filter(key => !thresholdKeys.has(key));
    if (unknownThresholds.length)
        throw new UsageError(`manifest.defaults.thresholds has unknown field(s): ${unknownThresholds.join(', ')}`);
    for (const key of ['pixelMatchPercent', 'contentMatchPercent', 'structureMatchPercent'])
        validatePercentage(thresholds[key], `manifest.defaults.thresholds.${key}`);

    if (!Array.isArray(parsed.sites) || parsed.sites.length === 0)
        throw new UsageError('manifest.sites must be a non-empty array');
    const ids = new Set();
    for (const [index, siteValue] of parsed.sites.entries()) {
        const site = requireObject(siteValue, `manifest.sites[${index}]`);
        const siteKeys = new Set([
            'id', 'name', 'url', 'category', 'description', 'tags',
            'viewport', 'navigationTimeoutSeconds', 'settleMilliseconds',
            'locale', 'timezoneId', 'pixelTolerance', 'thresholds',
        ]);
        const unknownSiteFields = Object.keys(site).filter(key => !siteKeys.has(key));
        if (unknownSiteFields.length)
            throw new UsageError(`manifest.sites[${index}] has unknown field(s): ${unknownSiteFields.join(', ')}`);
        const id = requireString(site.id, `manifest.sites[${index}].id`);
        if (!ID_PATTERN.test(id))
            throw new UsageError(`Site ID ${JSON.stringify(id)} must be lowercase kebab-case`);
        if (ids.has(id))
            throw new UsageError(`Duplicate site ID: ${id}`);
        ids.add(id);
        requireString(site.name, `manifest.sites[${index}].name`);
        const urlText = requireString(site.url, `manifest.sites[${index}].url`);
        let url;
        try {
            url = new URL(urlText);
        } catch {
            throw new UsageError(`Site ${id} has an invalid URL: ${urlText}`);
        }
        if (url.protocol !== 'https:')
            throw new UsageError(`Site ${id} URL must use HTTPS`);
        if (site.category !== 'stable' && site.category !== 'dynamic')
            throw new UsageError(`Site ${id} category must be "stable" or "dynamic"`);
        if (site.description !== undefined && typeof site.description !== 'string')
            throw new UsageError(`site ${id}.description must be a string`);
        if (site.tags !== undefined) {
            if (!Array.isArray(site.tags) || site.tags.some(tag => typeof tag !== 'string' || !ID_PATTERN.test(tag)))
                throw new UsageError(`site ${id}.tags must contain lowercase kebab-case strings`);
            if (new Set(site.tags).size !== site.tags.length)
                throw new UsageError(`site ${id}.tags must be unique`);
        }
        if (site.viewport !== undefined) {
            const siteViewport = requireObject(site.viewport, `site ${id}.viewport`);
            const unknownViewport = Object.keys(siteViewport).filter(key => key !== 'width' && key !== 'height');
            if (unknownViewport.length || !Object.hasOwn(siteViewport, 'width') || !Object.hasOwn(siteViewport, 'height'))
                throw new UsageError(`site ${id}.viewport must contain only width and height`);
            parseInteger(siteViewport.width, `site ${id}.viewport.width`, 320, 4096);
            parseInteger(siteViewport.height, `site ${id}.viewport.height`, 240, 4096);
        }
        if (site.locale !== undefined)
            requireString(site.locale, `site ${id}.locale`);
        if (site.timezoneId !== undefined)
            requireString(site.timezoneId, `site ${id}.timezoneId`);
        if (site.navigationTimeoutSeconds !== undefined)
            parseInteger(site.navigationTimeoutSeconds, `site ${id}.navigationTimeoutSeconds`, 1, 300);
        if (site.settleMilliseconds !== undefined)
            parseInteger(site.settleMilliseconds, `site ${id}.settleMilliseconds`, 0, 30_000);
        if (site.pixelTolerance !== undefined)
            parseInteger(site.pixelTolerance, `site ${id}.pixelTolerance`, 0, 255);
        if (site.thresholds !== undefined) {
            const siteThresholds = requireObject(site.thresholds, `site ${id}.thresholds`);
            const keys = Object.keys(siteThresholds);
            if (keys.length === 0 || keys.some(key => !thresholdKeys.has(key)))
                throw new UsageError(`site ${id}.thresholds must contain only known threshold fields`);
            for (const key of keys)
                validatePercentage(siteThresholds[key], `site ${id}.thresholds.${key}`);
        }
    }
    return parsed;
}

function selectSites(manifest, requestedIds) {
    if (requestedIds.length === 0)
        return manifest.sites;
    const requested = [...new Set(requestedIds)];
    const byId = new Map(manifest.sites.map(site => [site.id, site]));
    const unknown = requested.filter(id => !byId.has(id));
    if (unknown.length)
        throw new UsageError(`Unknown site ID(s): ${unknown.join(', ')}`);
    return requested.map(id => byId.get(id));
}

function effectiveSettings(manifest, site, options) {
    const defaults = manifest.defaults;
    const siteViewport = site.viewport ?? {};
    return {
        viewport: {
            width: options.width ?? siteViewport.width ?? defaults.viewport.width,
            height: options.height ?? siteViewport.height ?? defaults.viewport.height,
        },
        navigationTimeoutSeconds:
            options.timeoutSeconds ?? site.navigationTimeoutSeconds ?? defaults.navigationTimeoutSeconds,
        settleMilliseconds:
            options.settleMilliseconds ?? site.settleMilliseconds ?? defaults.settleMilliseconds,
        locale: site.locale ?? defaults.locale,
        timezoneId: site.timezoneId ?? defaults.timezoneId,
    };
}

function truncate(value) {
    const text = String(value);
    return text.length <= MAX_DIAGNOSTIC_LENGTH
        ? text
        : `${text.slice(0, MAX_DIAGNOSTIC_LENGTH - 1)}…`;
}

function appendBounded(items, value) {
    if (items.length < MAX_DIAGNOSTICS)
        items.push(truncate(value));
}

function sha256(data) {
    return crypto.createHash('sha256').update(data).digest('hex');
}

function sha256File(filePath) {
    return sha256(fs.readFileSync(filePath));
}

function withTimeout(operation, timeoutMilliseconds, description) {
    const pending = Promise.resolve().then(operation);
    pending.catch(() => {});
    let timer;
    const deadline = new Promise((_, reject) => {
        timer = setTimeout(
            () => reject(new StepTimeoutError(`${description} exceeded ${timeoutMilliseconds}ms`)),
            timeoutMilliseconds);
    });
    return Promise.race([pending, deadline]).finally(() => clearTimeout(timer));
}

async function closeQuietly(closeable, description) {
    if (!closeable)
        return;
    try {
        await withTimeout(() => closeable.close(), 10_000, description);
    } catch {
        // Cleanup must not turn a completed capture into a failure.
    }
}

function outputPaths(outputDir, siteId) {
    const siteDir = path.join(outputDir, 'sites', siteId);
    return {
        siteDir,
        screenshot: path.join(siteDir, 'reference.png'),
        snapshot: path.join(siteDir, 'snapshot.html'),
        metadata: path.join(siteDir, 'reference.json'),
    };
}

function clearReferenceOutputs(paths) {
    fs.mkdirSync(paths.siteDir, { recursive: true });
    for (const target of [paths.screenshot, paths.snapshot, paths.metadata])
        fs.rmSync(target, { force: true });
}

async function serializeReplaySnapshot(page, finalUrl) {
    return withTimeout(
        () => page.evaluate(url => {
            const root = document.documentElement.cloneNode(true);
            // Template contents are separate document fragments and are not
            // reached by root.querySelectorAll(). Broiler's replay parser scans
            // raw markup, so remove dormant containers whose scripts could
            // otherwise become executable when the snapshot is parsed again.
            root.querySelectorAll('template,noscript').forEach(element => element.remove());
            root.querySelectorAll('script').forEach(element => element.remove());
            root.querySelectorAll('*').forEach(element => {
                for (const attribute of [...element.attributes]) {
                    if (attribute.name.toLowerCase().startsWith('on'))
                        element.removeAttribute(attribute.name);
                }
            });
            root.querySelectorAll('meta[http-equiv]').forEach(element => {
                const directive = (element.getAttribute('http-equiv') || '').toLowerCase();
                if (directive === 'content-security-policy' || directive === 'refresh')
                    element.remove();
            });
            root.querySelectorAll('iframe').forEach(element => {
                element.removeAttribute('srcdoc');
                element.setAttribute('src', 'about:blank');
            });
            root.querySelectorAll('object').forEach(element => element.removeAttribute('data'));
            root.querySelectorAll('embed').forEach(element => element.removeAttribute('src'));
            root.querySelectorAll('base').forEach(element => element.remove());
            let head = root.querySelector('head');
            if (!head) {
                head = document.createElement('head');
                root.insertBefore(head, root.firstChild);
            }
            const base = document.createElement('base');
            base.setAttribute('href', url);
            head.insertBefore(base, head.firstChild);
            const doctype = document.doctype
                ? `<!DOCTYPE ${document.doctype.name}>\n`
                : '<!DOCTYPE html>\n';
            return doctype + root.outerHTML;
        }, finalUrl),
        SNAPSHOT_TIMEOUT_MS,
        'DOM snapshot serialization');
}

async function captureAttempt(browser, playwrightVersion, manifest, site, options, attemptNumber) {
    const settings = effectiveSettings(manifest, site, options);
    const paths = outputPaths(options.outputDir, site.id);
    fs.rmSync(paths.screenshot, { force: true });
    fs.rmSync(paths.snapshot, { force: true });

    const consoleErrors = [];
    const pageErrors = [];
    const requestFailures = [];
    const started = Date.now();
    let context;
    let page;
    try {
        context = await withTimeout(
            () => browser.newContext({
                viewport: settings.viewport,
                deviceScaleFactor: 1,
                locale: settings.locale,
                timezoneId: settings.timezoneId,
                colorScheme: 'light',
                reducedMotion: 'reduce',
                serviceWorkers: 'block',
                extraHTTPHeaders: {
                    'Accept-Language': settings.locale,
                    'DNT': '1',
                },
            }),
            30_000,
            'browser context creation');
        page = await withTimeout(() => context.newPage(), 30_000, 'page creation');
        page.on('console', message => {
            if (message.type() === 'error' || message.type() === 'warning')
                appendBounded(consoleErrors, `${message.type()}: ${message.text()}`);
        });
        page.on('pageerror', error => appendBounded(pageErrors, error.stack || error.message));
        page.on('requestfailed', request => {
            const failure = request.failure();
            appendBounded(requestFailures, `${request.method()} ${request.url()}: ${failure?.errorText || 'failed'}`);
        });

        const navigationStarted = Date.now();
        const response = await page.goto(site.url, {
            waitUntil: 'domcontentloaded',
            timeout: settings.navigationTimeoutSeconds * 1000,
        });
        const navigationMilliseconds = Date.now() - navigationStarted;
        if (!response)
            throw new Error('Navigation completed without a main-document response');

        const settleStarted = Date.now();
        let settleState = 'networkidle';
        if (settings.settleMilliseconds > 0) {
            try {
                await page.waitForLoadState('networkidle', { timeout: settings.settleMilliseconds });
            } catch {
                settleState = 'bounded-timeout';
            }
            const remaining = settings.settleMilliseconds - (Date.now() - settleStarted);
            if (remaining > 0)
                await page.waitForTimeout(remaining);
        } else {
            settleState = 'disabled';
        }

        const finalUrl = page.url();
        const snapshot = await serializeReplaySnapshot(page, finalUrl);
        fs.writeFileSync(paths.snapshot, snapshot, 'utf8');

        await page.screenshot({
            path: paths.screenshot,
            fullPage: false,
            animations: 'disabled',
            caret: 'hide',
            timeout: SCREENSHOT_TIMEOUT_MS,
        });
        const screenshotAt = new Date().toISOString();

        let title = '';
        try {
            title = await withTimeout(() => page.title(), 5_000, 'page title');
        } catch (error) {
            appendBounded(pageErrors, error.message);
        }
        let userAgent = '';
        try {
            userAgent = await withTimeout(
                () => page.evaluate(() => navigator.userAgent), 5_000, 'user agent');
        } catch (error) {
            appendBounded(pageErrors, error.message);
        }
        let mainDocumentSha256 = null;
        let mainDocumentBytes = null;
        try {
            const body = await withTimeout(() => response.body(), 10_000, 'main response body');
            mainDocumentSha256 = sha256(body);
            mainDocumentBytes = body.length;
        } catch (error) {
            appendBounded(requestFailures, `Could not hash main response: ${error.message}`);
        }

        return {
            status: 'captured',
            attempt: attemptNumber,
            requestedUrl: site.url,
            finalUrl,
            responseStatus: response.status(),
            responseStatusText: response.statusText(),
            title,
            capturedAt: new Date().toISOString(),
            screenshotAt,
            durationMilliseconds: Date.now() - started,
            navigationMilliseconds,
            navigationTimeoutSeconds: settings.navigationTimeoutSeconds,
            settleMilliseconds: settings.settleMilliseconds,
            settleState,
            viewport: settings.viewport,
            locale: settings.locale,
            timezoneId: settings.timezoneId,
            deviceScaleFactor: 1,
            colorScheme: 'light',
            reducedMotion: 'reduce',
            playwrightVersion,
            chromiumVersion: browser.version(),
            userAgent,
            mainDocumentSha256,
            mainDocumentBytes,
            snapshotSha256: sha256(Buffer.from(snapshot, 'utf8')),
            referenceImageSha256: sha256File(paths.screenshot),
            consoleErrors,
            pageErrors,
            requestFailures,
        };
    } catch (error) {
        fs.rmSync(paths.screenshot, { force: true });
        fs.rmSync(paths.snapshot, { force: true });
        return {
            status: 'incomplete',
            attempt: attemptNumber,
            requestedUrl: site.url,
            capturedAt: new Date().toISOString(),
            durationMilliseconds: Date.now() - started,
            viewport: settings.viewport,
            locale: settings.locale,
            timezoneId: settings.timezoneId,
            playwrightVersion,
            chromiumVersion: browser.version(),
            error: truncate(error.stack || error.message || error),
            consoleErrors,
            pageErrors,
            requestFailures,
        };
    } finally {
        await closeQuietly(page, `page close for ${site.id}`);
        await closeQuietly(context, `context close for ${site.id}`);
    }
}

async function captureSite(browser, playwrightVersion, manifest, site, options) {
    const paths = outputPaths(options.outputDir, site.id);
    clearReferenceOutputs(paths);
    const attempts = [];
    let finalAttempt;
    for (let attempt = 1; attempt <= 2; attempt++) {
        console.log(`[${site.id}] Chromium capture attempt ${attempt}/2: ${site.url}`);
        finalAttempt = await captureAttempt(browser, playwrightVersion, manifest, site, options, attempt);
        attempts.push({
            attempt,
            status: finalAttempt.status,
            durationMilliseconds: finalAttempt.durationMilliseconds,
            error: finalAttempt.error ?? null,
        });
        if (finalAttempt.status === 'captured')
            break;
    }
    const metadata = {
        schemaVersion: 1,
        site: {
            id: site.id,
            name: site.name,
            url: site.url,
            category: site.category,
            description: site.description ?? '',
            tags: site.tags ?? [],
        },
        ...finalAttempt,
        attempts,
    };
    fs.writeFileSync(paths.metadata, `${JSON.stringify(metadata, null, 2)}\n`, 'utf8');
    console.log(`[${site.id}] ${metadata.status} in ${metadata.durationMilliseconds}ms`);
    return metadata.status === 'captured';
}

async function main(argv = process.argv.slice(2)) {
    let options;
    try {
        options = parseArgs(argv);
        if (options.help) {
            process.stdout.write(usage());
            return 0;
        }
        const manifest = loadManifest(options.manifest);
        const sites = selectSites(manifest, options.sites);

        // Clear every selected case before loading or launching Playwright. If
        // dependency setup or the browser process fails early, a prior run's
        // screenshot can never be mistaken for this run's reference.
        fs.mkdirSync(options.outputDir, { recursive: true });
        for (const site of sites)
            clearReferenceOutputs(outputPaths(options.outputDir, site.id));

        let playwright;
        try {
            playwright = require('playwright');
        } catch (error) {
            throw new UsageError(
                `Playwright is unavailable (${error.message}). Run npm ci in tests/wpt and set ` +
                'NODE_PATH=tests/wpt/node_modules.');
        }
        let playwrightVersion = 'unknown';
        try {
            playwrightVersion = require('playwright/package.json').version;
        } catch {
            // The browser version is also recorded; an unusual package layout is non-fatal.
        }

        let browser;
        try {
            browser = await withTimeout(
                () => playwright.chromium.launch({ headless: true }),
                60_000,
                'Chromium launch');
        } catch (error) {
            // Preserve one explicit status per selected case even when browser
            // provisioning fails before a page can be created. The aggregate
            // runner can then distinguish an unavailable reference from a
            // Broiler render mismatch without scraping process stderr.
            for (const site of sites) {
                const paths = outputPaths(options.outputDir, site.id);
                clearReferenceOutputs(paths);
                const failure = {
                    schemaVersion: 1,
                    site: {
                        id: site.id,
                        name: site.name,
                        url: site.url,
                        category: site.category,
                        description: site.description ?? '',
                        tags: site.tags ?? [],
                    },
                    status: 'incomplete',
                    requestedUrl: site.url,
                    capturedAt: new Date().toISOString(),
                    playwrightVersion,
                    chromiumVersion: null,
                    error: truncate(error.stack || error.message || error),
                    attempts: [{ attempt: 0, status: 'incomplete', error: truncate(error.message || error) }],
                };
                fs.writeFileSync(paths.metadata, `${JSON.stringify(failure, null, 2)}\n`, 'utf8');
            }
            console.error(`Chromium could not start: ${error.stack || error.message || error}`);
            return 1;
        }
        let incomplete = 0;
        try {
            for (const site of sites) {
                if (!await captureSite(browser, playwrightVersion, manifest, site, options))
                    incomplete++;
            }
        } finally {
            await closeQuietly(browser, 'browser close');
        }
        console.log(`Chromium references: ${sites.length - incomplete} captured, ${incomplete} incomplete.`);
        return incomplete === 0 ? 0 : 1;
    } catch (error) {
        const prefix = error instanceof UsageError ? 'Configuration error' : 'Reference capture failed';
        console.error(`${prefix}: ${error.stack || error.message || error}`);
        if (error instanceof UsageError)
            console.error('Run with --help for usage.');
        return 2;
    }
}

if (require.main === module) {
    main().then(
        exitCode => { process.exitCode = exitCode; },
        error => {
            console.error(error.stack || error);
            process.exitCode = 2;
        });
}

module.exports = {
    UsageError,
    effectiveSettings,
    loadManifest,
    main,
    parseArgs,
    selectSites,
    serializeReplaySnapshot,
    sha256,
    usage,
};
