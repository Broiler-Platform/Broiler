#!/usr/bin/env node
// generate-wpt-references.js — Generate Chromium reference screenshots for
// web-platform-tests using Playwright.
//
// Usage:
//     node scripts/generate-wpt-references.js <test-dir> <output-dir> [--concurrency N] [--base-dir <dir>]
//
// For each .html / .htm / .xhtml file under <test-dir>, headless Chromium
// takes a 1024×768 viewport screenshot and writes the PNG to <output-dir>
// mirroring the relative directory structure.
//
// WPT tests frequently reference fonts via root-relative URLs such as
//     @import "/fonts/ahem.css";
// These map to {wptRoot}/fonts/ on disk.  When a fonts/ directory exists
// alongside the test root (baseDir), this script intercepts those requests
// and serves the local files so that custom test fonts (e.g. Ahem) render
// correctly in Chromium, matching real WPT behaviour.

'use strict';

const path = require('path');
const fs = require('fs');
const { pathToFileURL } = require('url');

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------
const TEST_EXTENSIONS = new Set(['.html', '.htm', '.xht', '.xhtml']);
const NON_TEST_DIRECTORIES = new Set([
    '.git',
    'node_modules',
    'reference',
    'references',
    'reftest',
    'resources',
    'support',
    'test-plan',
]);
const VIEWPORT = { width: 1024, height: 768 };
const PAGE_LOAD_TIMEOUT = 10_000;   // ms — max time to wait for page load
const REFTEST_WAIT_TIMEOUT = 5_000; // ms — max time to wait for a reftest-wait page to signal ready
const SCREENSHOT_TIMEOUT = 30_000;  // ms — max time to capture one screenshot
const CLOSE_TIMEOUT = 30_000;       // ms — max time to tear down a page/context/browser
const CONTEXT_SETUP_TIMEOUT = 60_000; // ms — max time to build a fresh context+page (incl. a browser restart)
// Per-test watchdog: an upper bound on one test's whole render, derived from the
// step budgets above so it cannot drift out of sync when one of them changes.
// The worst legitimate render spends the full load budget, both halves of the
// reftest-wait budget (probe + signal wait), and the full screenshot budget; the
// slack keeps a merely slow test from tripping the watchdog. See withTimeout for
// why a watchdog is needed on top of the per-step timeouts.
const RENDER_WATCHDOG_SLACK = 15_000;
const DEFAULT_RENDER_TIMEOUT =
    PAGE_LOAD_TIMEOUT + 2 * REFTEST_WAIT_TIMEOUT + SCREENSHOT_TIMEOUT + RENDER_WATCHDOG_SLACK;
const DEFAULT_CONCURRENCY = 8;
const ALL_SHARDS = -1;              // --shard-index sentinel meaning "all shards"
const DEFAULT_BROWSER_RESTART_LIMIT = 3;

/** Raised when an operation exceeded the time budget a watchdog gave it. */
class StepTimeoutError extends Error {
    constructor(message) {
        super(message);
        this.name = 'StepTimeoutError';
    }
}

/**
 * Run `operation` under a hard deadline, rejecting with StepTimeoutError when it
 * overruns.
 *
 * Playwright's own per-call timeouts cover most of this script — page.goto,
 * page.waitForFunction and page.screenshot each reject once their budget is
 * spent, because the timer that enforces them lives in the *driver*, not in the
 * page. `page.evaluate` is the exception: it takes no timeout at all, so it
 * stays pending for as long as the renderer refuses to answer. A WPT test that
 * wedges its main thread *after* `load` has fired — an infinite loop started
 * from a timer, a runaway requestAnimationFrame chain — therefore navigates
 * fine and then parks the evaluate forever, and with it the worker awaiting it.
 * The remaining workers drain the queue and exit, `Promise.all(workers)` never
 * settles, and the shard sits at "N/M done" until the CI job's own timeout kills
 * it hours later with no output and no results (observed: a shard stuck for two
 * hours after 9000/9399, having never printed the 100% line).
 *
 * So every await that can reach the renderer gets an explicit bound, and the
 * whole per-test render additionally runs under this watchdog — belt and braces,
 * so that a future unbounded call cannot re-introduce a silent multi-hour stall.
 *
 * `operation` is a thunk so that the clock starts with the work rather than at
 * the call site, and so a synchronous throw is reported as a rejection.
 */
function withTimeout(operation, timeoutMs, description) {
    const promise = Promise.resolve().then(operation);
    // Once the watchdog fires the operation is abandoned, but it keeps running
    // and may reject later (typically when its context is torn down). Attach an
    // inert handler so that late rejection cannot surface as an unhandled
    // rejection and take the whole generator down.
    promise.catch(() => {});

    let timer = null;
    return new Promise((resolve, reject) => {
        timer = setTimeout(
            () => reject(new StepTimeoutError(`${description} exceeded ${timeoutMs}ms`)),
            timeoutMs);
        promise.then(resolve, reject);
    }).finally(() => clearTimeout(timer));
}

/** Close a page/context/browser, tolerating both failure and a wedged renderer. */
async function closeQuietly(closeable, description, timeoutMs = CLOSE_TIMEOUT) {
    try {
        await withTimeout(() => closeable.close(), timeoutMs, description);
    } catch {
        // Already gone, or too wedged to tear down cleanly — either way closing
        // the browser at the end of the run reclaims it, and a leaked context is
        // far cheaper than a stalled worker. Cleanup must never block progress.
    }
}

/**
 * Deterministic shard index in [0, shardCount) for a forward-slash relative
 * path, using a 32-bit FNV-1a hash of its UTF-8 bytes.
 *
 * This MUST stay byte-for-byte identical to WptTestRunner.GetShardIndex in
 * src/Broiler.Wpt/WptTestRunner.cs: the C# runner shards the test set the same
 * way, so shard N here generates references for exactly the tests shard N runs
 * there. Drift between the two would silently leave tests without references.
 */
function shardIndexForPath(relativePath, shardCount) {
    let hash = 2166136261;            // FNV offset basis (unsigned 32-bit)
    const bytes = Buffer.from(relativePath, 'utf8');
    for (const byte of bytes) {
        hash ^= byte;
        // Math.imul performs the multiply in 32-bit space; >>> 0 keeps it unsigned.
        hash = Math.imul(hash, 16777619) >>> 0;
    }
    return hash % shardCount;
}

/**
 * WPT crashtests (filename ending in `-crash.{html,htm,xht,xhtml}`) are security
 * regression tests that deliberately try to crash the browser engine.  They
 * are not reftests, never have a reference screenshot, and — by design — kill
 * the Chromium renderer when loaded.  Skip them so they neither waste a render
 * slot nor poison the worker that loads them.
 */
function isCrashTest(name) {
    // Matches `foo-crash.html` as well as flagged variants like
    // `foo-crash.https.html` (WPT appends `.flag` segments before the
    // extension).
    return /-crash(?:\.[^.]+)*\.(?:html|htm|xht|xhtml)$/i.test(name);
}

/** Match Broiler.HTML's conservative definition of a JavaScript-dependent WPT. */
function requiresJavaScript(markup) {
    return /<script\b/i.test(markup) ||
        /\bon[a-z]+\s*=\s*["']/i.test(markup) ||
        /javascript:/i.test(markup) ||
        /testharness\.js|testdriver\.js|reftest-wait/i.test(markup);
}

/** Exclude WPT references, resources, and specification source documents. */
function isNonTestFile(name) {
    const lowerName = name.toLowerCase();
    return /\.src\.(?:html|htm|xht|xhtml)$/.test(lowerName) ||
        /-(?:not)?ref\.(?:html|htm|xht|xhtml)$/.test(lowerName);
}

function createBrowserContextOptions(nonJsOnly) {
    return {
        viewport: VIEWPORT,
        javaScriptEnabled: !nonJsOnly,
    };
}

/** Content-type for a filename extension, for route.fulfill(). */
function contentTypeForExtension(ext) {
    switch (ext.toLowerCase()) {
        case '.css':   return 'text/css; charset=utf-8';
        case '.js':    return 'text/javascript; charset=utf-8';
        case '.html':
        case '.htm':
        case '.xht':
        case '.xhtml': return 'text/html; charset=utf-8';
        case '.svg':   return 'image/svg+xml';
        case '.png':   return 'image/png';
        case '.jpg':
        case '.jpeg':  return 'image/jpeg';
        case '.gif':   return 'image/gif';
        case '.webp':  return 'image/webp';
        case '.ttf':   return 'font/truetype';
        case '.otf':   return 'font/opentype';
        case '.woff':  return 'font/woff';
        case '.woff2': return 'font/woff2';
        default:       return 'application/octet-stream';
    }
}

/**
 * Whether a request path targets a WPT test-harness script (testharness.js,
 * testharnessreport.js, check-layout-th.js, …).
 *
 * Broiler.Wpt's runner does NOT load these: when it sees a `<script src>` whose
 * URL contains "testharness" or "check-layout" it injects lightweight stubs
 * instead (WptTestRunner.ExecuteScriptsWithDom / TestharnessStubs, where e.g.
 * `checkLayout` is a no-op), so the rendered page never contains the harness's
 * results table. The reference generator must render the *same* document, so it
 * likewise refuses to serve the real harness scripts — otherwise Chromium runs
 * the full harness and screenshots a PASS/FAIL results table that the stubbed
 * Broiler side can never reproduce, and every harness-driven test (all of
 * css-grid/parsing, the check-layout grid tests, …) fails on a spurious
 * MissingContent mismatch. The substring predicate mirrors the runner exactly.
 */
function isWptHarnessScript(requestPath) {
    const lower = requestPath.toLowerCase();
    return lower.includes('testharness') || lower.includes('check-layout');
}

/**
 * Hold the screenshot until a `reftest-wait` page says it is ready.
 *
 * A test that marks `<html class="reftest-wait">` is telling the reftest runner
 * *not* to compare at load: it drives the page to the state under test — start a
 * view transition and wait for `transition.ready`, wait for a `transitionend`,
 * settle two animation frames — and then calls `takeScreenshot()`, which removes
 * the class (see /common/reftest-wait.js). Screenshotting at load captures the
 * page *before* any of that, so the golden records the pre-test state and the
 * whole family fails on a reference artefact rather than an engine bug: Broiler
 * drains its event loop before rendering, so it produces the post-signal state
 * the test's own `rel=match` reference describes, and `--verify-reference` flags
 * exactly these as "suspect reference" (most of css-view-transitions).
 *
 * Bounded by <see>REFTEST_WAIT_TIMEOUT</see>: a page that never signals — commonly
 * one whose script aborted because the harness scripts are deliberately not served
 * (see isWptHarnessScript) — is screenshotted in whatever state it reached, which
 * is the same at-load state as before this wait existed. The same bound covers the
 * opening `reftest-wait` probe, which would otherwise be unbounded and hang the
 * whole shard on a page that wedges its main thread (see withTimeout).
 *
 * Measured when this landed: +25 in css/css-view-transitions (313 -> 338 of 490),
 * and exactly one mover across 1598 tests in css/css-position, html/semantics/popovers,
 * html/semantics/interactive-elements/the-dialog-element and css/css-backgrounds — so
 * the new semantics are surgical rather than broad churn. That one mover is
 * css/css-position/overlay/overlay-transition-finished, which screenshots from its
 * `transitionend` handler: its golden is now the post-transition page, and Broiler
 * cannot reach that state because it has no snapshot clock to end a discrete `overlay`
 * transition against (the event loop drains every timer to a fixed point regardless of
 * delay, so a duration cannot be modelled with a timer). Closing that needs the runner
 * to adopt `reftest-wait` as its own snapshot signal, mirroring this side.
 */
async function waitForReftestReady(page, timeoutMs = REFTEST_WAIT_TIMEOUT) {
    const isWaiting = () =>
        !!document.documentElement &&
        document.documentElement.classList.contains('reftest-wait');

    try {
        // page.evaluate takes no timeout of its own — unlike waitForFunction
        // below, it waits on the renderer indefinitely — so it must be bounded
        // here or a page that wedges its main thread parks this worker forever.
        const waiting = await withTimeout(
            () => page.evaluate(isWaiting),
            timeoutMs,
            'reftest-wait probe');
        if (!waiting) {
            return;
        }
        await page.waitForFunction(
            () => !document.documentElement ||
                !document.documentElement.classList.contains('reftest-wait'),
            null,
            { timeout: timeoutMs },
        );
    } catch {
        // Never signalled, too wedged to answer the probe, or the page went away
        // mid-wait — screenshot as-is.
    }
}

/** Navigate to one test, settle any reftest-wait, and capture its reference PNG. */
async function renderTest(page, testFile, outPath) {
    await page.goto(pathToFileURL(testFile).href, {
        waitUntil: 'load',
        timeout: PAGE_LOAD_TIMEOUT,
    });
    await waitForReftestReady(page);
    // Screenshot's timeout is spelled out rather than left to Playwright's
    // ambient 30s default, so the watchdog budget above stays derivable from
    // the constants in this file.
    await page.screenshot({ path: outPath, fullPage: false, timeout: SCREENSHOT_TIMEOUT });
}

/**
 * renderTest under a per-test watchdog: no single test may hold a worker beyond
 * `watchdogMs`, whatever goes wrong inside Playwright. A trip is reported as an
 * ordinary render failure — the test simply gets no reference and the runner
 * reports it as skipped — which is strictly better than stalling the shard.
 */
function renderTestWithWatchdog(page, testFile, outPath, label, watchdogMs) {
    return withTimeout(
        () => renderTest(page, testFile, outPath),
        watchdogMs,
        `rendering ${label}`);
}

function decodeFileUrlPath(requestUrl) {
    const encodedPath = requestUrl.replace(/^file:\/\//i, '').split(/[?#]/)[0];
    try {
        return decodeURIComponent(encodedPath);
    } catch {
        return null;
    }
}

/**
 * Resolve a file:// request URL to the on-disk resource the reference generator
 * should serve for it, or `null` when Chromium should be left to load (or 404)
 * the request itself.
 *
 * WPT tests reference shared support resources — fonts, stylesheets, images,
 * harness scripts — with *root-relative* URLs (/fonts/ahem.css,
 * /css/support/grid.css, /resources/testharness.js). A real WPT server resolves
 * those against the WPT root; loaded over file://, Chromium resolves them
 * against the filesystem root (file:///css/support/grid.css) where nothing
 * exists, so the resource 404s and the reference renders unstyled. This mirrors
 * a real server (and Broiler.Wpt's own runner, TryResolveWptRootRelativePath):
 *   - a WPT harness script (testharness.js, check-layout-th.js, …) → `null`, so
 *     Chromium 404s it and never renders the harness results table the runner's
 *     stubs omit (see isWptHarnessScript);
 *   - a path that resolves on disk as-is (the test document, a relative
 *     sub-resource) → `null` (Chromium loads it directly);
 *   - a root-relative path that resolves under `baseDir` → that path (served),
 *     contained within `baseDir` to reject `../` escapes;
 *   - anything else → `null` (Chromium 404s it, as before).
 */
function resolveRootRelativeResource(baseDir, requestUrl) {
    if (!/^file:\/\//i.test(requestUrl)) {
        return null;
    }
    const rawPath = decodeFileUrlPath(requestUrl);
    if (rawPath === null) {
        return null;
    }
    // Keep the reference in lock-step with the runner, which stubs (never loads)
    // the WPT harness scripts. Serving them here would render a results table
    // Broiler's stubbed render lacks — a guaranteed MissingContent mismatch.
    if (isWptHarnessScript(rawPath)) {
        return null;
    }
    try {
        if (fs.existsSync(rawPath) && fs.statSync(rawPath).isFile()) {
            return null;
        }
    } catch { /* fall through to the base-dir remap */ }

    const resolvedBaseDir = path.resolve(baseDir);
    const rel = rawPath.startsWith('/') ? '.' + rawPath : './' + rawPath;
    const candidate = path.resolve(resolvedBaseDir, rel);
    const contained =
        candidate === resolvedBaseDir || candidate.startsWith(resolvedBaseDir + path.sep);
    try {
        if (contained && fs.existsSync(candidate) && fs.statSync(candidate).isFile()) {
            return candidate;
        }
    } catch { /* fall through — leave it for Chromium to 404 */ }
    return null;
}

/** Recursively discover test files (excluding WPT crashtests). */
function discoverTests(dir) {
    const results = [];
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) {
            if (NON_TEST_DIRECTORIES.has(entry.name.toLowerCase())) {
                continue;
            }
            results.push(...discoverTests(full));
        } else if (
            TEST_EXTENSIONS.has(path.extname(entry.name).toLowerCase()) &&
            !isNonTestFile(entry.name) &&
            !isCrashTest(entry.name)
        ) {
            results.push(full);
        }
    }
    return results;
}

/** Ensure that all ancestor directories of `filePath` exist. */
function ensureDir(filePath) {
    const dir = path.dirname(filePath);
    fs.mkdirSync(dir, { recursive: true });
}

function parsePositiveIntegerEnv(name, fallback, env = process.env) {
    const raw = env[name];
    if (raw === undefined || raw === '') {
        return fallback;
    }

    if (!/^[1-9][0-9]*$/.test(raw)) {
        return fallback;
    }

    const value = Number.parseInt(raw, 10);
    return Number.isInteger(value) && value > 0 ? value : fallback;
}

/**
 * Per-test watchdog budget in milliseconds, overridable (in whole seconds) via
 * BROILER_WPT_REFERENCE_RENDER_TIMEOUT_SECONDS for runners whose pages legitimately
 * render slower than the derived default.
 */
function resolveRenderTimeoutMs(env = process.env) {
    const seconds = parsePositiveIntegerEnv(
        'BROILER_WPT_REFERENCE_RENDER_TIMEOUT_SECONDS', 0, env);
    return seconds > 0 ? seconds * 1000 : DEFAULT_RENDER_TIMEOUT;
}

function isBrowserClosedError(error) {
    const message = String(error && error.message ? error.message : error);
    return /browser (?:has been )?closed|target page, context or browser has been closed|browser disconnected/i.test(message);
}

/**
 * Load the set of forward-slash relative test paths named in a prior JSON report
 * (the incremental-rerun manifest). Used to restrict reference generation to the
 * tests an incremental run will actually re-execute, so references exist for
 * exactly that set — the C# runner's `--rerun-json` filters the same manifest, so
 * the two stay in lock-step.
 *
 * Every entry in the failed-tests manifest is a rerun candidate, so all paths are
 * taken. `relativeTestPath` (already forward-slash, relative to the WPT root) is
 * preferred; `testPath` is a fallback. Returns a Set of normalised relative paths.
 */
function loadRerunTestPaths(rerunJson) {
    const data = JSON.parse(fs.readFileSync(rerunJson, 'utf8'));
    const results = data && Array.isArray(data.results) ? data.results : null;
    if (!results) {
        throw new Error(`rerun report ${rerunJson} has no top-level 'results' array`);
    }
    const paths = new Set();
    for (const result of results) {
        if (!result || typeof result !== 'object') {
            continue;
        }
        const raw = result.relativeTestPath || result.testPath;
        if (typeof raw === 'string' && raw.trim() !== '') {
            paths.add(raw.split(path.sep).join('/').replace(/\\/g, '/'));
        }
    }
    return paths;
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------
async function main(args = process.argv.slice(2)) {
    let testDir = null;
    let outputDir = null;
    let baseDir = null;
    let concurrency = DEFAULT_CONCURRENCY;
    let shardCount = 1;
    let shardIndex = ALL_SHARDS;
    let nonJsOnly = false;
    let rerunJson = null;

    for (let i = 0; i < args.length; i++) {
        if (args[i] === '--concurrency' && i + 1 < args.length) {
            concurrency = parseInt(args[++i], 10) || DEFAULT_CONCURRENCY;
        } else if (args[i] === '--base-dir' && i + 1 < args.length) {
            baseDir = args[++i];
        } else if (args[i] === '--shard-count' && i + 1 < args.length) {
            shardCount = parseInt(args[++i], 10);
        } else if (args[i] === '--shard-index' && i + 1 < args.length) {
            shardIndex = parseInt(args[++i], 10);
        } else if (args[i] === '--rerun-json' && i + 1 < args.length) {
            rerunJson = args[++i];
        } else if (args[i] === '--non-js') {
            nonJsOnly = true;
        } else if (!testDir) {
            testDir = args[i];
        } else if (!outputDir) {
            outputDir = args[i];
        }
    }

    if (!testDir || !outputDir) {
        console.error('Usage: node generate-wpt-references.js <test-dir> <output-dir> [--concurrency N] [--base-dir <dir>] [--shard-count N --shard-index I] [--non-js]');
        process.exit(1);
    }

    if (!Number.isInteger(shardCount) || shardCount < 1) {
        console.error(`Error: --shard-count must be a positive integer (got ${shardCount}).`);
        process.exit(1);
    }
    if (!Number.isInteger(shardIndex) || (shardIndex !== ALL_SHARDS && (shardIndex < 0 || shardIndex >= shardCount))) {
        console.error(`Error: --shard-index must be ${ALL_SHARDS} (all) or between 0 and ${shardCount - 1} (got ${shardIndex}).`);
        process.exit(1);
    }

    testDir = path.resolve(testDir);
    outputDir = path.resolve(outputDir);
    // When --base-dir is provided, compute output paths relative to it
    // instead of testDir.  This ensures that when generating references for
    // a subset directory, the output mirrors the full directory hierarchy
    // expected by the C# WptTestRunner.
    baseDir = baseDir ? path.resolve(baseDir) : testDir;

    if (!fs.existsSync(testDir)) {
        console.error(`Error: test directory not found: ${testDir}`);
        process.exit(1);
    }

    console.log(`Discovering test files in: ${testDir}`);
    let testFiles = discoverTests(testDir);
    console.log(`Found ${testFiles.length} test files`);

    if (nonJsOnly) {
        const beforeFilter = testFiles.length;
        testFiles = testFiles.filter((testFile) =>
            !requiresJavaScript(fs.readFileSync(testFile, 'utf8')));
        console.log(`Non-JS mode: selected ${testFiles.length} files; skipped ${beforeFilter - testFiles.length} JavaScript-dependent files`);
    }

    // Incremental rerun: restrict generation to the tests named in the prior
    // manifest, matching the C# runner's own `--rerun-json` filter. Applied before
    // sharding (like the runner) so shard N still generates exactly the rerun tests
    // it will execute. This is what makes an incremental run correct: without it
    // the run reused a cached reference set that may be absent, so every reran test
    // was skipped as "missing reference image".
    if (rerunJson) {
        let rerunPaths;
        try {
            rerunPaths = loadRerunTestPaths(rerunJson);
        } catch (error) {
            console.error(`Error: ${error.message}`);
            process.exit(1);
        }
        const beforeRerun = testFiles.length;
        testFiles = testFiles.filter((testFile) => {
            const relative = path.relative(baseDir, testFile).split(path.sep).join('/');
            return rerunPaths.has(relative);
        });
        console.log(`Rerun mode: selected ${testFiles.length} of ${beforeRerun} files present in ${rerunJson}`);
    }

    // When sharding, keep only the files assigned to this shard by the same
    // FNV-1a(relative-path) % shardCount rule the C# runner uses, so this shard
    // generates references for exactly the tests it will later execute.
    if (shardIndex !== ALL_SHARDS && shardCount > 1) {
        testFiles = testFiles.filter((testFile) => {
            const relative = path.relative(baseDir, testFile).split(path.sep).join('/');
            return shardIndexForPath(relative, shardCount) === shardIndex;
        });
        console.log(`Shard ${shardIndex + 1}/${shardCount}: ${testFiles.length} test files in this shard`);
    }

    if (testFiles.length === 0) {
        console.log('Nothing to do.');
        process.exit(0);
    }

    fs.mkdirSync(outputDir, { recursive: true });

    console.log(`Launching Chromium (concurrency=${concurrency}) …`);
    const { chromium } = require('playwright');
    async function launchBrowser() {
        return chromium.launch({
            headless: true,
            ...(process.env.BROILER_CHROMIUM_PATH ? { executablePath: process.env.BROILER_CHROMIUM_PATH } : {}),
            // Allow file:// pages to load other file:// resources (e.g. SVG images
            // referenced via <img src="support/...">) which Chrome blocks by default.
            args: ['--allow-file-access-from-files'],
        });
    }

    let browser = await launchBrowser();
    let browserRestartPromise = null;
    let browserRestartCount = 0;
    const browserRestartLimit = parsePositiveIntegerEnv(
        'BROILER_WPT_REFERENCE_BROWSER_RESTARTS',
        DEFAULT_BROWSER_RESTART_LIMIT);
    const renderTimeout = resolveRenderTimeoutMs();

    async function restartBrowser(reason) {
        if (browserRestartPromise !== null) {
            await browserRestartPromise;
            return;
        }

        browserRestartPromise = (async () => {
            browserRestartCount++;
            if (browserRestartCount > browserRestartLimit) {
                throw new Error(`Chromium closed too many times while generating WPT references (limit ${browserRestartLimit}). Last failure: ${reason}`);
            }

            console.error(`  ⚠ Restarting Chromium after browser closure (${browserRestartCount}/${browserRestartLimit}): ${reason}`);
            try { await browser.close(); } catch { /* browser already gone */ }
            browser = await launchBrowser();
        })();

        try {
            await browserRestartPromise;
        } finally {
            browserRestartPromise = null;
        }
    }

    // WPT tests reference their support resources — fonts, shared stylesheets,
    // images, harness scripts — via *root-relative* URLs such as /fonts/ahem.css,
    // /css/support/grid.css, or /resources/testharness.js.  A real WPT server
    // resolves those against the WPT root.  When a test is loaded over file://,
    // Chromium instead resolves them against the filesystem root
    // (file:///css/support/grid.css), where nothing exists — so the resource
    // silently 404s and the reference renders *unstyled* (e.g. a grid test whose
    // display:grid + track colours live in /css/support/grid.css screenshots
    // blank).  Broiler.Wpt's own runner already remaps these to the WPT root
    // (TryResolveWptRootRelativePath); the reference generator must do the same
    // or the two sides render different documents and every such test fails on a
    // spurious pixel mismatch.  Intercept file:// requests: paths that resolve on
    // disk as-is (the test document and its relative sub-resources) load
    // directly; a root-relative path that does not resolve is served from
    // {baseDir}, contained within it to guard against ../ escapes.
    const resolvedBaseDir = path.resolve(baseDir);
    console.log(`Serving root-relative WPT resources from: ${resolvedBaseDir}`);

    let completed = 0;
    let errors = 0;
    const total = testFiles.length;

    async function fileRouteHandler(route) {
        const served = resolveRootRelativeResource(resolvedBaseDir, route.request().url());
        if (served === null) {
            // The test document, a resolvable relative sub-resource, or an
            // unmappable path — let Chromium load (or 404) it directly.
            return route.continue();
        }
        await route.fulfill({
            status: 200,
            contentType: contentTypeForExtension(path.extname(served)),
            body: fs.readFileSync(served),
        });
    }

    // Create a fresh context + page, registering the resource route.  Used both
    // for a worker's initial page and to recover after a renderer crash.
    // Bounded like every other await that can reach the browser: a wedged
    // browser process must fail the shard with a diagnosable error rather than
    // park a worker (and with it Promise.all) indefinitely.
    async function newRenderTarget() {
        return withTimeout(buildRenderTarget, CONTEXT_SETUP_TIMEOUT, 'creating a render context');
    }

    async function buildRenderTarget() {
        if (browserRestartPromise !== null) {
            await browserRestartPromise;
        }
        if (!browser.isConnected()) {
            await restartBrowser('browser disconnected before creating a render context');
        }

        try {
            const context = await browser.newContext(createBrowserContextOptions(nonJsOnly));
            await context.route(/^file:\/\//i, fileRouteHandler);
            const page = await context.newPage();
            return { context, page };
        } catch (err) {
            if (!isBrowserClosedError(err)) {
                throw err;
            }

            await restartBrowser(err.message || err);
            const context = await browser.newContext(createBrowserContextOptions(nonJsOnly));
            await context.route(/^file:\/\//i, fileRouteHandler);
            const page = await context.newPage();
            return { context, page };
        }
    }

    // Worker function — processes one file at a time from the queue.
    async function worker(queue) {
        let { context, page } = await newRenderTarget();

        while (queue.length > 0) {
            const testFile = queue.pop();
            const relative = path.relative(baseDir, testFile);
            const outPath = path.join(
                outputDir,
                relative.replace(/\.[^.]+$/, '.png'),
            );
            const rel = path.relative(testDir, testFile);

            try {
                ensureDir(outPath);
                await renderTestWithWatchdog(page, testFile, outPath, rel, renderTimeout);
            } catch (err) {
                // Log the failure path for diagnostics; the file will be
                // reported as "skipped" by the Broiler.Wpt runner. A tripped
                // watchdog is always reported, however many ordinary failures
                // preceded it: it marks a test that would once have hung the
                // shard, and the throttle below could otherwise swallow the only
                // clue to a stall.
                if (err instanceof StepTimeoutError || errors === 0 || errors % 100 === 0) {
                    console.error(`  ⚠ Failed: ${rel}: ${err.message || err}`);
                }
                errors++;

                // Rebuild the render target after every failed navigation or
                // screenshot. Timeouts can leave a page stuck in recursive
                // loading, and renderer crashes leave it permanently dead.
                await closeQuietly(context, `closing context after ${rel}`);
                try {
                    ({ context, page } = await newRenderTarget());
                } catch (rebuildErr) {
                    console.error(`  ⚠ Failed to recover worker after render failure: ${rebuildErr.message || rebuildErr}`);
                    break;   // fail the shard below instead of silently draining half of it.
                }
            }

            completed++;
            if (completed % 500 === 0 || completed === total) {
                const pct = ((completed / total) * 100).toFixed(1);
                console.log(`  [${pct}%] ${completed}/${total} done (${errors} errors)`);
            }
        }

        await closeQuietly(page, 'closing worker page');
        await closeQuietly(context, 'closing worker context');
    }

    // Shallow-copy as a mutable queue (pop from end is O(1)).
    const queue = [...testFiles];

    // Launch workers.
    const workers = [];
    for (let i = 0; i < Math.min(concurrency, total); i++) {
        workers.push(worker(queue));
    }
    await Promise.all(workers);

    await closeQuietly(browser, 'closing browser');

    console.log();
    console.log(`Reference generation complete: ${completed} files, ${errors} errors`);
    console.log(`Output: ${outputDir}`);

    if (completed !== total) {
        throw new Error(`Reference generation stopped early: completed ${completed}/${total} files. Chromium likely closed while workers still had queued tests.`);
    }
}

module.exports = {
    DEFAULT_RENDER_TIMEOUT,
    StepTimeoutError,
    closeQuietly,
    contentTypeForExtension,
    createBrowserContextOptions,
    discoverTests,
    isNonTestFile,
    isWptHarnessScript,
    main,
    isBrowserClosedError,
    loadRerunTestPaths,
    parsePositiveIntegerEnv,
    renderTestWithWatchdog,
    requiresJavaScript,
    resolveRenderTimeoutMs,
    resolveRootRelativeResource,
    shardIndexForPath,
    waitForReftestReady,
    withTimeout,
};

if (require.main === module) {
    main().catch((error) => {
        console.error(error && error.stack ? error.stack : error);
        process.exitCode = 1;
    });
}
