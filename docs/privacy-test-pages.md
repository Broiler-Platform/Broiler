# Privacy test pages

The [DuckDuckGo privacy test pages](https://github.com/duckduckgo/privacy-test-pages)
are a public corpus that asks a browser to exercise the parts of the platform
privacy features act on: client-side storage, the request paths a page can open,
the fingerprinting surface, HTTPS upgrades, and referrer and tracker handling.
Each page runs its probes itself and publishes the outcome in a `results`
global — the interface the upstream README documents — so what a run reports is
what the page computed, not a rendering of it.

```sh
python scripts/run-privacy-test-pages.py                 # every page in the manifest
python scripts/run-privacy-test-pages.py --pages fingerprinting,gpc
python scripts/run-privacy-test-pages.py --skip-build --fail-on-regression
```

The runner needs no third-party Python packages, and the reports land under the
ignored `artifacts/privacy-test-pages/`.

## What this measures, and what it does not

**It measures coverage.** A probe that produces a value is one Broiler was able
to carry out; a probe that produces nothing is a platform gap — an API that is
not there, a request that never completed, a callback that never ran.

**It does not grade privacy.** The pages are written to *describe* a browser, so
the values they collect are a description of what a page can observe, not a
score. "Broiler reported a `navigator.userAgent`" is coverage; whether that
string is a good answer for a shipping browser is a product question this suite
does not answer. Nothing here should be read as a privacy result for Broiler.

## How a page is run

`Broiler.Cli --evaluate-page` fetches the page, runs its scripts through the DOM
bridge exactly as a capture would, fires the load event, and then evaluates the
manifest's expressions against the same JavaScript context:

```sh
dotnet run --project src/Broiler.Cli -- \
  --evaluate-page "https://privacy-test-pages.site/privacy-protections/fingerprinting/?run" \
  --evaluate "typeof runTests === 'function' ? (runTests(), 'started') : 'auto'" \
  --evaluate "typeof results === 'undefined' ? null : JSON.stringify(results)" \
  --output results.json
```

Pending timers and promises are drained *between* expressions, which is what
makes the pair work: the first starts the page's own run, the second reads the
global it filled once that run has settled. `results` is a top-level `const`
rather than a property of `window`, so it is reachable by name from a later
evaluation and by nothing else.

The mode is general — it answers "what did this page's JavaScript compute?" for
any page — and is documented under `--help`. `--evaluate-html-output <FILE>`
additionally writes the post-script DOM.

## Outcomes and the baseline

Every entry of a page's `results` array is classified:

| Outcome | Meaning |
| --- | --- |
| `value` | The probe produced something: Broiler carried the test out. |
| `empty` | `null`, `{}`, `[]`, or blank — the probe produced nothing. |
| `missing` | Only from a comparison: the baseline had this test, the run did not. |
| `new` | Only from a comparison: the run had this test, the baseline did not. |

`tests/privacy-test-pages/baseline.json` records the outcome of every test at
the last accepted run, and is the tracked expectation the suite compares
against. Regenerate it deliberately, never as a way to clear a red run:

```sh
python scripts/run-privacy-test-pages.py --update-baseline
```

A partial run (`--pages`) keeps the baseline entries for the pages it skipped,
so regenerating one page does not delete the rest.

## What counts as a regression

The corpus is live and maintained upstream, so tests appear, are renamed, and
disappear without a Broiler code change. Only two things fail a run under
`--fail-on-regression`:

- a page that used to produce results and now produces none, and
- a test whose outcome was `value` and is now `empty` or `missing`.

Tests added upstream (`new`) and tests removed upstream (`removed`) are reported
in full and never fail a run. `--fail-on-incomplete` additionally fails when any
selected page could not be evaluated at all, which is off by default because a
public site can be unreachable for reasons that are not Broiler's.

Each page is attempted twice by default (`attempts` in the manifest) so a single
network failure does not read as a regression.

## The corpus

`tests/privacy-test-pages/pages.json` (validated by `pages.schema.json`) lists
the pages, the query each needs to run itself on load, and any per-page
overrides. It covers nine of the Privacy Protections pages: fingerprinting,
storage blocking, request blocking, blocking behaviour, Global Privacy Control,
HTTPS upgrades, HTTPS upgrade loop protection, AMP loop protection, and
surrogates.

Three of the remaining pages are deliberately absent, because a single-page
evaluation cannot produce their results at all rather than because Broiler fails
them:

| Page | Why it is not in the corpus |
| --- | --- |
| `referrer-trimming` | Finishes on a *return* navigation; the results array is empty on the first load. |
| `storage-partitioning` | Needs partitioned sub-frames to report back; the results array stays empty. |
| `click-to-load` | Loads the Facebook SDK and never settles — the evaluation runs to the process timeout with no output. |

Adding a page is a manifest entry plus a baseline regeneration. A page that
needs a different trigger can override `startExpression`; one that publishes its
results elsewhere can override `resultsExpression`.

## Reports

Each run writes to `artifacts/privacy-test-pages/`:

```
results.json        the whole run, machine-readable: per-page outcomes, comparison, attempts
report.md           per-page detail, including every regression, addition and removal
report.html         the same as a browsable page
job-summary.md      the workflow summary: totals, regressions, pages that did not run
pages/<id>/
  results.json      the page's own results payload, as it published it
  attempt-N.json    the raw --evaluate-page report
  attempt-N.stdout.log, attempt-N.stderr.log
```

## CI

The `Privacy Test Pages` workflow runs weekly and on manual dispatch. It is not
a pull-request gate: the inputs are public pages that change independently of
this repository. A scheduled run fails on a regression against the baseline;
`fail_on_regression`, `fail_on_incomplete`, `pages`, and `update_baseline` are
dispatch inputs. `update_baseline` uploads the regenerated file as an artifact
rather than committing it, so a baseline change stays a reviewed commit.

## Current coverage

The fingerprinting page is where Broiler carries out most of what is asked of
it. The request- and storage-driven pages are largely `empty` today: their
probes depend on sub-resource loads, workers, sockets, and frames completing and
reporting back, which is the gap this baseline exists to track. Read the
per-page numbers in `report.md` rather than the total, which the 124-test
fingerprinting page dominates.
