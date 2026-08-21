#!/usr/bin/env node
/*
 * Capture Chromium references for the DuckDuckGo privacy test page suite.
 *
 * The pages publish everything they measured in a `results` global, so a
 * reference is that global as a shipping engine fills it — not a screenshot.
 * The same manifest expressions Broiler.Cli evaluates are evaluated here, which
 * is what makes the two runs comparable: a probe that Chromium answers and
 * Broiler does not is a platform gap, and a probe neither answers is a property
 * of the page or the network rather than of Broiler.
 *
 * Playwright is loaded lazily so `--help` and manifest validation work before
 * `npm ci` has populated tests/wpt/node_modules. CI sets NODE_PATH to that
 * pinned dependency rather than installing a second, drifting browser package.
 */

'use strict';

const fs = require('fs');
const path = require('path');

const ID_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const SCHEMA_VERSION = 1;
const MAX_DIAGNOSTICS = 50;
const MAX_DIAGNOSTIC_LENGTH = 800;
const LAUNCH_TIMEOUT_MS = 60_000;
const CONTEXT_TIMEOUT_MS = 30_000;
const EVALUATE_TIMEOUT_MS = 30_000;
const DEFAULT_POLL_INTERVAL_MS = 500;
// A page's probes land in `results` over time, so the array is read until its
// length holds still. Three quiet polls is the shortest window that does not
// mistake the gap between two slow probes for the end of the run.
const DEFAULT_STABLE_POLLS = 3;
const DEFAULT_VIEWPORT = { width: 1280, height: 720 };
const DEFAULT_LOCALE = 'en-US';
const DEFAULT_TIMEZONE = 'UTC';

class UsageError extends Error {}
class StepTimeoutError extends Error {}

function usage() {
    return `Usage: node scripts/capture-privacy-reference.js [OPTIONS]

Runs each privacy test page in Chromium and records the page's own \`results\`
global as the reference Broiler is compared against.

Options:
  --manifest <FILE>          Page manifest (default: tests/privacy-test-pages/pages.json)
  --output-dir <DIR>         Result root (default: artifacts/privacy-test-pages)
  --pages <ID[,ID...]>       Capture only these page ids; repeatable (default: all)
  --page <ID[,ID...]>        Alias for --pages
  --timeout-seconds <N>      Override the manifest per-page timeout
  --attempts <N>             Override the manifest attempt count (1-5)
  --poll-interval-ms <N>     How often the results array is re-read (default: ${DEFAULT_POLL_INTERVAL_MS})
  --executable-path <FILE>   Chromium binary to launch instead of Playwright's own
                             (also read from BROILER_CHROMIUM_EXECUTABLE)
  --headed                   Launch with a visible browser window
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
        manifest: path.join(repoRoot, 'tests', 'privacy-test-pages', 'pages.json'),
        outputDir: path.join(repoRoot, 'artifacts', 'privacy-test-pages'),
        pages: [],
        timeoutSeconds: null,
        attempts: null,
        pollIntervalMilliseconds: DEFAULT_POLL_INTERVAL_MS,
        executablePath: process.env.BROILER_CHROMIUM_EXECUTABLE || null,
        headed: false,
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
            case '--page':
            case '--pages':
                options.pages.push(...requireValue().split(',').map(value => value.trim()).filter(Boolean));
                break;
            case '--timeout-seconds':
                options.timeoutSeconds = parseInteger(requireValue(), '--timeout-seconds', 1, 600);
                break;
            case '--attempts':
                options.attempts = parseInteger(requireValue(), '--attempts', 1, 5);
                break;
            case '--poll-interval-ms':
                options.pollIntervalMilliseconds = parseInteger(requireValue(), '--poll-interval-ms', 50, 10_000);
                break;
            case '--executable-path':
                options.executablePath = path.resolve(requireValue());
                break;
            case '--headed':
                options.headed = true;
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

function requireIdentifier(value, label) {
    const text = requireString(value, label);
    if (!ID_PATTERN.test(text))
        throw new UsageError(`${label} must be a lowercase hyphenated identifier, got ${JSON.stringify(text)}`);
    return text;
}

/*
 * Validates the shape this script depends on rather than the whole manifest
 * contract; run-privacy-test-pages.py owns the authoritative validation and
 * both read the same file, so a disagreement here would be a second source of
 * truth rather than a stricter check.
 */
function validateManifest(document, label = 'manifest') {
    const manifest = requireObject(document, label);
    if (manifest.schemaVersion !== SCHEMA_VERSION)
        throw new UsageError(`${label}.schemaVersion must be ${SCHEMA_VERSION}`);

    const source = requireObject(manifest.source, `${label}.source`);
    const baseUrl = requireString(source.baseUrl, `${label}.source.baseUrl`);
    if (!baseUrl.startsWith('https://'))
        throw new UsageError(`${label}.source.baseUrl must be an https:// origin`);
    if (baseUrl.endsWith('/'))
        throw new UsageError(`${label}.source.baseUrl must not end with '/'`);

    const defaults = requireObject(manifest.defaults, `${label}.defaults`);
    requireString(defaults.startExpression, `${label}.defaults.startExpression`);
    requireString(defaults.resultsExpression, `${label}.defaults.resultsExpression`);
    if (!Number.isSafeInteger(defaults.timeoutSeconds) || defaults.timeoutSeconds < 1)
        throw new UsageError(`${label}.defaults.timeoutSeconds must be a positive integer`);
    if (!Number.isSafeInteger(defaults.attempts) || defaults.attempts < 1)
        throw new UsageError(`${label}.defaults.attempts must be a positive integer`);

    if (!Array.isArray(manifest.pages) || manifest.pages.length === 0)
        throw new UsageError(`${label}.pages must be a non-empty array`);

    const seen = new Set();
    manifest.pages.forEach((entry, index) => {
        const location = `${label}.pages[${index}]`;
        const page = requireObject(entry, location);
        const id = requireIdentifier(page.id, `${location}.id`);
        if (seen.has(id))
            throw new UsageError(`${location}.id duplicates ${JSON.stringify(id)}`);
        seen.add(id);
        requireString(page.name, `${location}.name`);
        const pagePath = requireString(page.path, `${location}.path`);
        if (!pagePath.startsWith('/'))
            throw new UsageError(`${location}.path must start with '/'`);
    });

    return manifest;
}

function loadManifest(file) {
    let raw;
    try {
        raw = fs.readFileSync(file, 'utf8');
    } catch (error) {
        throw new UsageError(`Manifest ${file} could not be read: ${error.message}`);
    }
    let parsed;
    try {
        parsed = JSON.parse(raw);
    } catch (error) {
        throw new UsageError(`Manifest ${file} is not valid JSON: ${error.message}`);
    }
    return validateManifest(parsed, file);
}

function selectPages(manifest, requested) {
    if (!requested.length)
        return manifest.pages;
    const known = new Map(manifest.pages.map(page => [page.id, page]));
    const unknown = requested.filter(id => !known.has(id));
    if (unknown.length)
        throw new UsageError(`Unknown page id(s): ${unknown.join(', ')}`);
    return requested.map(id => known.get(id));
}

function effectiveSettings(manifest, page, options) {
    return {
        timeoutSeconds: options.timeoutSeconds
            ?? page.timeoutSeconds
            ?? manifest.defaults.timeoutSeconds,
        attempts: options.attempts ?? page.attempts ?? manifest.defaults.attempts,
        startExpression: page.startExpression ?? manifest.defaults.startExpression,
        resultsExpression: page.resultsExpression ?? manifest.defaults.resultsExpression,
    };
}

function pageUrl(manifest, page) {
    return `${manifest.source.baseUrl}${page.path}`;
}

function truncate(value, limit = MAX_DIAGNOSTIC_LENGTH) {
    const text = String(value);
    return text.length <= limit ? text : `${text.slice(0, limit - 1)}…`;
}

function appendBounded(list, value) {
    if (list.length < MAX_DIAGNOSTICS)
        list.push(truncate(value));
    else if (list.length === MAX_DIAGNOSTICS)
        list.push(`… further entries dropped after ${MAX_DIAGNOSTICS}`);
}

function withTimeout(action, milliseconds, label) {
    return new Promise((resolve, reject) => {
        const timer = setTimeout(
            () => reject(new StepTimeoutError(`${label} exceeded ${milliseconds}ms`)),
            milliseconds);
        Promise.resolve()
            .then(action)
            .then(resolve, reject)
            .finally(() => clearTimeout(timer));
    });
}

async function closeQuietly(closable, label) {
    if (!closable)
        return;
    try {
        await withTimeout(() => closable.close(), 15_000, label);
    } catch {
        // Cleanup must not turn a completed capture into a failure.
    }
}

function referencePath(outputDir, pageId) {
    return path.join(outputDir, 'pages', pageId, 'reference.json');
}

function clearReference(outputDir, pageId) {
    const target = referencePath(outputDir, pageId);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.rmSync(target, { force: true });
}

/*
 * Reads the manifest's results expression and reports how many probes it has
 * published so far. A page that has not defined the global yet, or that throws
 * while serializing, is reported as "no payload" rather than as a failure: both
 * are ordinary states part-way through a run.
 */
function parseResultsPayload(serialized) {
    const empty = { payload: null, count: 0, answered: 0 };
    if (typeof serialized !== 'string' || serialized.trim() === '')
        return empty;
    let parsed;
    try {
        parsed = JSON.parse(serialized);
    } catch {
        return empty;
    }
    if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed))
        return empty;
    const entries = Array.isArray(parsed.results) ? parsed.results : null;
    if (entries === null)
        return empty;
    // Most pages push a probe only once it settles, so the length alone would
    // do; a page that seeds its array and fills the values in afterwards would
    // otherwise look finished on the first poll, so answered values are counted
    // as well and the capture waits for both to hold still.
    const answered = entries.filter(entry =>
        entry !== null
        && typeof entry === 'object'
        && entry.value !== undefined
        && entry.value !== null).length;
    return { payload: parsed, count: entries.length, answered };
}

async function readResults(page, expression) {
    const serialized = await withTimeout(
        () => page.evaluate(expression),
        EVALUATE_TIMEOUT_MS,
        'results expression');
    return parseResultsPayload(serialized);
}

async function captureAttempt(browser, manifest, entry, options, attemptNumber) {
    const settings = effectiveSettings(manifest, entry, options);
    const url = pageUrl(manifest, entry);
    const consoleErrors = [];
    const pageErrors = [];
    const requestFailures = [];
    const started = Date.now();
    let context;
    let tab;

    try {
        context = await withTimeout(
            () => browser.newContext({
                viewport: DEFAULT_VIEWPORT,
                deviceScaleFactor: 1,
                locale: DEFAULT_LOCALE,
                timezoneId: DEFAULT_TIMEZONE,
                colorScheme: 'light',
                reducedMotion: 'reduce',
                // Storage, worker and service-worker probes are exactly what
                // several of these pages measure, so nothing is blocked here:
                // a reference has to be what a stock browser does.
                serviceWorkers: 'allow',
                extraHTTPHeaders: { 'Accept-Language': DEFAULT_LOCALE },
            }),
            CONTEXT_TIMEOUT_MS,
            'browser context creation');
        tab = await withTimeout(() => context.newPage(), CONTEXT_TIMEOUT_MS, 'page creation');
        tab.on('console', message => {
            if (message.type() === 'error' || message.type() === 'warning')
                appendBounded(consoleErrors, `${message.type()}: ${message.text()}`);
        });
        tab.on('pageerror', error => appendBounded(pageErrors, error.stack || error.message));
        tab.on('requestfailed', request => {
            const failure = request.failure();
            appendBounded(requestFailures, `${request.method()} ${request.url()}: ${failure?.errorText || 'failed'}`);
        });

        const deadline = started + settings.timeoutSeconds * 1000;
        const response = await tab.goto(url, {
            waitUntil: 'load',
            timeout: Math.max(1_000, deadline - Date.now()),
        });
        if (!response)
            throw new Error('Navigation completed without a main-document response');

        let startValue = null;
        let startError = null;
        try {
            startValue = await withTimeout(
                () => tab.evaluate(settings.startExpression),
                EVALUATE_TIMEOUT_MS,
                'start expression');
        } catch (error) {
            // A page that runs itself on load can reject the trigger; the
            // results poll below still decides whether the run produced data.
            startError = truncate(error.message || error);
        }

        let payload = null;
        let count = 0;
        let answered = 0;
        let quietPolls = 0;
        let settleState = 'deadline';
        while (Date.now() < deadline) {
            const observed = await readResults(tab, settings.resultsExpression);
            const held = observed.count === count && observed.answered === answered;
            quietPolls = held && count > 0 ? quietPolls + 1 : 0;
            if (observed.payload !== null) {
                payload = observed.payload;
                count = observed.count;
                answered = observed.answered;
            }
            if (quietPolls >= DEFAULT_STABLE_POLLS) {
                settleState = 'stable';
                break;
            }
            await tab.waitForTimeout(
                Math.min(options.pollIntervalMilliseconds, Math.max(0, deadline - Date.now())));
        }

        if (payload === null) {
            const final = await readResults(tab, settings.resultsExpression);
            payload = final.payload;
            count = final.count;
        }

        return {
            status: payload !== null && count > 0 ? 'ok' : 'failed',
            reason: payload === null
                ? 'the page did not publish a results object'
                : (count > 0 ? null : 'the page published an empty results array'),
            attempt: attemptNumber,
            requestedUrl: url,
            finalUrl: tab.url(),
            responseStatus: response.status(),
            durationMilliseconds: Date.now() - started,
            settleState,
            testCount: count,
            startExpressionValue: startValue,
            startExpressionError: startError,
            results: payload,
            userAgent: await tab.evaluate('navigator.userAgent').catch(() => null),
            diagnostics: { consoleErrors, pageErrors, requestFailures },
        };
    } catch (error) {
        return {
            status: 'failed',
            reason: truncate(error.message || error),
            attempt: attemptNumber,
            requestedUrl: url,
            durationMilliseconds: Date.now() - started,
            settleState: 'error',
            testCount: 0,
            results: null,
            diagnostics: { consoleErrors, pageErrors, requestFailures },
        };
    } finally {
        await closeQuietly(tab, `page close for ${entry.id}`);
        await closeQuietly(context, `context close for ${entry.id}`);
    }
}

function referenceDocument(manifest, entry, browserMetadata, attempts, finalAttempt) {
    const { results, diagnostics, ...rest } = finalAttempt;
    return {
        schemaVersion: SCHEMA_VERSION,
        engine: 'chromium',
        page: {
            id: entry.id,
            name: entry.name,
            url: pageUrl(manifest, entry),
            path: entry.path,
        },
        capturedAt: new Date().toISOString(),
        browser: browserMetadata,
        ...rest,
        diagnostics,
        attempts,
        results,
    };
}

async function capturePage(browser, browserMetadata, manifest, entry, options) {
    clearReference(options.outputDir, entry.id);
    const settings = effectiveSettings(manifest, entry, options);
    const attempts = [];
    let finalAttempt;
    for (let attempt = 1; attempt <= settings.attempts; attempt++) {
        console.log(`[${entry.id}] Chromium attempt ${attempt}/${settings.attempts}: ${pageUrl(manifest, entry)}`);
        finalAttempt = await captureAttempt(browser, manifest, entry, options, attempt);
        attempts.push({
            attempt,
            status: finalAttempt.status,
            reason: finalAttempt.reason ?? null,
            testCount: finalAttempt.testCount,
            durationMilliseconds: finalAttempt.durationMilliseconds,
        });
        if (finalAttempt.status === 'ok')
            break;
    }

    const document = referenceDocument(manifest, entry, browserMetadata, attempts, finalAttempt);
    const target = referencePath(options.outputDir, entry.id);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, `${JSON.stringify(document, null, 2)}\n`, 'utf8');
    console.log(`[${entry.id}] ${document.status}: ${document.testCount} test(s) in ${document.durationMilliseconds}ms`);
    return document.status === 'ok';
}

function unavailableReference(manifest, entry, browserMetadata, error) {
    return {
        schemaVersion: SCHEMA_VERSION,
        engine: 'chromium',
        page: {
            id: entry.id,
            name: entry.name,
            url: pageUrl(manifest, entry),
            path: entry.path,
        },
        capturedAt: new Date().toISOString(),
        browser: browserMetadata,
        status: 'failed',
        reason: truncate(error.message || error),
        testCount: 0,
        settleState: 'error',
        attempts: [],
        diagnostics: { consoleErrors: [], pageErrors: [], requestFailures: [] },
        results: null,
    };
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
        const pages = selectPages(manifest, options.pages);

        // Clear every selected page before Playwright is loaded or a browser is
        // launched, so a previous run's reference can never be read as this
        // run's evidence when provisioning fails early.
        fs.mkdirSync(options.outputDir, { recursive: true });
        for (const entry of pages)
            clearReference(options.outputDir, entry.id);

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
            // The browser version is recorded too; an unusual layout is non-fatal.
        }

        const launchOptions = { headless: !options.headed };
        if (options.executablePath)
            launchOptions.executablePath = options.executablePath;

        let browser;
        try {
            browser = await withTimeout(
                () => playwright.chromium.launch(launchOptions),
                LAUNCH_TIMEOUT_MS,
                'Chromium launch');
        } catch (error) {
            // Leave one explicit reference per selected page even when the
            // browser never starts, so the comparing runner can tell an
            // unavailable reference from a Broiler gap without reading stderr.
            const metadata = {
                playwrightVersion,
                chromiumVersion: null,
                executablePath: options.executablePath,
            };
            for (const entry of pages) {
                const target = referencePath(options.outputDir, entry.id);
                fs.mkdirSync(path.dirname(target), { recursive: true });
                fs.writeFileSync(
                    target,
                    `${JSON.stringify(unavailableReference(manifest, entry, metadata, error), null, 2)}\n`,
                    'utf8');
            }
            console.error(`Chromium could not start: ${error.stack || error.message || error}`);
            return 1;
        }

        const browserMetadata = {
            playwrightVersion,
            chromiumVersion: browser.version(),
            executablePath: options.executablePath,
        };

        let failed = 0;
        try {
            for (const entry of pages) {
                if (!await capturePage(browser, browserMetadata, manifest, entry, options))
                    failed++;
            }
        } finally {
            await closeQuietly(browser, 'browser close');
        }
        console.log(`Chromium references: ${pages.length - failed} captured, ${failed} incomplete.`);
        return failed === 0 ? 0 : 1;
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
    pageUrl,
    parseArgs,
    parseResultsPayload,
    referenceDocument,
    selectPages,
    usage,
    validateManifest,
};
