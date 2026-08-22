# Privacy test pages

The [DuckDuckGo privacy test pages](https://github.com/duckduckgo/privacy-test-pages)
are a public corpus that asks a browser to exercise the parts of the platform
privacy features act on: client-side storage, the request paths a page can open,
the fingerprinting surface, HTTPS upgrades, and referrer and tracker handling.
Each page runs its probes itself and publishes the outcome in a `results`
global — the interface the upstream README documents — so what a run reports is
what the page computed, not a rendering of it.

Every page is run **twice**: once by Chromium through Playwright and once by
Broiler, from the same manifest and the same expressions. The two `results`
payloads are then compared probe by probe, so the report says not only what
Broiler produced but what a shipping engine produces for the same probe.

```sh
python scripts/run-privacy-test-pages.py                 # every page, both engines
python scripts/run-privacy-test-pages.py --pages fingerprinting,gpc
python scripts/run-privacy-test-pages.py --skip-build --fail-on-regression
python scripts/run-privacy-test-pages.py --no-reference   # Broiler only
```

The Python runner needs no third-party packages; the Chromium capture needs the
pinned Playwright under `tests/wpt` (`npm ci` there, then
`npx playwright install chromium`), and the runner sets `NODE_PATH` to it
itself. Reports land under the ignored `artifacts/privacy-test-pages/`.

## What this measures, and what it does not

**It measures coverage.** A probe that produces a value is one Broiler was able
to carry out; a probe that produces nothing is a platform gap — an API that is
not there, a request that never completed, a callback that never ran.

**Chromium says which gaps are Broiler's.** Several of these pages depend on
endpoints, sub-frames and return navigations that can fail for reasons that have
nothing to do with the engine under test, and a probe nothing answers looks
exactly like a probe Broiler cannot answer. Running Chromium over the same
manifest separates the two: a probe Chromium answers and Broiler does not is a
platform gap, and it arrives with the shape and value that were expected.

**It does not grade privacy.** The pages are written to *describe* a browser, so
the values they collect are a description of what a page can observe, not a
score. "Broiler reported a `navigator.userAgent`" is coverage; whether that
string is a good answer for a shipping browser is a product question this suite
does not answer. Nothing here should be read as a privacy result for Broiler —
including agreement with Chromium, which means Broiler carried the probe out,
not that it made the right privacy choice.

**Nothing is compared as pixels.** These pages render a summary of what their
own JavaScript measured; their `results` payload *is* the output, and a
screenshot of it would restate the same thing less precisely and with more
false positives. Pixel comparison against Chromium is what
[the real-world render suite](real-world-render-tests.md) is for.

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

## How the Chromium reference is captured

`scripts/capture-privacy-reference.js` drives the same manifest through
Playwright's Chromium and writes one `pages/<id>/reference.json` per page:

```sh
NODE_PATH=tests/wpt/node_modules node scripts/capture-privacy-reference.js \
  --pages fingerprinting --output-dir artifacts/privacy-test-pages
```

It evaluates the manifest's own `startExpression` and `resultsExpression` —
comparing two engines is only meaningful if they were asked the same question —
and then re-reads the results array on a poll until its length holds still for
three consecutive checks or the page's timeout expires. Nothing is blocked in
the browser context: storage, workers and service workers are exactly what
several of these pages measure, so the reference has to be what a stock browser
does. `--executable-path` (or `BROILER_CHROMIUM_EXECUTABLE`) points the capture
at a Chromium that is already on the machine instead of Playwright's own.

The runner captures references before it builds Broiler, so a browser failure
surfaces in the first minute rather than the last. `--skip-reference` reuses the
references already in the output directory (the one artefact a run may inherit;
everything else is cleared), and `--no-reference` runs Broiler alone.

A page Chromium could not run is reported as **not compared** rather than as
parity — an absent reference must never read as agreement. `--require-reference`
turns that into a failure for runs that need the comparison to have happened.

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

The baseline records Broiler's own outcomes only. The Chromium comparison is
recomputed from a fresh capture every run and is deliberately not baselined:
freezing another engine's answers would turn upstream corpus churn and Chromium
releases into Broiler regressions.

## Chromium parity

Each probe of a compared page falls into one of four buckets:

| Parity | Meaning |
| --- | --- |
| `parity` | Both engines answered. Broiler carried the probe out. |
| `gap` | Chromium answered, Broiler did not. **This is the finding.** |
| `neither` | No engine answered — a page, corpus or network limitation, not Broiler's. |
| `broiler-only` | Only Broiler answered; nearly always a sign the two runs saw different pages. |

These pages run each probe in a `try` and store the caught error where the
result belongs, so a probe that threw is published as a value like
`{"name": "ReferenceError", "message": "…", "stack": "…"}`. The comparison
recognizes that shape and counts it as *not answered*, whichever engine threw —
otherwise a missing API would read as parity. The baseline outcome is
deliberately left alone (a thrown probe is still a `value` there), so this
reinterpretation can never turn into a regression; it only decides what the
comparison calls a gap, and the gap then carries the thrown message as its
evidence.

A gap carries the evidence with it: the type Chromium returned and a bounded
excerpt of the value, so a reader can tell a missing API from a request that
never completed without re-running anything. Probes both engines answer in
*different shapes* are listed separately — a differing value is expected between
engines (a user-agent string, a screen size), but a differing type usually means
a stub standing in for a result.

Gaps never fail a run. They are the backlog this suite exists to describe, and
they change when the upstream corpus or Chromium changes; only the baseline
comparison decides pass or fail.

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
results.json        the whole run, machine-readable: per-page outcomes, comparison, parity, attempts
report.md           per-page detail, including every regression, addition, removal and gap
report.html         the same as a browsable page
job-summary.md      the workflow summary: totals, gaps, regressions, pages that did not run
gaps.json           the Chromium comparison alone, with the expected value per gap
gaps.md             the same, as the issue-ready document a reader files from
reference.log       the Chromium capture's own output
issues/index.json   what this run would file as a GitHub issue: kind, title, marker, counts
issues/<kind>.md    the body of each issue worth filing (see "Issues" below)
pages/<id>/
  results.json      the page's own results payload, as Broiler published it
  reference.json    the same page as Chromium published it, plus browser and diagnostics
  attempt-N.json    the raw --evaluate-page report
  attempt-N.stdout.log, attempt-N.stderr.log
```

`gaps.md` is the one to read first: it lists, per page, every probe Chromium
answered and Broiler did not, with what Chromium returned beside it.

## Issues

Like the WPT runners, a CI run files what it found as GitHub issues rather than
leaving it in an artifact that expires. Three of them, because ranking them
together would bury the two small ones under the large one:

| Kind | What it carries | Filed when |
| --- | --- | --- |
| `regressions` | Probes that stopped producing a value, and pages that stopped running; a page that stopped running reports its probe count rather than every probe, since they all went for one reason. | Any regression against the baseline. |
| `unmeasured` | Pages Broiler could not run, and pages with no Chromium reference, each with its reason. | Any such page — except a run that disabled the comparison itself. |
| `gaps` | Probes Chromium answers and Broiler does not, ranked by thrown-error signature and then by page. | Any gap, on a run that compared at least one page. |

The gaps issue leads with the error signatures because they are what collapses
the list into work: a probe that threw is reported once per probe, and
normalizing the quoted values, URLs and numbers out of the message regroups every
probe that a single missing API gates into one row.

`run-privacy-test-pages.py` decides all of this and writes the bodies —
`--gap-issue-limit` and `--regression-issue-limit` bound them — so the rules live
with the runner that measured them rather than in the workflow. `--github-output`
writes the counts and `create_<kind>_issue` flags a workflow step files them
from. A `--update-baseline` run files nothing: its comparison describes the
baseline being replaced.

Each body carries a hidden marker naming its kind, and
`scripts/lib/github-issue.js` uses it to find the issue already open for that
kind and refresh it in place — title, body, and a comment linking the run. This
is the one place the suite deliberately differs from the WPT runners, which open
a fresh issue per run: those are dispatched by a human who then reads them, while
this one runs weekly and unattended over a backlog that changes slowly, so a
second copy every Tuesday would bury the one somebody is working from.

## CI

The `Privacy Test Pages` workflow runs weekly and on manual dispatch. It is not
a pull-request gate: the inputs are public pages that change independently of
this repository. A scheduled run fails on a regression against the baseline;
`fail_on_regression`, `fail_on_incomplete`, `require_reference`,
`skip_reference`, `pages`, `update_baseline`, `create_issues`,
`gap_issue_limit`, and `regression_issue_limit` are dispatch inputs.
It files its findings as the issues described above — a run that fails on a
regression still files, since that is exactly the run with something to say —
using `ISSUE_TOKEN` when the repository configures one and the workflow's own
token otherwise. `create_issues: false` runs the suite without filing anything.
`update_baseline` uploads the regenerated file as an artifact rather than
committing it, so a baseline change stays a reviewed commit. The job prints
`gaps.md` to its log as well as uploading it, so the comparison outlives the
artifact retention window.

If Chromium cannot be installed the job warns and continues: the run then
reports its pages as uncompared rather than as parity, and the Broiler-side
coverage and baseline comparison still stand.

## Current coverage

The fingerprinting page is where Broiler carries out most of what is asked of
it. The request- and storage-driven pages are largely `empty` today: their
probes depend on sub-resource loads, workers, sockets, and frames completing and
reporting back, which is the gap this baseline exists to track. Read the
per-page numbers in `report.md` rather than the total, which the 124-test
fingerprinting page dominates, and read `gaps.md` for which of those empty
probes Chromium answers — those are the ones that are Broiler's to fix.

The findings from the first two-engine run, and the issues opened from them, are
in [Privacy test page gaps](privacy-test-page-gaps.md).
