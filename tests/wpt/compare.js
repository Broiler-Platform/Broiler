#!/usr/bin/env node
// compare.js — Compare Broiler CLI and Playwright WPT outputs and generate a
//              summary report.
//
// Usage:
//   node compare.js [--broiler-dir <dir>] [--playwright-dir <dir>]
//                   [--output <report.md>] [--json <report.json>]
//
// Reads _summary.json from both directories, then for every test that appears
// in at least one summary it:
//   1. Checks whether the test ran at all in each engine.
//   2. Compares the .txt output (normalised) and computes a similarity score.
//   3. Classifies the result as MATCH / DIFF / BROILER_FAIL / PLAYWRIGHT_FAIL / BOTH_FAIL.
//   4. Writes a Markdown + JSON report.

"use strict";

const fs = require("fs");
const path = require("path");

// ── Defaults ────────────────────────────────────────────────────────────
const SCRIPT_DIR = __dirname;
const DEFAULT_BROILER_DIR = path.join(SCRIPT_DIR, "results", "broiler");
const DEFAULT_PW_DIR = path.join(SCRIPT_DIR, "results", "playwright");
const DEFAULT_MD = path.join(SCRIPT_DIR, "results", "wpt-report.md");
const DEFAULT_JSON = path.join(SCRIPT_DIR, "results", "wpt-report.json");

// ── Args ────────────────────────────────────────────────────────────────
function parseArgs(argv) {
  const a = {
    broilerDir: DEFAULT_BROILER_DIR,
    playwrightDir: DEFAULT_PW_DIR,
    mdPath: DEFAULT_MD,
    jsonPath: DEFAULT_JSON,
  };
  for (let i = 2; i < argv.length; i++) {
    switch (argv[i]) {
      case "--broiler-dir":   a.broilerDir = argv[++i]; break;
      case "--playwright-dir": a.playwrightDir = argv[++i]; break;
      case "--output":        a.mdPath = argv[++i]; break;
      case "--json":          a.jsonPath = argv[++i]; break;
      case "--help":
        console.log("Usage: node compare.js [--broiler-dir <dir>] [--playwright-dir <dir>] [--output <report.md>] [--json <report.json>]");
        process.exit(0);
    }
  }
  return a;
}

// ── Utilities ───────────────────────────────────────────────────────────
function normalise(text) {
  return text
    .replace(/\r\n/g, "\n")
    .replace(/[ \t]+/g, " ")
    .replace(/\n{2,}/g, "\n")
    .trim()
    .toLowerCase();
}

/** Compute similarity ratio between two strings (0..1). */
function similarity(a, b) {
  if (a === b) return 1.0;
  if (!a || !b) return 0.0;
  const linesA = a.split("\n");
  const linesB = new Set(b.split("\n"));
  const common = linesA.filter((l) => linesB.has(l)).length;
  return common / Math.max(linesA.length, linesB.size);
}

function readJSON(p) {
  try {
    return JSON.parse(fs.readFileSync(p, "utf8"));
  } catch {
    return [];
  }
}

function readText(p) {
  try {
    return fs.readFileSync(p, "utf8");
  } catch {
    return null;
  }
}

// ── Main ────────────────────────────────────────────────────────────────
function main() {
  const args = parseArgs(process.argv);

  const bSummary = readJSON(path.join(args.broilerDir, "_summary.json"));
  const pSummary = readJSON(path.join(args.playwrightDir, "_summary.json"));

  // Merge test IDs from both summaries
  const byId = new Map();
  for (const e of [...bSummary, ...pSummary]) {
    if (!byId.has(e.id)) byId.set(e.id, { category: e.category, id: e.id, url: e.url });
  }

  const bStatus = new Map(bSummary.map((e) => [e.id, e]));
  const pStatus = new Map(pSummary.map((e) => [e.id, e]));

  const results = [];

  for (const [id, meta] of byId) {
    const bOk = bStatus.get(id)?.status === "OK";
    const pOk = pStatus.get(id)?.status === "OK";

    let classification;
    let sim = 0;
    let excerpt = "";

    if (!bOk && !pOk) {
      classification = "BOTH_FAIL";
    } else if (!bOk) {
      classification = "BROILER_FAIL";
    } else if (!pOk) {
      classification = "PLAYWRIGHT_FAIL";
    } else {
      // Both succeeded — compare text outputs
      const bText = normalise(readText(path.join(args.broilerDir, `${id}.txt`)) || "");
      const pText = normalise(readText(path.join(args.playwrightDir, `${id}.txt`)) || "");

      sim = similarity(bText, pText);

      if (sim >= 0.95) {
        classification = "MATCH";
      } else {
        classification = "DIFF";
        // Produce a short excerpt of divergence
        const bLines = bText.split("\n").slice(0, 10);
        const pLines = pText.split("\n").slice(0, 10);
        excerpt = `Broiler (first lines): ${bLines.join(" | ").substring(0, 200)}\nPlaywright (first lines): ${pLines.join(" | ").substring(0, 200)}`;
      }
    }

    results.push({ ...meta, classification, similarity: Math.round(sim * 100), excerpt });
  }

  // ── Produce Markdown report ─────────────────────────────────────────
  const counts = { MATCH: 0, DIFF: 0, BROILER_FAIL: 0, PLAYWRIGHT_FAIL: 0, BOTH_FAIL: 0 };
  results.forEach((r) => counts[r.classification]++);

  const lines = [];
  lines.push("# WPT Comparison Report — Broiler CLI vs Playwright");
  lines.push("");
  lines.push(`**Date:** ${new Date().toISOString()}`);
  lines.push(`**Tests:** ${results.length}`);
  lines.push("");
  lines.push("## Summary");
  lines.push("");
  lines.push(`| Status | Count |`);
  lines.push(`|--------|-------|`);
  lines.push(`| ✅ MATCH (≥95 % similar) | ${counts.MATCH} |`);
  lines.push(`| ⚠️ DIFF (< 95 % similar) | ${counts.DIFF} |`);
  lines.push(`| ❌ BROILER_FAIL | ${counts.BROILER_FAIL} |`);
  lines.push(`| 🟡 PLAYWRIGHT_FAIL | ${counts.PLAYWRIGHT_FAIL} |`);
  lines.push(`| 🔴 BOTH_FAIL | ${counts.BOTH_FAIL} |`);
  lines.push("");

  // ── Detail tables per classification ────────────────────────────────
  for (const cls of ["MATCH", "DIFF", "BROILER_FAIL", "PLAYWRIGHT_FAIL", "BOTH_FAIL"]) {
    const subset = results.filter((r) => r.classification === cls);
    if (subset.length === 0) continue;
    lines.push(`## ${cls} (${subset.length})`);
    lines.push("");
    lines.push("| Category | Test ID | Similarity | URL |");
    lines.push("|----------|---------|------------|-----|");
    for (const r of subset) {
      lines.push(`| ${r.category} | ${r.id} | ${r.similarity} % | [link](${r.url}) |`);
    }
    lines.push("");
  }

  // ── Diff excerpts ──────────────────────────────────────────────────
  const diffs = results.filter((r) => r.classification === "DIFF" && r.excerpt);
  if (diffs.length > 0) {
    lines.push("## Diff Excerpts");
    lines.push("");
    for (const d of diffs) {
      lines.push(`### ${d.id}`);
      lines.push("");
      lines.push("```");
      lines.push(d.excerpt);
      lines.push("```");
      lines.push("");
    }
  }

  fs.mkdirSync(path.dirname(args.mdPath), { recursive: true });
  fs.writeFileSync(args.mdPath, lines.join("\n"), "utf8");
  fs.writeFileSync(args.jsonPath, JSON.stringify(results, null, 2), "utf8");

  console.log("WPT Comparison Report");
  console.log("=====================");
  console.log(`  MATCH:           ${counts.MATCH}`);
  console.log(`  DIFF:            ${counts.DIFF}`);
  console.log(`  BROILER_FAIL:    ${counts.BROILER_FAIL}`);
  console.log(`  PLAYWRIGHT_FAIL: ${counts.PLAYWRIGHT_FAIL}`);
  console.log(`  BOTH_FAIL:       ${counts.BOTH_FAIL}`);
  console.log(`\nMarkdown report → ${args.mdPath}`);
  console.log(`JSON report     → ${args.jsonPath}`);
}

main();
