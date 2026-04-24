# WPT Triage Summary

- Generated: 2026-04-24T11:14:17Z
- Subset: html

## Totals

- Total: 9728
- Passed: 1686
- Failed: 1440
- Skipped: 6602

## Top failing buckets

- `html/semantics/scripting-1/the-script-element/execution-timing` — 113
- `html/semantics/forms/the-select-element/customizable-select` — 106
- `html/semantics/forms/the-input-element` — 87
- `html/syntax/parsing` — 77
- `html/semantics/interactive-elements/the-dialog-element` — 74

## Top skipped buckets

- `html/canvas/element/fill-and-stroke-styles` — 261
- `html/canvas/offscreen/fill-and-stroke-styles` — 244
- `html/canvas/element/path-objects` — 204
- `html/canvas/offscreen/path-objects` — 204
- `html/canvas/offscreen/layers` — 200

## Reference coverage priorities

- Missing-reference skips: 6583
- Pass-rate comparison ready: No
- Hold pass-rate comparisons until these buckets are rerun with generated references for the same subset.

### Top missing-reference buckets

- `html/canvas/element/fill-and-stroke-styles` — 261 missing-reference skip(s)
- `html/canvas/offscreen/fill-and-stroke-styles` — 244 missing-reference skip(s)
- `html/canvas/element/path-objects` — 204 missing-reference skip(s)
- `html/canvas/offscreen/path-objects` — 204 missing-reference skip(s)
- `html/canvas/offscreen/layers` — 200 missing-reference skip(s)

### Suggested reference-generation commands

- `./scripts/run-wpt-tests.sh --subset "html/canvas/element/fill-and-stroke-styles"`
- `./scripts/run-wpt-tests.sh --subset "html/canvas/offscreen/fill-and-stroke-styles"`
- `./scripts/run-wpt-tests.sh --subset "html/canvas/element/path-objects"`
- `./scripts/run-wpt-tests.sh --subset "html/canvas/offscreen/path-objects"`
- `./scripts/run-wpt-tests.sh --subset "html/canvas/offscreen/layers"`

## Deferred unsupported / MissingContent-dominant buckets

- `html/semantics/popovers` — 68 failure(s) [MissingContentDominant]; MissingContent 51.5 %
- `html/semantics/embedded-content/the-canvas-element` — 54 failure(s) [MissingContentDominant]; MissingContent 92.6 %
- `html/semantics/embedded-content/media-elements/track/track-element` — 45 failure(s) [MissingContentDominant]; MissingContent 97.8 %
- `html/semantics/embedded-content/the-img-element` — 24 failure(s) [MissingContentDominant]; MissingContent 70.8 %
- `html/semantics/forms/the-fieldset-element/accessibility` — 23 failure(s) [MissingContentDominant]; MissingContent 82.6 %

## Timeout failures

- None

### Suggested timeout subset commands

- None

## Non-pixel / exception failures

- None

## Suggested next subset commands

- `./scripts/run-wpt-tests.sh --subset "html/semantics/scripting-1/the-script-element/execution-timing"`
- `./scripts/run-wpt-tests.sh --subset "html/semantics/forms/the-select-element/customizable-select"`
- `./scripts/run-wpt-tests.sh --subset "html/semantics/forms/the-input-element"`
- `./scripts/run-wpt-tests.sh --subset "html/syntax/parsing"`
- `./scripts/run-wpt-tests.sh --subset "html/semantics/interactive-elements/the-dialog-element"`
