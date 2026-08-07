# WPT reftests — Broiler against WPT's own references

The [WPT Reftests](../.github/workflows/wpt-reftests.yml) workflow runs the half
of the Web Platform Tests corpus that carries its own answer, and decides each
test **without any other rendering engine**.

## What a reftest is, and why it needs no reference image

A WPT reftest states its correct rendering inside the checkout, as a link in the
test document:

```html
<link rel="match"    href="foo-ref.html">   <!-- must render identically to this -->
<link rel="mismatch" href="bar-notref.html"><!-- must NOT render identically to this -->
```

The reference is deliberately written to be simple — it produces the same picture
using only features the test does not exercise. So the pair can be checked by one
engine on its own: render the test, render the reference, compare the two
bitmaps. That is what this suite does, with Broiler on both sides.

The main suite ([WPT Tests](../.github/workflows/wpt-tests.yml)) works the other
way: every shard downloads Chromium through Playwright, screenshots each test,
and compares Broiler's render against that PNG. The two suites answer different
questions and their pass rates are **not comparable**.

|                         | `wpt-tests.yml` (golden images)             | `wpt-reftests.yml` (this suite)                    |
| ----------------------- | ------------------------------------------- | -------------------------------------------------- |
| Baseline                | Chromium screenshot of the test             | Broiler's render of the test's declared reference   |
| Tests run               | every discovered document                   | only those declaring a reference                    |
| Needs a browser         | yes (Playwright Chromium, cached)           | no                                                  |
| A failure can be        | a real bug, **or** a font/scrollbar/AA difference between engines, **or** a stale golden | a real disagreement with what WPT says the test should look like |
| Blind spot              | Chromium missing the feature under test     | a bug that hits the test and its reference alike    |

The trade is worth stating plainly: this suite cannot catch a defect that damages
the test and its reference identically, because both go through the same
renderer. In exchange nothing is ever attributed to Broiler that is really a
cross-engine rendering difference, and a run needs no browser download — which
also removes the browser-provisioning outage the golden-image suite has to defend
against ([issue #1534](https://github.com/Broiler-Platform/Broiler/issues/1534)).

## Running it

CI: **Actions → WPT Reftests → Run workflow.** Inputs mirror the golden-image
suite — `subset`, `shard_index`, `rerun_failed_only`, `test_timeout_seconds`,
`memory_limit_mb`, `pass_threshold`, and the two issue-size limits.

Locally:

```sh
# Every reftest in a checkout
./scripts/run-wpt-reftests.sh --wpt-dir tests/wpt/checkout

# One directory, with the failing renders saved for triage
./scripts/run-wpt-reftests.sh --wpt-dir tests/wpt/checkout \
    --subset "css/css-flexbox" --failure-images /tmp/reftest-failures
```

Or the runner directly:

```sh
dotnet run --project src/Broiler.Wpt -- \
    --wpt-dir tests/wpt/checkout --reftests-only [--subset <path>]
```

`--reftests-only` is the whole switch: it narrows discovery to tests with a
resolvable reference and decides each one by rendering both sides.
`--reference-dir` is ignored in this mode and nothing reads or writes the
reference-image tree.

## How a test is decided

Implemented in
[`WptTestRunner.RunReferenceTest`](../src/Broiler.Wpt/WptTestRunner.cs).

1. **Membership.** A test is in the suite when it declares at least one
   `rel="match"` / `rel="mismatch"` href that resolves to a file present on disk
   (root-relative hrefs map under the WPT root; query and fragment are stripped).
   Everything else — including a test whose href dangles — is dropped before the
   run rather than reported as skipped, so the totals describe only tests the
   suite could actually decide.
2. **Render.** The test is rendered by Broiler. A reference href naming a bitmap
   (`.png`, `.jpg`, …) is decoded as-is; any other reference is rendered by
   Broiler too.
3. **Compare** at the run's pixel pass threshold (99% by default, i.e. at most 1%
   of pixels may differ) — the same threshold and comparer the golden-image suite
   uses.
4. **Verdict.** A `rel="match"` reference must be reproduced; with several of
   them the test passes on the first one it reproduces (WPT's own rule) and the
   closest candidate is what a failure reports. A test whose references are all
   `rel="mismatch"` passes only when it differs from every one of them.

Manual tests, variant tests, and media-playback tests are skipped exactly as in
the golden-image path. Reference *chains* — a reference that itself declares a
reference — are not followed; the declared reference is rendered as written,
which is what the chain asserts it looks like anyway.

## CI shape

The workflow is a copy of `wpt-tests.yml` with the Chromium stage removed, so
everything downstream is shared rather than reimplemented:

- 8 deterministic shards, the same FNV-1a(relative-path) assignment.
- One composite action, [`run-wpt-reftest-shard`](../.github/actions/run-wpt-reftest-shard/action.yml),
  used by both the initial pass and the end-of-workflow retry pass, so a retried
  shard is measured exactly like the attempt it replaces.
- `scripts/merge-wpt-shards.py` merges the shards, detects shards that aborted
  abnormally, files the two failure issues, and folds failures into the manifest.
- Its own rerun manifest, `tests/wpt-baseline/failed-reftests.json` — a reftest
  failure and a golden-image failure are different measurements of the same test,
  so they must not overwrite each other.
- Issues from this suite are titled `WPT reftest run: …` so they are never
  confused with the golden-image suite's.

Two things the reftest shard deliberately does *not* inherit: the Playwright and
reference caches (there is nothing to cache), and `BROILER_WPT_DEFER_PROMISE_TESTS`
— that override exists to freeze the DOM at the point Chromium's reference
generator screenshots, and a reftest has no Chromium capture to line up with.
