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

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Recursively discover test files. */
function discoverTests(dir) {
    const results = [];
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const full = path.join(dir, entry.name);
        if (entry.isDirectory()) {
            results.push(...discoverTests(full));
        } else if (TEST_EXTENSIONS.has(path.extname(entry.name).toLowerCase())) {
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
    const browser = await chromium.launch({ headless: true });

    let completed = 0;
    let errors = 0;
    const total = testFiles.length;

    // Worker function — processes one file at a time from the queue.
    async function worker(queue) {
        const context = await browser.newContext({ viewport: VIEWPORT });
        const page = await context.newPage();

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
            }

            completed++;
            if (completed % 500 === 0 || completed === total) {
                const pct = ((completed / total) * 100).toFixed(1);
                console.log(`  [${pct}%] ${completed}/${total} done (${errors} errors)`);
            }
        }

        await page.close();
        await context.close();
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
