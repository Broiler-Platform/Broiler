// run-octane.mjs — run the Octane 2.0 benchmark suite under Chromium (V8 via
// Playwright) and/or Broiler (the BroilerJS --script-host shell), then emit a
// per-engine result file and a comparison report.
//
// Each Octane suite is executed in isolation — a fresh Chromium page or a fresh
// Broiler process — so a crash, hang, or error in one suite never discards the
// others. This matters because Broiler is an experimental engine: some suites
// score, some throw a catchable error, and some abort the whole process.
//
// Usage:
//   node run-octane.mjs --octane-dir <dir> [options]
//
// Options:
//   --octane-dir <dir>     Octane checkout (contains base.js, richards.js, …). Required.
//   --engines <list>       Comma-separated: chromium,broiler (default: chromium,broiler)
//   --broiler-dll <path>   BroilerJS.dll to run with `dotnet ... --script-host`.
//                          Required when broiler is in --engines.
//   --out-dir <dir>        Where result JSON/MD are written (default: tests/octane/results)
//   --suites <path>        Suite manifest (default: scripts/octane-suites.json)
//   --runner <path>        Shared runner JS (default: scripts/octane-runner.js)
//   --timeout <sec>        Per-suite timeout in seconds (default: 180)

import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..');

function parseArgs(argv) {
  const opts = {
    octaneDir: null,
    engines: 'chromium,broiler',
    broilerDll: null,
    outDir: join(REPO_ROOT, 'tests', 'octane', 'results'),
    suites: join(__dirname, 'octane-suites.json'),
    runner: join(__dirname, 'octane-runner.js'),
    timeout: 180,
  };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const next = () => argv[++i];
    switch (a) {
      case '--octane-dir': opts.octaneDir = next(); break;
      case '--engines': opts.engines = next(); break;
      case '--broiler-dll': opts.broilerDll = next(); break;
      case '--out-dir': opts.outDir = next(); break;
      case '--suites': opts.suites = next(); break;
      case '--runner': opts.runner = next(); break;
      case '--timeout': opts.timeout = parseInt(next(), 10); break;
      default: throw new Error(`Unknown argument: ${a}`);
    }
  }
  if (!opts.octaneDir) throw new Error('--octane-dir is required');
  return opts;
}

// Pull the numeric benchmark scores out of a raw Octane result object,
// classifying the suite as ok/error and computing its geometric mean.
function normalizeRaw(raw) {
  const benchmarks = {};
  const errors = {};
  for (const [k, v] of Object.entries(raw)) {
    if (k.startsWith('__')) continue;
    if (typeof v === 'string' && v.startsWith('ERROR:')) {
      errors[k] = v.slice('ERROR:'.length).trim();
    } else {
      const n = Number(v);
      if (Number.isFinite(n)) benchmarks[k] = n;
      else errors[k] = `non-numeric score: ${v}`;
    }
  }
  return { benchmarks, errors, version: raw.__version__ ?? null };
}

function geomean(values) {
  const xs = values.filter((v) => Number.isFinite(v) && v > 0);
  if (xs.length === 0) return null;
  const sumLn = xs.reduce((acc, v) => acc + Math.log(v), 0);
  return Math.round(Math.exp(sumLn / xs.length));
}

// ── Broiler: one isolated `dotnet … --script-host <combined>` process per suite
function runBroiler(opts, manifest, baseJs, runnerJs) {
  if (!opts.broilerDll) throw new Error('--broiler-dll is required for the broiler engine');
  const work = mkdtempSync(join(tmpdir(), 'octane-broiler-'));
  const benchmarks = {};
  const suiteStatus = {};
  let octaneVersion = null;

  for (const suite of manifest.suites) {
    const parts = [baseJs];
    for (const f of suite.files) parts.push(readFileSync(join(opts.octaneDir, f), 'utf8'));
    parts.push(runnerJs);
    const combined = join(work, `${suite.name}.js`);
    writeFileSync(combined, parts.join('\n'));

    process.stderr.write(`[broiler] ${suite.name} … `);
    const res = spawnSync('dotnet', [opts.broilerDll, '--script-host', combined], {
      encoding: 'utf8',
      timeout: opts.timeout * 1000,
      maxBuffer: 64 * 1024 * 1024,
    });

    const line = (res.stdout || '').split('\n').find((l) => l.includes('OCTANE_RESULT_JSON'));
    if (line) {
      const raw = JSON.parse(line.slice(line.indexOf('{')));
      const { benchmarks: b, errors, version } = normalizeRaw(raw);
      octaneVersion ??= version;
      Object.assign(benchmarks, b);
      const ok = Object.keys(errors).length === 0;
      suiteStatus[suite.name] = ok
        ? { status: 'ok' }
        : { status: 'error', error: Object.values(errors)[0] };
      process.stderr.write(ok ? `ok\n` : `error\n`);
    } else if (res.error && res.error.code === 'ETIMEDOUT') {
      suiteStatus[suite.name] = { status: 'timeout' };
      process.stderr.write(`timeout\n`);
    } else {
      const stderr = (res.stderr || '').replace(/\r/g, '').split('\n').find((l) => l.includes('Exception')) || '';
      suiteStatus[suite.name] = { status: 'crash', error: stderr.slice(0, 300) };
      process.stderr.write(`crash\n`);
    }
  }

  return {
    engine: 'broiler',
    engineLabel: 'Broiler.JS (BroilerJS --script-host)',
    octaneVersion,
    perSuiteTimeoutSec: opts.timeout,
    benchmarks,
    suiteStatus,
    geomean: geomean(Object.values(benchmarks)),
  };
}

// ── Chromium: one isolated Playwright page per suite (real V8 in a browser) ──
async function runChromium(opts, manifest, baseJs, runnerJs) {
  // Playwright is installed under tests/octane/node_modules by the orchestrator.
  // ESM bare-specifier resolution ignores NODE_PATH, so resolve it explicitly
  // from that install location.
  const requireFrom = createRequire(join(REPO_ROOT, 'tests', 'octane', 'package.json'));
  const { chromium } = requireFrom('playwright');
  const browser = await chromium.launch();
  const engineLabel = `Chromium ${browser.version()}`;
  const benchmarks = {};
  const suiteStatus = {};
  let octaneVersion = null;

  try {
    for (const suite of manifest.suites) {
      process.stderr.write(`[chromium] ${suite.name} … `);
      const context = await browser.newContext();
      const page = await context.newPage();
      try {
        await page.setContent('<!doctype html><html><head></head><body></body></html>');
        await page.addScriptTag({ content: baseJs });
        for (const f of suite.files) {
          await page.addScriptTag({ content: readFileSync(join(opts.octaneDir, f), 'utf8') });
        }
        await page.addScriptTag({ content: runnerJs });
        const raw = await Promise.race([
          page.evaluate(() => new Promise((res) => { window.__octaneRun(res); })),
          new Promise((_, rej) => setTimeout(() => rej(new Error('timeout')), opts.timeout * 1000)),
        ]);
        const { benchmarks: b, errors, version } = normalizeRaw(raw);
        octaneVersion ??= version;
        Object.assign(benchmarks, b);
        const ok = Object.keys(errors).length === 0;
        suiteStatus[suite.name] = ok
          ? { status: 'ok' }
          : { status: 'error', error: Object.values(errors)[0] };
        process.stderr.write(ok ? `ok\n` : `error\n`);
      } catch (e) {
        const status = String(e.message).includes('timeout') ? 'timeout' : 'crash';
        suiteStatus[suite.name] = { status, error: String(e.message).slice(0, 300) };
        process.stderr.write(`${status}\n`);
      } finally {
        await context.close();
      }
    }
  } finally {
    await browser.close();
  }

  return {
    engine: 'chromium',
    engineLabel,
    octaneVersion,
    perSuiteTimeoutSec: opts.timeout,
    benchmarks,
    suiteStatus,
    geomean: geomean(Object.values(benchmarks)),
  };
}

// ── Comparison report ───────────────────────────────────────────────────────
function buildComparison(results, generatedAt) {
  const chromium = results.chromium;
  const broiler = results.broiler;
  const names = new Set();
  for (const r of [chromium, broiler]) {
    if (!r) continue;
    for (const k of Object.keys(r.benchmarks)) names.add(k);
    for (const k of Object.keys(r.suiteStatus)) names.add(k);
  }
  // Order benchmarks by the manifest; sub-results (…Latency) sort after their suite.
  const rows = [...names].sort();

  function cell(r, name) {
    if (!r) return { score: null, status: 'n/a' };
    if (name in r.benchmarks) return { score: r.benchmarks[name], status: r.suiteStatus[name]?.status ?? 'ok' };
    const st = r.suiteStatus[name];
    return { score: null, status: st ? st.status : 'n/a', error: st?.error };
  }

  const comparison = {
    generatedAt,
    octaneVersion: (chromium?.octaneVersion ?? broiler?.octaneVersion) ?? null,
    engines: {
      chromium: chromium ? { label: chromium.engineLabel, geomean: chromium.geomean } : null,
      broiler: broiler ? { label: broiler.engineLabel, geomean: broiler.geomean } : null,
    },
    benchmarks: rows.map((name) => {
      const c = cell(chromium, name);
      const b = cell(broiler, name);
      const ratio = c.score && b.score ? Number((b.score / c.score).toFixed(3)) : null;
      return { name, chromium: c, broiler: b, broilerVsChromium: ratio };
    }),
  };
  return comparison;
}

function renderMarkdown(cmp) {
  const c = cmp.engines.chromium;
  const b = cmp.engines.broiler;
  const fmt = (cell) => {
    if (cell.score != null) return String(cell.score);
    if (cell.status === 'n/a') return '—';
    return `_${cell.status}_`;
  };
  const lines = [];
  lines.push(`# Octane 2.0 benchmark — Chromium vs Broiler`);
  lines.push('');
  lines.push(`- Generated: \`${cmp.generatedAt}\``);
  lines.push(`- Octane version: \`${cmp.octaneVersion ?? 'unknown'}\``);
  if (c) lines.push(`- Chromium: \`${c.label}\` — **overall score ${c.geomean ?? 'n/a'}**`);
  if (b) lines.push(`- Broiler: \`${b.label}\` — **overall score ${b.geomean ?? 'n/a'}**`);
  lines.push('');
  lines.push('Higher is better. "Broiler / Chromium" is the ratio of scores on suites both engines completed.');
  lines.push('');
  lines.push('| Benchmark | Chromium | Broiler | Broiler / Chromium |');
  lines.push('|---|--:|--:|--:|');
  for (const row of cmp.benchmarks) {
    const ratio = row.broilerVsChromium != null ? row.broilerVsChromium.toFixed(3) : '—';
    lines.push(`| ${row.name} | ${fmt(row.chromium)} | ${fmt(row.broiler)} | ${ratio} |`);
  }
  lines.push(`| **Overall (geomean)** | **${c?.geomean ?? '—'}** | **${b?.geomean ?? '—'}** | ${c?.geomean && b?.geomean ? (b.geomean / c.geomean).toFixed(3) : '—'} |`);
  lines.push('');
  // Non-ok suite notes.
  const notes = cmp.benchmarks.filter((r) => r.broiler.status && !['ok', 'n/a'].includes(r.broiler.status) && r.broiler.error);
  if (notes.length) {
    lines.push('## Broiler failures');
    lines.push('');
    for (const n of notes) lines.push(`- **${n.name}** (${n.broiler.status}): ${n.broiler.error}`);
    lines.push('');
  }
  lines.push('---');
  lines.push('_Generated by `scripts/run-octane.mjs`. Source suite: https://github.com/chromium/octane_');
  lines.push('');
  return lines.join('\n');
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  const manifest = JSON.parse(readFileSync(opts.suites, 'utf8'));
  const baseJs = readFileSync(join(opts.octaneDir, 'base.js'), 'utf8');
  const runnerJs = readFileSync(opts.runner, 'utf8');
  const engines = opts.engines.split(',').map((s) => s.trim()).filter(Boolean);
  const generatedAt = new Date().toISOString();

  mkdirSync(opts.outDir, { recursive: true });
  const results = {};

  for (const engine of engines) {
    let r;
    if (engine === 'broiler') r = runBroiler(opts, manifest, baseJs, runnerJs);
    else if (engine === 'chromium') r = await runChromium(opts, manifest, baseJs, runnerJs);
    else throw new Error(`Unknown engine: ${engine}`);
    r.generatedAt = generatedAt;
    results[engine] = r;
    const out = join(opts.outDir, `${engine}-results.json`);
    writeFileSync(out, JSON.stringify(r, null, 2) + '\n');
    process.stderr.write(`[${engine}] wrote ${out} (overall score: ${r.geomean ?? 'n/a'})\n`);
  }

  // Fold in any per-engine result already on disk so a single-engine run still
  // refreshes the comparison against the other engine's last result.
  for (const engine of ['chromium', 'broiler']) {
    if (results[engine]) continue;
    const p = join(opts.outDir, `${engine}-results.json`);
    if (existsSync(p)) results[engine] = JSON.parse(readFileSync(p, 'utf8'));
  }

  const cmp = buildComparison(results, generatedAt);
  writeFileSync(join(opts.outDir, 'comparison.json'), JSON.stringify(cmp, null, 2) + '\n');
  writeFileSync(join(opts.outDir, 'comparison.md'), renderMarkdown(cmp));
  process.stderr.write(`[compare] wrote comparison.json + comparison.md\n`);
}

main().catch((e) => { console.error(e); process.exit(1); });
