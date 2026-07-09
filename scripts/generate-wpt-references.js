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
const DEFAULT_CONCURRENCY = 8;
const ALL_SHARDS = -1;              // --shard-index sentinel meaning "all shards"
const DEFAULT_BROWSER_RESTART_LIMIT = 3;

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

function parsePositiveIntegerEnv(name, fallback) {
    const raw = process.env[name];
    if (raw === undefined || raw === '') {
        return fallback;
    }

    if (!/^[1-9][0-9]*$/.test(raw)) {
        return fallback;
    }

    const value = Number.parseInt(raw, 10);
    return Number.isInteger(value) && value > 0 ? value : fallback;
}

function isBrowserClosedError(error) {
    const message = String(error && error.message ? error.message : error);
    return /browser (?:has been )?closed|target page, context or browser has been closed|browser disconnected/i.test(message);
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

    for (let i = 0; i < args.length; i++) {
        if (args[i] === '--concurrency' && i + 1 < args.length) {
            concurrency = parseInt(args[++i], 10) || DEFAULT_CONCURRENCY;
        } else if (args[i] === '--base-dir' && i + 1 < args.length) {
            baseDir = args[++i];
        } else if (args[i] === '--shard-count' && i + 1 < args.length) {
            shardCount = parseInt(args[++i], 10);
        } else if (args[i] === '--shard-index' && i + 1 < args.length) {
            shardIndex = parseInt(args[++i], 10);
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
            executablePath: '/usr/bin/chromium',
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
    async function newRenderTarget() {
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

            try {
                ensureDir(outPath);
                const fileUrl = pathToFileURL(testFile).href;
                await page.goto(fileUrl, {
                    waitUntil: 'load',
                    timeout: PAGE_LOAD_TIMEOUT,
                });
                await page.screenshot({ path: outPath, fullPage: false });
            } catch (err) {
                // Log the failure path for diagnostics; the file will be
                // reported as "skipped" by the Broiler.Wpt runner.
                if (errors === 0 || errors % 100 === 0) {
                    const rel = path.relative(testDir, testFile);
                    console.error(`  ⚠ Failed: ${rel}: ${err.message || err}`);
                }
                errors++;

                // Rebuild the render target after every failed navigation or
                // screenshot. Timeouts can leave a page stuck in recursive
                // loading, and renderer crashes leave it permanently dead.
                try { await context.close(); } catch { /* already gone */ }
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

        try { await page.close(); } catch { /* may already be closed */ }
        try { await context.close(); } catch { /* may already be closed */ }
    }

    // Shallow-copy as a mutable queue (pop from end is O(1)).
    const queue = [...testFiles];

    // Launch workers.
    const workers = [];
    for (let i = 0; i < Math.min(concurrency, total); i++) {
        workers.push(worker(queue));
    }
    await Promise.all(workers);

    try { await browser.close(); } catch { /* may already be closed */ }

    console.log();
    console.log(`Reference generation complete: ${completed} files, ${errors} errors`);
    console.log(`Output: ${outputDir}`);

    if (completed !== total) {
        throw new Error(`Reference generation stopped early: completed ${completed}/${total} files. Chromium likely closed while workers still had queued tests.`);
    }
}

module.exports = {
    contentTypeForExtension,
    createBrowserContextOptions,
    discoverTests,
    isNonTestFile,
    isWptHarnessScript,
    main,
    isBrowserClosedError,
    parsePositiveIntegerEnv,
    requiresJavaScript,
    resolveRootRelativeResource,
    shardIndexForPath,
};

if (require.main === module) {
    main().catch((error) => {
        console.error(error && error.stack ? error.stack : error);
        process.exitCode = 1;
    });
}
