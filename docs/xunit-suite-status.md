# xUnit suite status

What `dotnet test` reports across the main repo's xUnit projects, why the failures
are there, and what it takes to run the suite at all. Measured on Linux, 4 cores,
16 GB, .NET SDK 10.0.302, submodules at the pinned pointers.

Keep it current when a project's result changes — the point of the file is that
"is this failure mine?" is answerable without a bisect.

## Nothing in CI runs `dotnet test`

`.github/workflows/` holds six workflows — preview packaging, NuGet packaging,
Octane, real-world renders, WPT reftests and WPT tests. None of them runs the
xUnit suite. The WPT workflows run `src/Broiler.Wpt` (the standards runner) and
score it against `tests/wpt-baseline`; that is a different thing from
`Broiler.Wpt.Tests`, which is the hand-written xUnit coverage *of* that runner
and is not gated anywhere.

So a change can turn an xUnit test red and land. That is the mechanism behind
most of what is listed below, and it is worth fixing before the list is.

## Where it stands

29 xUnit projects, ~4 800 `[Fact]`s plus theory cases. 27 are green:

| Project group | Projects | Result |
| --- | --- | --- |
| `Broiler.Documents.*.Tests` | 7 | green |
| `Broiler.UI.*.Tests` | 11 | green |
| `Broiler.Layout.Tests` | 1 | green — 1 126 tests |
| `src/*.Tests` except the two below | 8 | green |
| `src/Broiler.Wpt.Tests` | 1 | **54 failing** of 1 083 |
| `src/Broiler.Cli.Tests` | 1 | **52 failing** of 3 485 (48 distinct tests) |

`Broiler.Media.*` and `Broiler.Input.*` also live under `*.Tests` names but are
not xUnit — they are `Exe` projects with their own `Main`, and nothing here
covers them.

## These failures are not new

A 372-test slice covering nine of the failing classes was run against a build of
`bd23ca0` — the oldest commit this repository's history contains (2026-08-17;
`main` is force-rewritten as changes land, so that is the whole visible history).
It produced the same failure set there, give or take two tests fixed since. So
for the classes checked there is no regression point to bisect to, and the suite
has not been green in any history available here.

The ones that *were* traceable to a change are fixed, and they share a shape:
a deliberate behaviour change landed with its own reasoning, and the test that
pinned the old behaviour was left behind.

* **`NamedPageTests.A_Page_Name_Inside_An_Out_Of_Flow_Subtree_Breaks_Nothing`**
  asserted that a page-name change inside an out-of-flow subtree does not break.
  "Break on a page name inside an out-of-flow subtree" made it break on purpose,
  adjudicated the two WPT tests that state the rule opposite ways, and wrote the
  losing one up in `docs/wpt-rendering-gaps-wont-fix.md` — without touching this
  test. It now pins the rule that shipped, alongside a new one for the half that
  still holds (an out-of-flow box's own name stays out of its parent's flow).
* **`GraphicsAbstractionTests`** (3 tests) asserted nearest-neighbour sampling
  on an upscaled blit. `Broiler.HTML`'s "Filter a bitmap when it is drawn at a
  size other than its own" made scaled blits bilinear on purpose and measured
  the gain; the tests now draw 1:1, which is what they were pinning.
* **`WptTestRunnerTests.RunTempScriptExecution`** reflected into
  `ExecuteScriptsWithDom` and cast its result to `string`. That method returns an
  `ExecutedDocument` now — HTML *or* a `DomDocument`, whichever the render path
  wants — so six tests failed before reaching their own assertions.
* **`CssExtractionPhaseZeroTests`** (3 tests) had been overtaken by the
  retirement they guard: a directory the test enumerates is gone (so
  `EnumerateFiles` threw where an empty directory would have passed), `CssData`
  shed two more members, and `Broiler.Layout`'s friend surface grew by five
  grants that each carry their reason in the csproj beside them.
* **`Broiler.Browser.Core.Tests`** did not compile at all —
  `HtmlDocumentParser.ParseDocument` is static and one call site still
  constructed a parser (CS0176), the same fix `Broiler.Documents.Html` and
  `Broiler.Cli.Tests` already took. `Broiler.DOM`'s own `Broiler.Dom.Html.Tests`
  has it too, in two files; that fix is `patches/0001-…` (its remote is outside
  this session's scope, so the pointer is not bumped) and takes that project
  from "does not build" to 41 green tests.

## `src/Broiler.Wpt.Tests` — 54

| Kind | Count | What it means |
| --- | --- | --- |
| Pixel comparison below threshold | 31 | The render differs from the checked-in Chromium reference by more than 1%. Real layout/paint gaps: `align-content-block-00{2,4,6,8,10}` land at 92–96% with content displaced by tens of pixels, the six `position-area-*` tests at 90–96%, `writing-modes/select[size]` at 55.6%. |
| Scripted DOM / geometry assertion | 21 | A feature the test drives from script does not behave as asserted: SVG hit-testing through `elementFromPoint`, `scrollIntoView` axis mapping under writing modes, `position-visibility`, `position-try` fallbacks, keyframes collected from a `<style>` element's text content. |
| Wall-clock | 2 | `ScrollWriteGeometryTimeoutTests.OverflowAlignment_ScrollWrites_RenderWellWithinRunnerTimeout` (20 s budget) and `WptTestRunnerTests.RunTestWithTimeout_GridTemplateColumnsCrash_Completes_Without_Timing_Out` (6 s). Both are sensitive to what else is running on the box. |

The scripted-DOM group overlaps `Broiler.Cli.Tests` almost test for test — the
same features are covered on both sides of the bridge, so a fix there closes
two failures at once. `Wpt_CssomView_ElementFromPoint_Uses_Svg_Groups_…` and
`GoogleSearchPolyfillTests.Document_HitTesting_Uses_Svg_Groups_…` are the same
assertion.

## `src/Broiler.Cli.Tests` — 48 distinct tests (52 cases)

| Kind | Count | Examples |
| --- | --- | --- |
| Engine feature gaps | 37 | `GoogleSearchPolyfillTests` alone is 11 of them: SVG hit-testing, `scrollIntoView` under writing modes, `ch`/`lh`/`rlh` under `zoom`, scroll-offset clamping. The rest spread over `background-clip: border-area`, `var()` in shorthands, `:lang` against `xml:lang`, grid/flex content sizing, serialization of mutated iframe scroll state, and the sub-document module drain described below. |
| Acid targets not yet met | 4 | Acid3 scores 96; `PhaseD_…_At_Least_97` and `PhaseE_…_At_Least_100` state the targets, and `V7_Acid3_Image_Capture_Produces_Valid_Output` asserts 100. These are goals, not regressions. |
| Architecture guards the code has drifted past | 4 | `HtmlBridgeArchitectureGuardTests` (files over the 750-line limit), `HtmlBridgeBoundaryGuardTests` (the frozen `Broiler.JavaScript.*` dependency set has grown to three), `DomWrapperFunctionTests` (`new JSFunction(` where `DomFunction` is wanted), `CssExtractionPhaseThreeTests`. Each names the refactor it wants; none can be closed by editing the test, which is the difference between these and the `CssExtractionPhaseZeroTests` fixed above. |
| Missing artifact | 1 | `AcidRenderComparisonInfrastructureTests.Acid_Umbrella_Roadmap_Covers_All_Three_Tests` requires `docs/roadmap/acid-test-triage.md` with `## Acid1`, `## Acid2` and `## Acid3` headings. No such file has existed in this history, and writing one to satisfy the assertion would be inventing the plan it is meant to check. |
| Environmental | 2 | `PdfToWordConverterTests` needs the `Broiler.Pdf` app, which a bare container does not build. |

One more is load-sensitive rather than broken:
`ScriptCompileAheadOverlapTests.Every_Source_Is_Compiled_By_A_Worker_When_The_Budget_Is_On`
failed in one full run and passed in the next and in isolation.

## Running it

* **The Phase 0 fixture pins an SDK feature band.**
  `tests/broiler-code-phase0/fixture/global.json` asks for 10.0.302 with
  `rollForward: latestFeature`, which will not roll *down* a band — so the
  Ubuntu archive's 10.0.1xx does not satisfy it and
  `Broiler.Code.Language.CSharp.Tests` reports "A compatible .NET SDK was not
  found" for two tests. `.claude/hooks/session-start.sh` installs a satisfying
  SDK beside whatever is on PATH; `cd tests/broiler-code-phase0/fixture &&
  dotnet --version` is the check.
* **`Broiler.Cli.Tests` used to hang, and it was one test.**
  `SubDocumentEngineModuleTests.Iframe_Module_Runs_Through_Engine_Path_When_Parent_Is_ModuleContext`
  assigns an iframe's `srcdoc` from script, so `DomBridge.ExecuteSubDocumentScripts`
  ran the frame's ES-module root *while the parent's script was still on the
  stack* — and awaited it synchronously. The engine queues a module's
  continuations to run when the outermost execution finishes, which is the call
  that was blocking, so the thread deadlocked. It is a hang rather than a
  failure, and a synchronous `[Fact]` at that: xUnit 2.5.3 ignores `Timeout` on
  one, so nothing ever cancelled it and no test scheduled after it in the run
  executed. That is why the project appeared to stop around test ~3 200 with a
  large heap — the heap was a symptom of a run that never ended, not the cause.

  The module root is now started and not awaited (it is deferred anyway), with
  a continuation to report a failure that would otherwise go unobserved. The
  test now *fails* in two seconds instead of hanging: the frame's DOM effect
  needs a drain that nothing in the test performs, which is a real gap in the
  engine-only module path and is listed above. With that unblocked the project
  runs end to end for the first time — 3 485 tests in 5.3 minutes.
* **`ScriptCompileAheadOverlapTests` is a benchmark, not a unit test.** Its
  collection takes the test host from nothing to ~8.7 GB in 140 seconds — it
  compiles ~60 000 functions across its repetitions, and the memory is
  *reachable*, not garbage (an aggressive blocking compacting `GC.Collect`
  after every test in the class recovers almost none of it). All 49 tests pass,
  and a retention that size in the script-compile path is worth a look on its
  own account. Filter the collection out when running the project for a quick
  answer.
* Some `Wpt_*_MatchesReference` tests and the PDF conversion tests can fail in a
  bare container for environmental reasons. Baseline before attributing a
  failure to your change.
