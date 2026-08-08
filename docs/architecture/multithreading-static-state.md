# Shared mutable state on the render path

The P0-c audit from the [multithreading roadmap](multithreading.md#p0-c--shared-cache-thread-safety-audit):
what a second thread running layout or paint would reach, what it must establish
before it may run, and which of it is actually a race.

Re-derive the enumeration with:

```sh
python3 scripts/audit-mutable-statics.py --json /tmp/statics.json
```

The script is syntactic and deliberately over-reports; the classification below is
the judgement it cannot make.

## What the enumeration found

Over the nine render-path projects (`Broiler.Dom`, `Broiler.Dom.Html`,
`Broiler.CSS`, `Broiler.CSS.Dom`, `Broiler.Layout`, `Broiler.Graphics`,
`Broiler.Media.Image`, `Broiler.Media.Image.Managed`, `Broiler.HTML/Source`):

| Component | files | assignable | mutable container | concurrent container | shared instance root | thread-static |
|---|---:|---:|---:|---:|---:|---:|
| CSS | 12 | 0 | 8 | 0 | 17 | 8 |
| DOM | 4 | 0 | 8 | 0 | 4 | 0 |
| Graphics | 7 | 2 | 1 | 0 | 4 | 1 |
| HTML | 17 | 3 | 2 | 0 | 26 | 0 |
| Layout | 9 | 0 | 2 | 0 | 2 | 9 |
| Media | 3 | 1 | 0 | 0 | 2 | 0 |
| **total** | 52 | 6 | 21 | 0 | 55 | 18 |

**The roadmap's framing of this item is wrong in a way worth correcting, and its
own headline example is the proof.** P0-c asks for the *mutable statics* on the
render path and sizes the job by counting files that declare one. Enumerated, the
static-field surface is almost entirely harmless: 21 of the 27 non-thread-static
entries are lookup tables filled by a static initialiser and never written again
(`HtmlDocumentParser.VoidElements`, `CssValueParser.NamedColors`,
`CssUtils.BlockLevelHtmlTags`), and concurrent reads of those are safe. Six fields
are assignable and every one of them is a set-once initialisation latch or
deliberate process-wide state.

The state that actually corrupts is **instance** state on a process-wide singleton,
which a scan for `static` fields does not see at all. Item #9 — the roadmap's own
example — is exactly that: `FontsHandler`'s four caches are

```csharp
private readonly Dictionary<string, Dictionary<double, Dictionary<FontStyle, RFont>>> _fontsCache = new(...);
```

instance fields on the `FontsHandler` owned by the `RAdapter` behind the
process-wide `CompatProvider.ImageAdapter`. `BImageRenderer._images`, the other
site the roadmap names, is `private readonly Dictionary<ulong, BBitmap>` — instance
again. Neither is a static. The audit script therefore reports **shared instance
roots** as their own bucket: a `static readonly` field whose type is not a scalar is
an entry point into an object graph that every thread shares, and the classification
question moves inside it.

## The ambient state a worker thread must establish

Eighteen `[ThreadStatic]` fields, in three groups. These are not races — they are a
contract, and the failure mode is that breaking it is **silent**. A worker thread
that establishes none of them does not throw; it reads defaults, and defaults are
plausible: a zero viewport makes every `vh`/`vw` length zero, an unset quirks flag
means standards mode, an empty SVG table resolves every `url(#id)` to nothing. All
three produce a wrong render that looks like a rendering bug rather than a
threading one.

| Slot | Fields | Established by | Read by |
|---|---|---|---|
| `Viewport` | `CssLengthParser._vhFactor`, `_vwFactor`, `_vminFactor`, `_vmaxFactor`, `_viFactor`, `_vbFactor` (+2 zoom/`rem` factors) | `CssLengthParser.SetViewportSize(w, h, rootWritingMode)` | every viewport-relative length in layout |
| `DocumentMode` | `DocumentModeContext._quirksMode` | `DocumentModeContext.CurrentQuirksMode` | root/body sizing (the quirks fill-viewport behaviour) |
| `SvgTables` | `SvgFilterTable._table`, `._colorTable`; `SvgClipPathTable._table` | `FragmentTreeBuilder.Build` | `SvgRenderer`, the HTML paint walker |

`CssBoxProperties._verticalFrameLayoutDepth` is the nineteenth `[ThreadStatic]` and
is **not** in the table: it is a re-entrancy counter scoped to a single call chain,
not state a thread inherits, so a fresh thread starting at zero is correct.

### The contract, and its check

[`Broiler.Layout/AmbientRenderState.cs`](../../Broiler.Layout/Broiler.Layout/AmbientRenderState.cs)
is the single call a worker thread makes —

```csharp
AmbientRenderState.Establish(viewportWidth, viewportHeight, rootWritingMode, quirksMode);
```

— and the debug-mode assertion P0-c's exit gate asks for. The record is kept **per
slot** rather than as one "this thread is ready" bit, because establishing the
viewport does not establish the quirks flag, and a single bit would let a reader of
one slot be satisfied by a writer of another — which is the mistake the assertion
exists to find. `AmbientRenderStateTests` exercises it on a real second thread; a
test that simulated the failure by clearing the record on the main thread would pass
against an implementation that stored the record in a plain `static`.

### Two things the assertion is not, and why

**It is off by default** (`AmbientRenderState.EnforceOnThisThread`, which is thread-scoped so a test cannot arm it for a layout test running beside it). Turning it on
unconditionally fails every render in the repository, because
`DocumentModeContext.CurrentQuirksMode` is **never written on the HTML-string render
path** — only the HtmlBridge DOM path publishes it
(`src/Broiler.HtmlBridge.Dom/DomBridge.HtmlParsing.cs:48`). Single-threaded that is
harmless: the thread-static default is `false`, and standards mode is what the
string path wants. But `DocumentModeContext`'s own class comment claims "each parse
overwrites it, so a stale `true` never leaks into a later standards-mode render",
and **that holds only because one thread renders one document at a time**. On a
pooled thread that previously rendered a quirks-mode document through the DOM path,
it does not hold. The switch is there for the work that introduces worker threads
to turn on; leaving it on now would report a currently-unreachable defect as a test
failure across the suite.

**The `Viewport` slot has no read-side assertion yet.** `CssLengthParser` is in the
`Broiler.CSS` submodule, and instrumenting its eight thread-statics needs a patch
there rather than a main-repo edit. `AmbientRenderState.Establish` fills the slot and
records it, so a thread that goes through the contract is covered; a thread that
calls `CssLengthParser.SetViewportSize` directly — which `HtmlContainerInt.PerformLayout`
does today — is not credited for it and would assert if a reader were instrumented.
Closing this means one Broiler.CSS patch: mark the slot from `SetViewportSize`, assert
from the factor reads.

## Classification

### Safe: initialise-once lookup tables (21 sites)

Filled by a static initialiser or collection expression at type load and never
written again. Concurrent reads of a `Dictionary`/`HashSet` that nobody writes are
safe, and the CLR's type-initialisation lock makes the fill itself a single-threaded
event with a release barrier.

`HtmlDocumentParser` (7: `VoidElements`, `StructuralTags`, `HeadMetadataElements`,
`PClosers`, `TableElements`, `TableChildElements`, `FormattingElements`),
`HtmlTokenizer.RawTextElements`, `CssValueParser.NamedColors`,
`CssSelectorMatcher.RecognizedPseudoClasses`, `CssStyleEngine.Supports`
(4: `ColorFunctions`, `NamedColors`, `SystemColors`, `KnownProperties`),
`CssStyleEngine.Values.LengthPercentageProperties`,
`CssStyleEngine.BorderResetShorthands`, `CssLayoutEngineTable.BorderStyleRank`,
`CssUtils.BlockLevelHtmlTags`, `BColor.Named.s_named`, `HtmlUtils._list`.

**Two of the twenty-one are written after initialisation, and both are already
synchronised.** `BroilerFontRegistry.LoadedFamilies` is a `HashSet<string>` mutated
by `RegisterFontFile` — every access is inside `lock (Sync)`. `HtmlUtils._list` looks
like the same shape and is not: it is only ever read by `IsSingleTag`.

### Safe: set-once initialisation latches (6 assignable statics)

| Site | Why it is not a race |
|---|---|
| `BImageCodecs._catalog` | Registered once at host startup, read thereafter. |
| `BTextMetrics._provider` | Same; defaults to `FallbackProvider` so an unset read is still valid. |
| `CompatProvider._defaultProvider`, `_defaultLoadAttempted` | The `_defaultLoadAttempted` `int` is the interlocked latch that makes the `Assembly.Load` happen once. |
| `CommonUtils._tempPath` | Set once from configuration. `public` and assignable, which is a smell, but not a render-path write. |
| `ImageAnimationClock._presentationTimeTicks` | **Deliberately process-wide** and documented as such: image loading is dispatched across threads, so a thread-static clock would give a worker a different frame than the thread that requested it. |

### The real work: shared instance roots

These are the sites parallel rendering has to deal with, and none of them is a
`static` field.

| Root | Instance state | Status |
|---|---|---|
| `CompatProvider.ImageAdapter` → `RAdapter._fontsHandler` | Four unsynchronised `Dictionary` caches, one of them a triply-nested map | **Item #9.** Partly addressed: `patches/0114-graphics-font-cache-thread-safety.patch` makes `FallbackSystemFont`'s four caches `ConcurrentDictionary`. `FontsHandler`'s own four are still plain `Dictionary`. |
| `BImageRenderer._images` | `Dictionary<ulong, BBitmap>` | Named by the roadmap; unaddressed. Blocks item #8 (concurrent image decode). |
| `CssStyleEngine` `_cache`, `_sparseCache`, `_declaredCascadeCache` | Behind one `_sync` | Correct but serialising. The roadmap's own step 2 for Broiler.CSS is to shard it; see the measured cascade cost below for why this matters more than it looks. |
| `HandlerFactory.Instance`, `RGraphicsRasterBackend.Instance` | None — both are stateless dispatchers whose per-call state is in parameters | Safe as they stand. Worth re-checking if either grows a field. |
| `HttpClient` (2 sites) | Thread-safe by contract | Safe. |
| `ConditionalWeakTable<CssStyleSheet, …>` (2 sites) | Documented thread-safe | Safe. |
| `Lazy<FallbackSystemFont?>` | See row 1 | `Lazy<T>` publication is safe; the object it publishes was not. |
| `CompatProvider` `AsyncLocal<ICompatProvider?>` | Flows across `await`, per logical call context | Correct by construction for the current design; note it is *not* per-thread, so a worker started with `Thread.Start` does not inherit it while one started from a `Task` does. |

## What this changes about the roadmap

1. **P0-c's scope estimate is measuring the wrong thing.** Counting files with a
   mutable private static (the roadmap's "Broiler.JS 306, `src/` 118, Broiler.HTML
   67…") does not size this job, because the hazard is not in static fields. The
   render path has 27 non-thread-static ones and 21 of them are frozen lookup tables.
2. **The exit-gate list is short and specific**, which is good news: three ambient
   slots and eight shared instance roots, of which two (`FontsHandler`,
   `BImageRenderer._images`) are genuine unsynchronised caches on the render path.
3. **`DocumentModeContext` has a latent per-thread defect today**, documented above.
   It is unreachable single-threaded and becomes reachable with the first pooled
   render thread. It is cheap to fix — publish the flag on the string path too — and
   should be fixed before, not after, item #13.
