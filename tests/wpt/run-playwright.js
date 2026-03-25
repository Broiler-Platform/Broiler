#!/usr/bin/env node
// run-playwright.js — Capture reference output from wpt.live using Playwright
//
// Usage:
//   node run-playwright.js [--urls <manifest>] [--output-dir <dir>] [--timeout <ms>]
//
// For each URL in the manifest the script launches a headless Chromium page,
// waits for the WPT test-harness to finish (or a timeout), and writes:
//   <output-dir>/<test-id>.txt   — visible text content of <body>
//   <output-dir>/<test-id>.html  — full outer-HTML of the document

"use strict";

const { chromium } = require("playwright");
const fs = require("fs");
const path = require("path");

// ── Defaults ────────────────────────────────────────────────────────────
const DEFAULT_MANIFEST = path.join(__dirname, "wpt-urls.txt");
const DEFAULT_OUTPUT = path.join(__dirname, "results", "playwright");
const DEFAULT_TIMEOUT = 30000; // 30 s per test

// ── Argument parsing ────────────────────────────────────────────────────
function parseArgs(argv) {
  const args = { manifest: DEFAULT_MANIFEST, outputDir: DEFAULT_OUTPUT, timeout: DEFAULT_TIMEOUT };
  for (let i = 2; i < argv.length; i++) {
    switch (argv[i]) {
      case "--urls":
        args.manifest = argv[++i];
        break;
      case "--output-dir":
        args.outputDir = argv[++i];
        break;
      case "--timeout":
        args.timeout = parseInt(argv[++i], 10);
        break;
      case "--help":
        console.log("Usage: node run-playwright.js [--urls <manifest>] [--output-dir <dir>] [--timeout <ms>]");
        process.exit(0);
    }
  }
  return args;
}

// ── Manifest reader ─────────────────────────────────────────────────────
function readManifest(filepath) {
  return fs
    .readFileSync(filepath, "utf8")
    .split("\n")
    .map((l) => l.trim())
    .filter((l) => l && !l.startsWith("#"))
    .map((l) => {
      const [category, id, url] = l.split("|");
      return { category, id, url };
    });
}

// ── Main ────────────────────────────────────────────────────────────────
async function main() {
  const args = parseArgs(process.argv);
  const entries = readManifest(args.manifest);

  fs.mkdirSync(args.outputDir, { recursive: true });

  console.log(`Playwright runner — ${entries.length} tests, timeout ${args.timeout} ms`);

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1024, height: 768 },
    ignoreHTTPSErrors: true,
  });

  const summary = [];

  for (const entry of entries) {
    const label = `[${entry.category}] ${entry.id}`;
    process.stdout.write(`  ${label} ... `);

    const page = await context.newPage();
    let status = "OK";
    let errorMsg = "";

    try {
      await page.goto(entry.url, { waitUntil: "networkidle", timeout: args.timeout });

      // Wait for WPT test-harness completion signal or DOM stability.
      try {
        await page.waitForSelector("#results, .pass, .fail, #summary", { timeout: 10000 });
      } catch {
        // Not all pages use testharness.js — fall back to load-state check.
        await page.waitForLoadState("domcontentloaded");
      }

      const textContent = await page.evaluate(() => {
        const body = document.body;
        return body ? body.innerText || body.textContent || "" : "";
      });

      const htmlContent = await page.evaluate(() => document.documentElement.outerHTML);

      const txtPath = path.join(args.outputDir, `${entry.id}.txt`);
      const htmlPath = path.join(args.outputDir, `${entry.id}.html`);
      fs.writeFileSync(txtPath, textContent, "utf8");
      fs.writeFileSync(htmlPath, htmlContent, "utf8");
    } catch (err) {
      status = "ERROR";
      errorMsg = err.message.split("\n")[0];
    } finally {
      await page.close();
    }

    summary.push({ ...entry, status, error: errorMsg });
    console.log(status === "OK" ? "OK" : `ERROR: ${errorMsg}`);
  }

  await browser.close();

  // Write machine-readable summary
  const summaryPath = path.join(args.outputDir, "_summary.json");
  fs.writeFileSync(summaryPath, JSON.stringify(summary, null, 2), "utf8");

  const ok = summary.filter((s) => s.status === "OK").length;
  const fail = summary.filter((s) => s.status !== "OK").length;
  console.log(`\nDone — ${ok} OK, ${fail} errors.  Summary → ${summaryPath}`);
}

main().catch((err) => {
  console.error("Fatal:", err);
  process.exit(1);
});
