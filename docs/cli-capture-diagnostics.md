# Capture diagnostics

`Broiler.Cli` can record everything a capture touched, so that a page which
renders wrong can be diagnosed from evidence rather than from the rendered
result. It is off unless asked for, and it never changes what a capture
produces or what the process exits with.

```sh
dotnet run --project src/Broiler.Cli -- \
  --capture-image "https://www.google.com/search?q=broiler" \
  --output out.png \
  --diagnostic-dir artifacts/google-diag
```

## Why

A heavily-scripted page fails as a cascade. One missing API throws inside a
bootstrap, the bootstrap never reaches the code that would have built the
page, and what renders is a fragment. The fragment says nothing about which
script stopped, what it asked for, or whether the resources it needed even
arrived — and re-running the capture reproduces the outcome without producing
the evidence.

This bundle keeps the evidence instead of the conclusion: the failures as they
happened, the bytes of everything fetched and everything executed, and a digest
that ranks what went wrong.

## The arguments

| Argument | Effect |
| --- | --- |
| `--diagnostic-dir <DIR>` | Write the whole bundle to `<DIR>` |
| `--diagnostic-log <FILE>` | Write the JavaScript failure log to `FILE`. On its own it records only that log and archives nothing; with `--diagnostic-dir` it relocates the log out of the bundle |

Both work with `--url` and with `--capture-image`. They are independent of the
older `--diagnostics`, which prints the run's log entries to stdout as JSON and
archives nothing.

`--url` saves the page's source rather than rendering it, and runs inline
scripts against a window stub with no DOM. Its bundle is correspondingly
thinner: the document and the inline scripts with their failures, but no
external scripts, sub-resources or post-script DOM. Reach for `--capture-image`
when the question is why a page renders the way it does.

With more than one `--url`/`--capture-image`, each capture runs in its own child
process and gets its own sub-directory of `<DIR>`, named after its output file.
Concurrent captures cannot share one log file or one `resources/` directory
without interleaving into evidence that cannot be attributed to a page, so a
bare `--diagnostic-log` is rejected for a batch.

## What the bundle contains

```
<DIR>/
  javascript-errors.log   every JS failure, written and flushed as it happens
  console.log             console.log/warn/error/info in order
  summary.md              the digest: what failed, what was missing, what was slow
  diagnostics.json        all of the above, machine-readable
  resources/
    index.json            one row per resource: URL, kind, status, bytes, ms, error
    0000-www.google.com-search.html
    0001-www.google.com-xjs.js
    …
```

### `javascript-errors.log`

Every `JavaScript` log entry at `Warning` or above — thrown script errors,
`console.error`, `console.warn`, fetch failures, and the drain-budget warning —
with its exception and stack trace indented beneath it.

#### What reaches it, and what does not

| How the exception arises | Logged |
| --- | --- |
| Thrown at the top level of a script | yes |
| Thrown inside a timer callback | yes |
| Thrown inside an event listener | yes |
| A promise rejected with no handler | yes, **once the `Broiler.JS` patch is applied** |
| Caught by the page's own `try`/`catch` | **no** |

**A caught exception is not reported**, because the log sits at the host's
`catch` around script evaluation and a caught exception never reaches it. That
matches a browser — DevTools needs "pause on caught exceptions" to see these —
but it is worth knowing before reading a low failure count as good news. A page
whose bootstrap wraps everything in `try`/`catch` and reports through its own
error channel can fail throughout and log nothing here. When a bundle shows few
failures and a document the scripts barely changed, that pattern, not success,
is the first thing to suspect.

#### Reading a `BROILER_LOG_THROWS` log: most of it is the page

The counterpart to the table above is `BROILER_LOG_THROWS=1`, which makes a
**Debug** build write every `JSException.Throw` to stderr — caught or not, with
the .NET stack of the throw site. It is the only way to see the caught ones, and
the first thing to know about it is that on a heavily-scripted page **almost
everything in it is the page throwing on purpose**.

Google Search is the worked example. Its bot-detection VM signals "this register
is not set" by throwing a three-element array — `throw [lQ, 30, V]`, which the
log renders as `[object Object],30,440` — and catches it one frame up in its own
dispatch loop. It is control flow, not failure. On one saved results page:

| Engine | Throws | Of which `[lQ,30,N]` |
| --- | --- | --- |
| Chromium 1194 (CDP, pause-on-all-exceptions) | 165 | 153 |
| Broiler | 20 | 19 |

So a log full of `[object Object],30,N` says nothing on its own, and neither does
its *absence* of engine errors — a `ReferenceError` raised through
`JSEngine.NewReferenceError` is thrown by its caller and never passes through
`JSException.Throw`, so it does not appear here at all. Read the log for what is
**not** page-shaped:

```sh
BROILER_LOG_THROWS=1 dotnet run --project src/Broiler.Cli -c Debug -- \
  --capture-image <URL> --output out.png 2> throws.txt

grep -a '^\[JSException.Throw\]' throws.txt | sort | uniq -c | sort -rn | head -20
```

and then compare the **count** against a real browser on the same bytes rather
than judging it alone — the same page under Playwright's Chromium, with
`Debugger.setPauseOnExceptions: 'all'` and a `Debugger.resume` per pause, gives
the baseline. An order-of-magnitude gap is the finding; the shape of any one
entry usually is not. The same page replayed from `resources/` is what makes the
comparison controlled: the challenge a live capture is served differs per
request, and its throw count varies by three orders of magnitude between them
for reasons that have nothing to do with the engine.

**Unhandled promise rejections** need the patch under `patches/` that adds
`JSPromiseRejectionTracker` to `Broiler.JS`; the engine has no notion of them
otherwise. Without it the reporting compiles out and a rejected promise nobody
handled is lost — which on a promise-driven page is most of what goes wrong.
Check whether it is live with:

```sh
git -C Broiler.JS log --oneline --grep 'Report promises rejected with nobody'
```

It is written and flushed per entry, not buffered and dumped at exit. The runs
that most need diagnosing are the ones that hang, blow `--timeout` or die, and
those are exactly the runs a write-at-exit design leaves an empty file for.

### `resources/`

Every page, script, stylesheet, `fetch`/`XMLHttpRequest` response and
`iframe`/`object` sub-document the engine obtained, plus every script body it
executed — inline, `data:` URI and module — under the label the error log names
it by (`inline-7`, `deferred-0`, `module-2`).

A file whose bytes are already archived is not stored twice; its manifest row
points at the existing file. So a `<script src>` and the `inline-N` entry that
ran it share one file, and the manifest is therefore also the map from the label
in an error message to the URL the code came from.

The document is archived twice on purpose: as fetched, and again after every
script has run (`document-after-scripts`). Diffing the two is precisely what the
page's JavaScript did or failed to do.

`index.json` carries the outcome of each attempt, including the ones that
produced nothing — a 404, a refused connection, a blocked host. A resource that
never arrived is recorded as an attempt with an `Error` and no archived file.

### `summary.md`

- **What the scripts did to the document** — the byte count as fetched against
  the byte count after scripts. A scripted page that renders as a fragment
  usually shows a document the scripts barely changed, which points at the
  failures below rather than at layout or paint.
- **Platform features the page asked for and did not get** — identifiers pulled
  out of the failure messages (`X is not defined` → a missing global,
  `Cannot get property y of undefined` → a missing property, `X is not a
  function` → a missing callable), ranked by how often each was blamed. This is
  the work list for making the page run.
- **Distinct failures, most frequent first** — grouped by what went wrong rather
  than where, so one missing global that broke an inline script and a deferred
  one counts once. Script labels and per-occurrence numbers are blanked before
  grouping.
- **Resources that failed to load**, and the **slowest resources**.

## Where the recording happens

The fetches are spread across three assemblies, and none of them may reference
the CLI. `ResourceTrace`, in `Broiler.HtmlBridge.Core`, sits below all three:
a fetch site records into it, and the CLI listens.

| Recorded at | Covers |
| --- | --- |
| `CaptureService` / `LinkNavigator` | The top-level document, and a followed first link |
| `CaptureService.ExecuteScriptsWithDom` | Every script body as executed, and the post-script DOM |
| `ScriptExtractionService.FetchResolvedScript` | External `<script src>`, on the inline path and the prefetch worker alike |
| `ResourceLoader.LoadTextDirect` | External stylesheets and other text sub-resources |
| `FetchBinding` | `fetch()`, `XMLHttpRequest`, `navigator.sendBeacon` |
| `DomBridge` sub-documents | `iframe`/`object` documents |

Images, fonts and other binary sub-resources are not archived. They are loaded
by `ImageLoadHandler` in the `Broiler.HTML` submodule rather than through the
bridge's loader, so recording them would mean a submodule change; the render
log still reports their failures.

## Cost, and what it cannot do

With no bundle requested, a traced call site reads one static field and returns
a `default` struct — nothing is timed, allocated or decoded. When a bundle *is*
requested, the run pays the archive writes and holds up to 50,000 log entries in
memory; beyond that, entries stop being retained for `diagnostics.json` and
`summary.md` while the streamed log keeps everything.

Diagnostics never participate in the run they observe. Every handler swallows
its own I/O failures and `ResourceTrace` swallows what a handler throws, so an
unwritable path or a full disk degrades the bundle and nothing else. The one
deliberate exception is startup: a destination that cannot be created fails the
command outright, because a mistyped path is the one diagnostics mistake that is
otherwise invisible.

Archived resources are written verbatim, including URLs with query strings and
any response bodies — treat a bundle as you would the page it came from.
