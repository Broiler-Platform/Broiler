# WPT regression baselines

Committed per-segment baseline snapshots used by the scheduled WPT matrix
(`.github/workflows/wpt-tests.yml`) and `scripts/check-wpt-regression.sh`.

Each file is a `wpt-results.json` produced by a known-good run for one segment,
named after the matrix `segment.id` (e.g. `css-css2.json`, `css-flexbox.json`,
`dom.json`). The gate compares a fresh run's `summary.passed` / `summary.failed`
against the baseline and fails only on **regression** — so the large backlog of
skipped/failing tests does not keep CI red.

## Establishing or refreshing a baseline

When a segment has no baseline file, the gate emits a warning and passes. To
enable gating for that segment:

1. Run the segment (locally or via the manual `workflow_dispatch`):
   ```bash
   bash scripts/run-wpt-tests.sh --subset "css/CSS2"
   ```
2. Copy the result here under the matching segment id:
   ```bash
   cp tests/wpt-results/wpt-results.json tests/wpt-baseline/css-css2.json
   ```
3. Commit it. Subsequent runs are now gated against this snapshot.

Refresh a baseline (raise the bar) the same way after an intentional,
reviewed improvement that increases the pass count.
