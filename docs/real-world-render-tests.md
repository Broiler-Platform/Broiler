# Real-world website render tests

The real-world suite complements WPT with public pages people actually use. It
captures Google Search, 7-Zip, heise online, Wikipedia, GitHub, MDN, Hacker News,
and W3C at a fixed 1024×768 viewport, then publishes the screenshots, diffs,
logs, machine-readable results, and a detailed HTML/Markdown report.

This is deliberately an **observational** suite. A live website can change its
HTML, experiment assignment, consent page, advertisements, news, fonts, or
anti-bot response without a Broiler commit. The weekly workflow therefore
reports missed quality thresholds but does not fail on them by default. A
manual dispatch can enable `fail_on_mismatch` or `fail_on_incomplete` when a
specific run is intended to act as a gate.

## Two comparison lanes

Each test case produces three renders:

1. **Chromium reference** — Playwright visits the live URL in a fresh context
   with a fixed viewport, locale, timezone, light color scheme, reduced motion,
   and device scale factor. It records the final URL, main response status,
   browser versions, errors, timings, final serialized DOM, and screenshot.
2. **Broiler live** — `Broiler.Cli --capture-image` visits the same public URL.
   This is the end-to-end result and includes Broiler's networking, HTML, CSS,
   JavaScript, layout, and painting behavior. Its comparison determines the
   site's quality result.
3. **Broiler replay** — Broiler renders the final DOM recorded by Chromium after
   scripts, inline event handlers, refresh/CSP metadata, and embedded-document
   navigation have been neutralized and a `<base>` URL has been inserted. This
   layout-only diagnostic does not determine pass/fail. A poor
   live result with a good replay points toward input loading or script
   execution; a poor result in both lanes points toward layout or painting.

The replay is useful isolation evidence, not a bit-for-bit frozen web archive.
External styles, images, and fonts can still change or fail, and serialized DOM
does not preserve canvas pixels, closed shadow roots, browser-internal control
state, or every JavaScript object. The captured `snapshot.html` and its SHA-256
hash nevertheless make the exact document structure used by the replay
inspectable.

The live lane deliberately measures the product as it exists today. Chromium
uses the manifest's locale/timezone and its normal browser user agent, while
`Broiler.Cli` currently has no equivalent shared browser-session controls and
fetches through its own loaders. Google or heise may therefore return different
documents. The report records that evidence, and the replay lane helps distinguish
an input/loading discrepancy from a layout or painting discrepancy.

## Metrics

Every comparison reports:

- **Pixel match** — percentage of viewport pixels whose RGBA channels differ by
  at most the configured tolerance (5 by default, matching the WPT runner).
- **Content match** — the same tolerant match restricted to the union of the
  two images' non-background pixels. This prevents a mostly white viewport from
  calling a visibly broken page “97% pixel-perfect.”
- **Structure match** — edge precision/recall F1 with one pixel of spatial
  tolerance. It is sensitive to missing text, controls, and layout while being
  less dominated by flat backgrounds.
- Mean absolute error, root-mean-square error, content coverage, mismatch
  bounding box, image dimensions and hashes, reference-to-live capture delta,
  plus the worst 4×3 viewport tiles.

A live result meets quality only when its dimensions match and all three primary
metrics meet their thresholds. Stable and dynamic sites are summarized
separately; dynamic pages are never presented as a standards-conformance rate.
Capture/network failures are reported as incomplete rather than pixel failures.

## Run locally

From the repository root on Linux or WSL:

```bash
(cd tests/wpt && npm ci && npx playwright install --with-deps chromium)
python -m pip install --requirement tests/real-world-sites/requirements.txt
export NODE_PATH="$PWD/tests/wpt/node_modules"
python scripts/run-real-world-render-tests.py
```

Run a smaller slice or make missed thresholds fail the command:

```bash
python scripts/run-real-world-render-tests.py \
  --sites google-search,seven-zip,heise \
  --fail-on-mismatch
```

Use `--skip-reference-capture` to reuse references and snapshots already in the
output directory. Reuse is accepted only when the recorded URL, viewport,
locale, timezone, timing policy, and both SHA-256 hashes match the current case;
otherwise the site is reported incomplete rather than compared against stale
evidence. Use `--skip-build` to reuse an existing Release build of
`Broiler.Cli`. Each Broiler child process has a 1024 MiB process-tree RSS limit
by default; use `--memory-limit-mb` to change it or `0` to disable it. Run
`--help` for viewport, threshold, retry, and output options.

Generated evidence is written to `artifacts/real-world-render-tests/`:

```text
results.json                 complete machine-readable report
report.md                    detailed artifact-oriented Markdown report
job-summary.md               Actions-safe summary without broken artifact links
report.html                  standalone visual report
sites/<id>/reference.png     Chromium viewport
sites/<id>/snapshot.html     recorded final DOM for diagnostic replay
sites/<id>/broiler-live.png  end-to-end Broiler viewport
sites/<id>/broiler-replay.png
sites/<id>/live-diff.png
sites/<id>/replay-diff.png
sites/<id>/*.json|*.log      metadata, process output, and individual result
```

The corpus is the versioned allowlist in
[`tests/real-world-sites/sites.json`](../tests/real-world-sites/sites.json), with
its format documented by `sites.schema.json`. Keep it limited to public,
unauthenticated pages: CI must never send repository credentials or signed-in
browser state to these origins.

## CI behavior

[`real-world-render-tests.yml`](../.github/workflows/real-world-render-tests.yml)
runs weekly and on manual dispatch. It reuses the WPT suite's pinned Playwright
version and Chromium cache, applies hard capture-process deadlines, always adds
an Actions-safe Markdown summary to the job, and retains the complete visual artifact
for 30 days. Every report records the repository/submodule revisions, manifest
and harness hashes, OS/tool/browser versions, viewport, locale/timezone, HTTP
result, capture timings, and process memory peak. It does not run for every pull request so the repository does not
hammer public sites or turn their outages and experiments into unrelated merge
failures. Regardless of the two opt-in failure flags, a run with zero comparable
sites exits with an infrastructure-failure status so a completely broken harness
cannot appear green; partial capture failures remain visible in the report and
are non-fatal by default.
