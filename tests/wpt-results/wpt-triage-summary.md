# WPT Triage Summary

- Generated: 2026-04-21T21:07:02Z
- Subset: css

## Totals

- Total: 24920
- Passed: 2267
- Failed: 1984
- Skipped: 20669

## Top failing buckets

- `css/css-writing-modes` — 380
- `css/filter-effects` — 241
- `css/css-variables` — 198
- `css/selectors` — 197
- `css/css-view-transitions` — 126

## Top skipped buckets

- `css/css-flexbox` — 1038
- `css/css-ui/compute-kind-widget-generated` — 802
- `css/css-break` — 478
- `css/css-ui` — 471
- `css/css-transforms` — 451

## Reference coverage priorities

- Missing-reference skips: 20661
- Pass-rate comparison ready: No
- Hold pass-rate comparisons until these buckets are rerun with generated references for the same subset.

### Top missing-reference buckets

- `css/css-flexbox` — 1038 missing-reference skip(s)
- `css/css-ui/compute-kind-widget-generated` — 802 missing-reference skip(s)
- `css/css-break` — 478 missing-reference skip(s)
- `css/css-ui` — 470 missing-reference skip(s)
- `css/css-transforms` — 451 missing-reference skip(s)

### Suggested reference-generation commands

- `./scripts/run-wpt-tests.sh --subset "css/css-flexbox"`
- `./scripts/run-wpt-tests.sh --subset "css/css-ui/compute-kind-widget-generated"`
- `./scripts/run-wpt-tests.sh --subset "css/css-break"`
- `./scripts/run-wpt-tests.sh --subset "css/css-ui"`
- `./scripts/run-wpt-tests.sh --subset "css/css-transforms"`

## Deferred unsupported / MissingContent-dominant buckets

- `css/css-writing-modes` — 380 failure(s) [MissingContentDominant]; MissingContent 62.1 %
- `css/filter-effects` — 255 failure(s) [ExplicitFeatureGap]
- `css/selectors` — 197 failure(s) [MissingContentDominant]; MissingContent 57.4 %
- `css/css-view-transitions` — 167 failure(s) [ExplicitFeatureGap]
- `css/motion` — 93 failure(s) [MissingContentDominant]; MissingContent 76.3 %

## Non-pixel / exception failures

- `Timeout` `css/css-grid/parsing/grid-template-columns-crash.html`
  - Test timed out after 30 second(s): /home/runner/work/Broiler/Broiler/tests/wpt/checkout/css/css-grid/parsing/grid-template-columns-crash.html
- `Timeout` `css/css-overflow/scroll-markers/column-scroll-marker-007.html`
  - Test timed out after 30 second(s): /home/runner/work/Broiler/Broiler/tests/wpt/checkout/css/css-overflow/scroll-markers/column-scroll-marker-007.html
- `Timeout` `css/css-overflow/scroll-markers/targeted-scroll-marker-selection-with-transition.tentative.html`
  - Test timed out after 30 second(s): /home/runner/work/Broiler/Broiler/tests/wpt/checkout/css/css-overflow/scroll-markers/targeted-scroll-marker-selection-with-transition.tentative.html
- `Timeout` `css/css-overflow/scroll-markers/targeted-scroll-marker-selection.tentative.html`
  - Test timed out after 30 second(s): /home/runner/work/Broiler/Broiler/tests/wpt/checkout/css/css-overflow/scroll-markers/targeted-scroll-marker-selection.tentative.html
- `Timeout` `css/css-shapes/shape-outside/supported-shapes/circle/shape-outside-circle-030.html`
  - Test timed out after 30 second(s): /home/runner/work/Broiler/Broiler/tests/wpt/checkout/css/css-shapes/shape-outside/supported-shapes/circle/shape-outside-circle-030.html

## Suggested next subset commands

- `./scripts/run-wpt-tests.sh --subset "css/css-variables"`
- `./scripts/run-wpt-tests.sh --subset "css/css-flexbox"`
- `./scripts/run-wpt-tests.sh --subset "css/css-ui/compute-kind-widget-generated"`
- `./scripts/run-wpt-tests.sh --subset "css/css-break"`
- `./scripts/run-wpt-tests.sh --subset "css/css-ui"`
