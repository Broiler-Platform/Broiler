# html5test.com — the rendering exceptions

Rendering <https://html5test.com/> has been reported twice, each time as four distinct JavaScript
exceptions. This is what each one actually is, what was fixed, and what was deliberately left alone.

Source of the page under test: <https://github.com/WebPlatformTest/HTML5test>.

## Reading the stack traces

Broiler labels each script it executes `inline-{i}`, indexed over the document's `<script>`
elements in order (`ScriptEngine.ExecuteDetailed`, `src/Broiler.HtmlBridge.Scripting/ScriptEngine.cs`).
For html5test that gives:

| label | script |
| --- | --- |
| `inline-0` | `scripts/base.js` |
| `inline-1` | `scripts/8/engine.js` — the feature-probe suite |
| `inline-2` | `scripts/8/data.js` |
| `inline-3` | the analytics inline script |
| `inline-4` | Cloudflare's `email-decode.min.js` |
| `inline-5` | the page's trailing inline script (`waitForWhichBrowser` / `start`) |

So every reported failure is in `engine.js`, reached through `Runner` (`engine.js:4423`) and the
`testsuite[s](this.list)` dispatch loop (`engine.js:4512`).

**Line numbers are the live file's, not GitHub's.** The copy html5test.com serves carries one extra
line near the top (a bare `Test =` on line 2) that the repository's does not, so every line below it
is off by one between the two. Fetch `https://html5test.com/scripts/8/engine.js` when checking a
trace.

## None of them aborted the page

Every reported site sits inside html5test's own `try { … } catch (e) { }`. That is not incidental —
`Runner.initialize` wraps the whole testsuite loop in a single `try { … } catch (e) { error(e) }`
(`engine.js:4423-4519`), so had the first exception escaped, the run would have stopped there and the
others could never have been reported. Four reports is itself the proof that all four were caught.

They are feature-detection probes: html5test deliberately provokes a throw and reads the failure as
"unsupported".

**Where the report came from matters.** Broiler's own capture diagnostics do not list these. The only
place a JS error is ever logged is the host `catch` around `context.Eval(script, label)` —
`src/Broiler.Cli/CaptureService.cs` and the equivalents in
`src/Broiler.HtmlBridge.Scripting/ScriptEngine.cs` — that is, the *unwind* boundary, reached only when
an exception escapes an entire script. There is no throw-time hook anywhere: `Broiler.JS` never
reports, it only *constructs* a `JSException` carrying an origin frame. A caught exception therefore
produces no `javascript-errors.log` entry at all, and a `--diagnostic-dir` capture of this page reports
**0 JavaScript failures** both before and after every fix below.

The reported traces interleave C# frames (`JSUndefined.cs:62`) with JS frames and name Windows paths
(`D:\Broiler\…`), which is what a **first-chance** observer sees: every `JSException` at the moment it
is constructed, caught or not. That is a strictly noisier view than the capture diagnostics, and
reading it as a list of page failures overstates what happened — none of these stopped html5test, and
one of them (WebRTC) is the correct answer.

That does not make them uninteresting — they are real capability gaps, and several were outright
defects — but it does mean none of them was an emergency.

---

# Third report: `1986` alone

The canvas fix below landed, and `3030`, `3055` and `3071` are gone from the report with it. What
remains is the WebRTC probe, on its own — and it is the one of the four that was never a defect. Its
message is now fixed too (see the section on it); the throw itself is correct and stays.

# Second report: `1986`, `3030`, `3055`, `3071`

Two of the first report's four are gone, which is the first thing worth reading off it: the
`attributes[0]` and `HTMLElement` fixes below landed and their probes no longer throw. What is left is
**two root causes**, not four:

| trace | probe | cause |
| --- | --- | --- |
| `engine.js:1986` | `new (window.RTCPeerConnection \|\| …)(null)` | no WebRTC — **not a defect** |
| `engine.js:3030` | `ctx.getImageData(0, 0, 1, 1)` | canvas had no pixels — **fixed** |
| `engine.js:3055` | `canvas.toDataURL('image/png')` | canvas had no pixels — **fixed** |
| `engine.js:3071` | `canvas.toDataURL('image/jpeg')` | canvas had no pixels — **fixed** |

Two further sites, `engine.js:3087` (`image/vnd.ms-photo`) and `:3103` (`image/webp`), are the same
`toDataURL` call and threw identically; they are absent from the report. Why is not determinable from
this side — the observer that produced it is not code in this repository, and its de-duplication is not
described by the traces (each block repeats its frame list two or three times, with no consistent
relation to the number of call sites). They are fixed by the same change and are covered by a test.

## (1) `TypeError: cannot create instance of undefined` — `engine.js:1986` — **not a defect**

```js
o = new (window.RTCPeerConnection || window.msRTCPeerConnection || window.mozRTCPeerConnection || window.webkitRTCPeerConnection)(null);
```

All four are undefined, so this is `new undefined(…)`. Broiler implements no WebRTC surface anywhere —
no `RTCPeerConnection`, no `RTCDataChannel`, no `navigator.mediaDevices`, no `getUserMedia`, in the main
repo or any submodule. `webrtc/*` is listed in `tests/wpt-baseline/failed-tests.json` as a durable
expected failure, and WebRTC does not appear in `docs/ROADMAP.md`. The whole `Peer To Peer` section of
html5test scores 0/45 and did so before and after this round.

This is the expected outcome of a feature probe against an engine without the feature — a browser with
WebRTC disabled throws here too. Nothing to fix; implementing WebRTC is a feature project (an
ICE/DTLS/SCTP stack), not a bug fix.

### The message was wrong even though the throw was right — **fixed**

Broiler worded this `cannot create instance of undefined`. No browser does: V8, SpiderMonkey and JSC
all say `undefined is not a constructor`, and so does the rest of this engine — `JSFunction`,
`JSSymbol`, `JSGenerator`, `JSGeneratorFunctionV2`, `JSReflect` and `JSPromisePrototype` all raise
`… is not a constructor`. `JSUndefined.CreateInstance` and its `JSNull` sibling were the only two
sites left with a wording of their own.

That is worth fixing precisely *because* the throw is correct. This trace is the engine giving the
right answer to a feature probe, and a message no browser produces makes it read as an engine fault —
which is exactly how it arrived here, twice. `InvokeFunction`'s `undefined is not a function` is
deliberately untouched: that one already matches the browsers.

Both sites live in the `Broiler.JS` submodule, whose remote is outside this session's GitHub scope, so
the fix ships through the patch workflow rather than as a pointer bump — the commit
*"Say "is not a constructor", as every other construct site does"*, exported under `patches/` with its
tests (`ConstructNonConstructorMessageTests`). Nothing in the main repository asserts either message,
so the engine behaves identically until a maintainer applies it; the cost of not applying it is a
non-standard string in a stack trace.

**This does not remove the exception, and nothing short of implementing WebRTC would.** The page
evaluates `new (A || B || C || D)(null)` where all four are undefined; the only way not to throw is to
define one of them, and defining one that cannot open a peer connection would be the false claim of
support that the canvas section above spends its length avoiding. A browser with WebRTC disabled
throws here too. The 45 points html5test allots to `Peer To Peer` require a working implementation, not
a name.

## (2) `undefined is not a function` — `engine.js:3030`, `3055`, `3071` — **fixed**

```js
var data = ctx.getImageData(0, 0, 1, 1);                                   // 3030
passed = canvas.toDataURL('image/png').substring(5, 14) == 'image/png';    // 3055
passed = canvas.toDataURL('image/jpeg').substring(5, 15) == 'image/jpeg';  // 3071
```

`getImageData`, `putImageData`, `createImageData`, `ImageData` and `toDataURL` were all absent. The
absence was structural rather than a missing entry in a member list: the 2D context was a **style-state
stub with no pixels**. `CanvasRenderingContext2D` kept `fillStyle`, `lineWidth`, `font` and the
save/restore stack, and every drawing method was a literal empty body —
`public void FillRect(float x, float y, float width, float height) { }`. Phase 6 had removed a
`CanvasDrawCommand` recorder that no renderer ever read, and what remained recorded nothing. So there
was no backing store for `getImageData` to read or for `toDataURL` to encode.

**The previous round deliberately did not stub them**, and that call was right at the time: returning
zeroed pixels would have converted an honest `TypeError` — which every feature detector on the web
reads correctly as "no canvas readback" — into a false claim of support, and pages that branch on
`typeof ctx.getImageData === 'function'` would then take the canvas path and silently render nothing.
Absent is the safer answer *until the context can actually rasterise*. That is the condition this round
removes.

### The fix: the context has a bitmap

`CanvasRenderingContext2D` now owns a `Broiler.Graphics` `BBitmap` the size of the canvas and
rasterises into it through `BCanvas`, the CPU raster canvas that already backs the rest of the engine's
software rendering. `Broiler.Graphics` depends only on `Broiler.Media.Image` and nothing depends on the
bridge, so `Broiler.HtmlBridge.Dom` can reference it without a cycle.

Nothing in the JS surface gained or lost a *name* as a result of rasterising — `fillRect`, `arc`,
`fill`, `stroke`, `fillText` and the rest were already there. They stopped being empty bodies:

- **Rects and paths.** `fillRect`/`strokeRect` go to `BCanvas.FillRect`/`DrawRectangleStroke`;
  `clearRect` writes transparent pixels straight to the bitmap, because every blending path would
  leave a fully transparent source with no effect. Paths accumulate as flattened subpaths — `arc` is
  flattened so the chord's sagitta stays under a quarter pixel — and `fill`/`stroke` go to
  `FillPolygon`/`DrawPathStroke`.
- **Text** goes through `BImageRenderer`'s `DrawText`, so the glyphs are real outlines from the system
  font rather than boxes. That renderer clears whatever surface it is given, so a run is drawn on a
  scratch bitmap sized to the run — not to the canvas — and composited.
- **`globalAlpha`** folds into the resolved colour's alpha. **`globalCompositeOperation`** wraps a
  single draw in a `BCanvas` blend layer for the modes that rasteriser implements (`multiply`,
  `screen`, `overlay`, `darken`, `lighten`, `difference`, `plus-lighter`).
- **`getImageData`/`putImageData`/`createImageData`** read and write the bitmap directly.
  `BBitmap` holds straight-alpha RGBA, which is exactly what `ImageData` wants, so no conversion is
  needed. Out-of-bounds reads come back transparent black, as the spec requires.
- **`toDataURL`** encodes through `BBitmap.Encode`, which reaches `Broiler.Media.Image.Managed`'s
  encoders — PNG, JPEG, BMP and GIF.

Three things about that are worth keeping straight, because each is the difference between an honest
answer and a plausible-looking wrong one:

**`toDataURL` of an unsupported type falls back to PNG rather than throwing.** HTML requires it, and it
is also what keeps the answer honest: there is no JPEG-XR or WebP encoder, so `canvas.jpegxr` and
`canvas.webp` still read `image/png` out of the returned URL and still report **No**. A test pins that
they do — becoming Yes there would be the false claim of support this whole section is about.

**`getContext('2d')` now returns the same object every call.** It always should have — the spec
requires it — but while the context had no state worth keeping, building a fresh one per call was
merely wasteful. With a bitmap behind it, it decides whether anything a page draws survives: every
`getContext` handed back a blank canvas. The context is keyed off the `DomElement` rather than off its
JS wrapper, so it also survives the wrapper being rebuilt.

**`globalCompositeOperation` is a real accessor now, and rejects what it cannot do.** It used to be
nothing at all: the context object is extensible, so `ctx.globalCompositeOperation = 'screen'` merely
created an own property, and reading it back returned `'screen'` because the assignment had stored the
string — not because anything composited. That round-trip is exactly what a feature detector tests. The
setter now ignores an operator the rasteriser cannot apply, so the value that reads back is one that
was honoured.

Two smaller defects fell out of the same file:

- **`ctx.canvas` returned a fresh empty object**, so `ctx.canvas === theCanvas` was false and
  `ctx.canvas.width` did not answer. It returns the canvas element's JS object.
- **An `#if BROILER_CLI` branch claimed `getContext("2d")` returned `null` in the CLI.** It never did.
  `BROILER_CLI` is defined by `Broiler.Cli.csproj` for *its own* compilation, and `DefineConstants`
  does not reach a referenced project, so the constant was never set while `Broiler.HtmlBridge.Dom` was
  compiled and the branch was unreachable. Removing it changed no behaviour; what it cost was a comment
  that described a host difference that did not exist.

### What is exact and what is approximate

Rect fills, `clearRect`, path fills and strokes, `globalAlpha` and the blend modes are as exact as
`BCanvas` is. Two things are not, and are documented on the type rather than left to be discovered:

- **`measureText`** reports `BImageRenderer`'s own per-character block advance, not a shaped advance
  from the font. The non-`start` `textAlign` values derive from it and are approximate for the same
  reason.
- **There is no transform stack.** The binding exposes no `translate`/`rotate`/`scale`/`setTransform`,
  so none is needed — and `BCanvas` is a translate+uniform-scale rasteriser that could not carry a
  general affine anyway. Adding the transform methods means teaching the rasteriser affines first.

Also still absent, and still honestly absent: `drawImage`, gradients and patterns, `clip`, `ellipse`,
`setLineDash`, `Path2D`, `toBlob`. Their html5test rows (`canvas.path`, `canvas.ellipse`,
`canvas.dashed`, `canvas.focusring`, `canvas.hittest`) still read No.

### The bitmap is script-visible, not yet page-visible

**A `<canvas>` still paints nothing into the page.** Nothing outside the binding reads the context —
`Broiler.Layout` has no canvas display item, and a `<canvas>` lays out as an empty replaced box exactly
as before. So a page that draws a chart and shows it still renders blank in a capture; what changed is
that the same page can now read those pixels back, and that a page which *serialises* its canvas —
`toDataURL` into an `<img>`, or a data URL posted to a server — gets the real image.

That is a smaller gap than it was: the pixels now exist, in a `BBitmap` the display list's own
rasteriser already knows how to draw. Connecting them is a `DrawImageItem` sourced from the context's
bitmap plus an invalidation when a drawing call dirties it, in the shape `SvgImageRaster` already uses
for SVG-as-image. It is deliberately not attempted here — this change is about the exceptions, and
wiring the paint path is a separate piece of work with its own invalidation questions.

## Measured end to end

A real capture of the live site before and after
(`dotnet run --project src/Broiler.Cli -- --capture-image https://html5test.com/ --output <png>
--diagnostic-dir <dir>`), reading the score out of the post-script document the diagnostics archive:

| | score | rows scoring "Yes" | 2D Graphics section |
| --- | --- | --- | --- |
| before | 126 / 555 | 66 | 2 / 25 |
| after | **141 / 555** | **70** | **17 / 25** |

Newly passing: `canvas.context`, `canvas.blending`, `canvas.png`, `canvas.jpeg`. Every other row is
unchanged and **nothing regressed from "Yes" to "No"** — verified by diffing all 276 rows, not by
comparing totals.

`canvas.context` is in that list because it needs `CanvasRenderingContext2D` to exist as a global and
answer `instanceof`; that constructor and `ImageData` join the interface constructors from the previous
round in `DomBridge.RegisterDomInterfaceConstructors`. They are not node types, so they answer from the
members that define the interface rather than from `nodeType`. `ImageData` is additionally
*constructible*, as HTML defines it, and its `@@hasInstance` accepts both one the constructor produced
and one `getImageData` returned — those are different objects, and the readback is the commoner of the
two.

Both runs still report **0 JavaScript failures** in the diagnostics, which is the "caught exceptions are
not logged" point above made concrete: the fix is visible in the score, not in the failure count.

---

# First report: `44`, `228`, `1986`, `3030`

Kept because two of these are why the second report is shorter than the first.

## (1) `TypeError: Cannot get property nodeName of undefined` — `engine.js:44` — **fixed**

```js
e.innerHTML = "<div foo<bar=''>";
result &= e.firstChild.attributes[0].nodeName == "foo<bar" || e.firstChild.attributes[0].name == "foo<bar";
```

The obvious reading — "the tokenizer drops an attribute whose name contains `<`" — is **wrong**.
The tokenizer is correct: the attribute-name state appends `<` as an ordinary character
(`Broiler.DOM/Broiler.Dom.Html/HtmlTokenizer.cs:166-170`), and the DOM really does hold an
attribute named `foo<bar`. Observed before the fix: `attributes.length === 1`,
`getAttributeNames() === ["foo<bar"]`, `attributes.item(0).nodeName === "foo<bar"` — all correct.

The defect was that `attributes[0]` was `undefined`. `BuildNamedNodeMap` registered its index
properties under numeric **string** keys:

```csharp
map.FastAddProperty((KeyString)idx.ToString(), …);   // AttributesBinding.cs:60
```

A `JSObject` keeps named and indexed properties in two separate stores. `FastAddProperty(KeyString…)`
writes the named trie (`JSObject.PropertyStorage.cs:406`), while a JS read of an integer index
resolves through the `elements` table and never falls back to the named one. So the index properties
were written where no read ever looks — `Object.keys(attributes)` did not even list `"0"`, and
`hasOwnProperty("0")` was `false`, which is what gives the mismatch away.

**Fix:** use the `uint` overload, which writes indexed storage — the idiom `FormBinding.cs:56`
already uses. One line, `src/Broiler.HtmlBridge.Dom/Features/AttributesBinding.cs`.

The blast radius inside html5test is wider than the one assertion: the whole tokenizer block is one
`try`, so the throw at line 44 skipped the ~20 remaining assertions on lines 46-95 and zeroed the
entire `parsing.tokenizer` item. Line 50 (`<div "foo=''>`, reading `attributes[0].nodeName`) would
have thrown for the same reason and is fixed by the same change.

This was never really an html5test problem. Any page looping `el.attributes[i]` hit it, and the WPT
runner had already had to route around it in its custom-elements shim — *"the bridge's `attributes`
reports a length but does not answer to numeric indexing, so the obvious loop read `undefined.name`
and threw out of `customElements.define` — taking the whole page's script with it"*
(`src/Broiler.Wpt/WptTestRunner.cs`).

## (2) `ReferenceError: HTMLElement is not defined` — `engine.js:228` — **fixed**

```js
passed = element instanceof HTMLElement && !(element instanceof HTMLUnknownElement) && …
```

Broiler exposed no DOM interface constructors at all beyond `Node`, `Event`, `CustomEvent` and
`MouseEvent`. `HTMLElement`, `Element`, `HTMLUnknownElement`, `Document`, `Text`, `Comment`,
`DocumentFragment`, `CharacterData` and `Attr` were all absent, so the bare identifier threw.

There was a second, compounding gap behind it. A bridge DOM object is a plain `JSObject` whose
prototype is `Object.prototype` (`JsObjects.cs:44`); it carries its members directly rather than
inheriting them from an interface prototype. The ordinary `instanceof` prototype walk therefore
cannot match one — which is why the *existing* `Node` global already answered
`document.createElement('div') instanceof Node === false`. Simply defining the missing names as
functions would have converted a `ReferenceError` into a silently wrong `false`.

**Fix:** `DomBridge.RegisterDomInterfaceConstructors`
(`src/Broiler.HtmlBridge.Dom/DomBridge/Utilities.DomInterfaces.cs`) defines the constructors and
gives each an `@@hasInstance` that answers from the object's own `nodeType` / `namespaceURI` /
`tagName` instead of from a prototype chain. That is the spec's own extension point — ES §13.10.1
consults `@@hasInstance` before the prototype walk — so this is a real answer rather than a shim.
`Node` gains one too, keeping its type constants.

`HTMLUnknownElement` is modelled as the subtype it is: an unknown element is an instance of both it
and `HTMLElement`, which is exactly what html5test's `instanceof HTMLElement && !(instanceof
HTMLUnknownElement)` split relies on. A hyphenated name is an undefined custom element, which the
spec makes an `HTMLElement` rather than an unknown one.

Note it must be installed with `Object.defineProperty`: `Function.prototype[@@hasInstance]` is
non-writable, so a plain assignment silently does nothing in sloppy mode.

Giving DOM objects genuine per-interface prototype chains would subsume all of this and is the
better long-term shape. It is also a far larger change to the object model than making
`instanceof HTMLElement` — the single most common way a page asks "is this an element?" — stop
throwing.

**Scope, and what is still missing.** This covers the node interfaces, and the second round adds
`CanvasRenderingContext2D` and `ImageData`. html5test uses `instanceof` against roughly two dozen more
that remain undefined — `Blob` (`communication.xmlhttprequest2.response.blob`), `FileList`
(`form.file.files`), `NodeList`, `HTMLCollection` and the per-tag `HTML*Element` types. Those are not
node types, so they need a different discriminator than `nodeType`, and the per-tag types need a
tag→interface table. Note also that `childNodes` returns a `JSArray`, so a `NodeList` global would need
to decide what it is claiming about that before it would mean anything.

## Measured end to end (first round)

| | score | rows scoring "Yes" |
| --- | --- | --- |
| before | 123 / 555 | 61 |
| after | 126 / 555 | 64 |

Newly passing: `elements.interactive`, `elements.interactive.menutoolbar`,
`elements.semantic.ruby` — all of them `instanceof HTMLElement && !(instanceof HTMLUnknownElement)`
probes.

Two probes named above still score "No" for reasons beyond the exception: `parsing.tokenizer` runs
~20 further assertions after the one that used to throw, and `elements.section` additionally requires
`isBlock(element)` and `closesImplicitly(tag)`. Removing the throw lets those probes *finish*; it does
not by itself make them pass.

---

## What changed

Second round:

| file | change |
| --- | --- |
| `src/Broiler.HtmlBridge.Dom/DomBridge/CanvasRenderingContext2D.cs` | rewritten — the `BBitmap` backing store and the rasterising drawing operations |
| `src/Broiler.HtmlBridge.Dom/Features/CanvasBinding.cs` | pixel APIs, `toDataURL`, one context per canvas, real `ctx.canvas`, `globalCompositeOperation`/`textAlign` accessors |
| `src/Broiler.HtmlBridge.Dom/Features/ICanvasHost.cs` | new — the host contract for reaching the realm's `Uint8ClampedArray` |
| `src/Broiler.HtmlBridge.Dom/DomBridge/DomBridge.CanvasHost.cs` | new — its explicit implementation |
| `src/Broiler.HtmlBridge.Dom/DomBridge/Utilities.DomInterfaces.cs` | `CanvasRenderingContext2D` and `ImageData` globals |
| `src/Broiler.HtmlBridge.Dom/DomBridge/JsObjects.cs` | pass the bridge to `CanvasBinding.Install` |
| `src/Broiler.HtmlBridge.Dom/Broiler.HtmlBridge.Dom.csproj` | reference `Broiler.Graphics` and `Broiler.Media.Image` |
| `src/Broiler.Cli.Tests/CanvasPixelSurfaceTests.cs` | new — the pixel surface, and the html5test probes verbatim |
| `patches/0007-not-a-constructor-message.patch` | `Broiler.JS` — `new undefined()` says "is not a constructor" |

First round:

| file | change |
| --- | --- |
| `src/Broiler.HtmlBridge.Dom/Features/AttributesBinding.cs` | index properties into indexed storage |
| `src/Broiler.HtmlBridge.Dom/DomBridge/Utilities.DomInterfaces.cs` | new — the interface-constructor globals |
| `src/Broiler.HtmlBridge.Dom/DomBridge/Registration/Polyfills.cs` | register them |
| `src/Broiler.Wpt/WptTestRunner.cs` | shim guard now probes capability, not name existence |
| `src/Broiler.Cli.Tests/HtmlDomInterfacesTests.cs` | numeric-index and `foo<bar` regression tests |
| `src/Broiler.Cli.Tests/DomInterfaceConstructorTests.cs` | new — interface-constructor tests |

The `WptTestRunner.cs` change was a consequence of the `HTMLElement` fix, not an improvement in its own
right. Its custom-element shim installed a constructible `HTMLElement` only when the name was missing;
once the bridge defined the name, that guard would have skipped the shim and left
`class X extends HTMLElement` building plain objects instead of elements. It now probes the capability
it actually needs — that `new HTMLElement()` yields an element node — so the override still installs,
and a future real implementation would pass the probe and win.
