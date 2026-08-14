# html5test.com — the four rendering exceptions

Rendering <https://html5test.com/> reported four distinct JavaScript exceptions. This is what
each one actually is, what was fixed, and what was deliberately left alone.

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

## None of the four aborted the page

Every one of the four sites sits inside html5test's own `try { … } catch (e) { }`. That is not
incidental — `Runner.initialize` wraps the whole testsuite loop in a single
`try { … } catch (e) { error(e) }` (`engine.js:4423-4519`), so had the first exception escaped, the
run would have stopped there and the other three could never have been reported. Four reports is
itself the proof that all four were caught.

They are feature-detection probes: html5test deliberately provokes a throw and reads the failure as
"unsupported".

**Where the report came from matters.** Broiler's own capture diagnostics would not have listed
these. The only place a JS error is ever logged is the host `catch` around
`context.Eval(script, label)` — `src/Broiler.Cli/CaptureService.cs:770-776` and `:789-792`, and the
equivalents in `src/Broiler.HtmlBridge.Scripting/ScriptEngine.cs` — that is, the *unwind* boundary,
reached only when an exception escapes an entire script. There is no throw-time hook anywhere:
`Broiler.JS` never reports, it only *constructs* a `JSException` carrying an origin frame. A caught
exception therefore produces no `javascript-errors.log` entry at all.

The four traces here interleave C# frames (`JSUndefined.cs:41`) with JS frames and name Windows
paths (`D:\Broiler\…`), which is what a **first-chance** observer sees: every `JSException` at the
moment it is constructed, caught or not. That is a strictly noisier view than the capture
diagnostics, and reading it as a list of page failures overstates what happened — none of these
stopped html5test, and one of them (WebRTC) is the correct answer.

That does not make them uninteresting — three of the four are real capability gaps, and two were
outright defects — but it does mean none of them was an emergency.

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
(`src/Broiler.Wpt/WptTestRunner.cs`). That workaround can now be retired at leisure.

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

**Scope, and what is still missing.** This covers the node interfaces. html5test uses `instanceof`
against roughly two dozen more that remain undefined — `CanvasRenderingContext2D`
(`canvas.context`), `Blob` (`communication.xmlhttprequest2.response.blob`), `FileList`
(`form.file.files`), `NodeList`, `HTMLCollection` and the per-tag `HTML*Element` types. Those are
not node types, so they need a different discriminator than `nodeType`, and the per-tag types need a
tag→interface table; both are follow-on work rather than part of this fix. Note also that
`childNodes` returns a `JSArray`, so a `NodeList` global would need to decide what it is claiming
about that before it would mean anything.

## (3) `TypeError: cannot create instance of undefined` — `engine.js:1986` — **not a defect**

```js
o = new (window.RTCPeerConnection || window.msRTCPeerConnection || window.mozRTCPeerConnection || window.webkitRTCPeerConnection)(null);
```

All four are undefined, so this is `new undefined(…)`. Broiler implements no WebRTC surface
anywhere — no `RTCPeerConnection`, no `RTCDataChannel`, no `navigator.mediaDevices`,
no `getUserMedia`, in the main repo or any submodule. `webrtc/*` is listed in
`tests/wpt-baseline/failed-tests.json` as a durable expected failure, and WebRTC does not appear in
`docs/ROADMAP.md`.

This is the expected outcome of a feature probe against an engine without the feature — a browser
with WebRTC disabled throws here too. Nothing to fix; implementing WebRTC is a feature project
(an ICE/DTLS/SCTP stack), not a bug fix.

One cosmetic observation, independent of html5test: Broiler words this `cannot create instance of
undefined`, where the spec-conventional phrasing — and Broiler's own wording at its other
construct sites — is `… is not a constructor`. Worth a sweep if error-message consistency is ever
tidied; it lives in the `Broiler.JS` submodule (`JSUndefined.cs`, `JSNull.cs`), so it would go
through the patch workflow.

## (4) `TypeError: undefined is not a function` — `engine.js:3030` — **left open, deliberately**

```js
var data = ctx.getImageData(0, 0, 1, 1);
```

`getImageData` is absent, and so are `putImageData`, `createImageData`, `ImageData` and
`canvas.toDataURL`. The absence is structural, not a missing entry in a member list: the 2D context
is a **style-state stub with no pixels**. `CanvasRenderingContext2D`
(`src/Broiler.HtmlBridge.Dom/DomBridge/CanvasRenderingContext2D.cs`) keeps `fillStyle`, `lineWidth`,
`font` and the save/restore stack, and every drawing method is a literal empty body —
`public void FillRect(float x, float y, float width, float height) { }`. The type's own remarks
record that Phase 6 removed the former command list because nothing ever rendered it. So there is no
backing store for `getImageData` to read.

**This was deliberately not stubbed.** Returning zeroed pixels would convert an honest
`TypeError` — which every feature detector on the web reads correctly as "no canvas readback" —
into a false claim of support, and pages that branch on `typeof ctx.getImageData === 'function'`
would then take the canvas path and silently render nothing. Absent is the safer answer until the
context can actually rasterise.

The real fix is to give the context a real backing store. `Broiler.Graphics` already ships the
rasteriser and a readable RGBA buffer that would back it, and it is a dependency leaf, so the
binding could consume it without a cycle. That is a scoped, worthwhile project — it would also make
`canvas.toDataURL` and canvas painting possible — but it is a feature, not a fix, and it is not
attempted here.

## Measured end to end

A real capture of the live site before and after
(`dotnet run --project src/Broiler.Cli -- --capture-image https://html5test.com/ --output <png>
--diagnostic-dir <dir>`), reading the score out of the post-script document the diagnostics archive:

| | score | rows scoring "Yes" |
| --- | --- | --- |
| before | 123 / 555 | 61 |
| after | 126 / 555 | 64 |

Newly passing: `elements.interactive`, `elements.interactive.menutoolbar`,
`elements.semantic.ruby` — all of them `instanceof HTMLElement && !(instanceof HTMLUnknownElement)`
probes. Nothing regressed from "Yes" to "No".

Both runs report **0 JavaScript failures** in the diagnostics, before and after, which is the
"caught exceptions are not logged" point above made concrete: the fixes are visible in the score,
not in the failure count.

Two probes named in this document still score "No" for reasons beyond the exception:
`parsing.tokenizer` runs ~20 further assertions after the one that used to throw, and
`elements.section` additionally requires `isBlock(element)` and `closesImplicitly(tag)`. Removing
the throw lets those probes *finish*; it does not by itself make them pass.

## What changed

| file | change |
| --- | --- |
| `src/Broiler.HtmlBridge.Dom/Features/AttributesBinding.cs` | index properties into indexed storage |
| `src/Broiler.HtmlBridge.Dom/DomBridge/Utilities.DomInterfaces.cs` | new — the interface-constructor globals |
| `src/Broiler.HtmlBridge.Dom/DomBridge/Registration/Polyfills.cs` | register them |
| `src/Broiler.Wpt/WptTestRunner.cs` | shim guard now probes capability, not name existence |
| `src/Broiler.Cli.Tests/HtmlDomInterfacesTests.cs` | numeric-index and `foo<bar` regression tests |
| `src/Broiler.Cli.Tests/DomInterfaceConstructorTests.cs` | new — interface-constructor tests |

The `WptTestRunner.cs` change is a consequence of (2), not an improvement in its own right. Its
custom-element shim installed a constructible `HTMLElement` only when the name was missing; now that
the bridge defines the name, that guard would have skipped the shim and left
`class X extends HTMLElement` building plain objects instead of elements. It now probes the
capability it actually needs — that `new HTMLElement()` yields an element node — so the override
still installs, and a future real implementation would pass the probe and win.
