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

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------
const TEST_EXTENSIONS = new Set(['.html', '.htm', '.xhtml']);
const VIEWPORT = { width: 1024, height: 768 };
const PAGE_LOAD_TIMEOUT = 10_000;   // ms — max time to wait for page load
const DEFAULT_CONCURRENCY = 8;

/**
 * WPT crashtests (filename ending in `-crash.{html,htm,xhtml}`) are security
 * regression tests that deliberately try to crash the browser engine.  They
 * are not reftests, never have a reference screenshot, and — by design — kill
 * the Chromium renderer when loaded.  Skip them so they neither waste a render
 * slot nor poison the worker that loads them.
 */
function isCrashTest(name) {
    // Matches `foo-crash.html` as well as flagged variants like
    // `foo-crash.https.html` (WPT appends `.flag` segments before the
    // extension).
    return /-crash(?:\.[^.]+)*\.(?:html|htm|xhtml)$/i.test(name);
}

/** Recursively discover test files (excluding WPT crashtests). */
function discoverTests(dir) {
    const results = [];
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) {
            results.push(...discoverTests(full));
        } else if (
            TEST_EXTENSIONS.has(path.extname(entry.name).toLowerCase()) &&
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

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------
(async () => {
    const args = process.argv.slice(2);
    let testDir = null;
    let outputDir = null;
    let baseDir = null;
    let concurrency = DEFAULT_CONCURRENCY;

    for (let i = 0; i < args.length; i++) {
        if (args[i] === '--concurrency' && i + 1 < args.length) {
            concurrency = parseInt(args[++i], 10) || DEFAULT_CONCURRENCY;
        } else if (args[i] === '--base-dir' && i + 1 < args.length) {
            baseDir = args[++i];
        } else if (!testDir) {
            testDir = args[i];
        } else if (!outputDir) {
            outputDir = args[i];
        }
    }

    if (!testDir || !outputDir) {
        console.error('Usage: node generate-wpt-references.js <test-dir> <output-dir> [--concurrency N] [--base-dir <dir>]');
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
    const testFiles = discoverTests(testDir);
    console.log(`Found ${testFiles.length} test files`);

    if (testFiles.length === 0) {
        console.log('Nothing to do.');
        process.exit(0);
    }

    fs.mkdirSync(outputDir, { recursive: true });

    console.log(`Launching Chromium (concurrency=${concurrency}) …`);
    const browser = await chromium.launch({
        headless: true,
        // Allow file:// pages to load other file:// resources (e.g. SVG images
        // referenced via <img src="support/...">) which Chrome blocks by default.
        args: ['--allow-file-access-from-files'],
    });

    // Determine if there is a local fonts/ directory to serve.
    // WPT tests import fonts via root-relative URLs such as /fonts/ahem.css.
    // When loaded from file://, these become file:///fonts/... which do not
    // exist on the filesystem.  We intercept those requests and serve them
    // from {baseDir}/fonts/ so Chromium uses the correct test fonts.
    const localFontsDir = path.join(baseDir, 'fonts');
    const hasFontsDir = fs.existsSync(localFontsDir);
    if (hasFontsDir) {
        console.log(`Serving WPT fonts from: ${localFontsDir}`);
    }

    let completed = 0;
    let errors = 0;
    const total = testFiles.length;

    // Intercept root-relative /fonts/ requests and serve them from the
    // local fonts directory.  WPT tests use paths like /fonts/ahem.css
    // which a real WPT server resolves relative to the WPT root.  When
    // loading via file://, Chrome resolves them as file:///fonts/... which
    // does not exist on disk.  Playwright can intercept these requests via
    // context.route() for both http:// and file:// origin requests.
    async function fontRouteHandler(route) {
        const url = route.request().url();
        // Strip the file:///fonts/ prefix to get the filename/subpath.
        const subpath = decodeURIComponent(url.replace(/^file:\/\/\/fonts\//i, ''));
        const localPath = path.join(localFontsDir, subpath);
        if (fs.existsSync(localPath)) {
            const body = fs.readFileSync(localPath);
            const ext = path.extname(localPath).toLowerCase();
            const contentType =
                ext === '.css'  ? 'text/css; charset=utf-8' :
                ext === '.ttf'  ? 'font/truetype' :
                ext === '.otf'  ? 'font/opentype' :
                ext === '.woff' ? 'font/woff' :
                ext === '.woff2'? 'font/woff2' :
                'application/octet-stream';
            await route.fulfill({ status: 200, contentType, body });
        } else {
            await route.abort('failed');
        }
    }

    // Create a fresh context + page, registering the font route.  Used both
    // for a worker's initial page and to recover after a renderer crash.
    async function newRenderTarget() {
        const context = await browser.newContext({ viewport: VIEWPORT });
        if (hasFontsDir) {
            await context.route(/file:\/\/\/fonts\//i, fontRouteHandler);
        }
        const page = await context.newPage();
        return { context, page };
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
                const fileUrl = 'file://' + testFile;
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

                // A renderer crash leaves `page` permanently dead: every
                // subsequent goto on it throws "Page crashed", so a single
                // crashing test would cascade into hundreds of false failures
                // for unrelated files this worker later picks up.  Rebuild the
                // context+page so the worker recovers and keeps going.
                const crashed = page.isClosed() ||
                    /crash|Target (?:closed|page).*closed|browser has been closed/i.test(String(err && err.message));
                if (crashed) {
                    try { await context.close(); } catch { /* already gone */ }
                    try {
                        ({ context, page } = await newRenderTarget());
                    } catch (rebuildErr) {
                        console.error(`  ⚠ Failed to recover worker after crash: ${rebuildErr.message || rebuildErr}`);
                        break;   // browser itself is unusable; let other workers finish.
                    }
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

    await browser.close();

    console.log();
    console.log(`Reference generation complete: ${completed} files, ${errors} errors`);
    console.log(`Output: ${outputDir}`);
})();
