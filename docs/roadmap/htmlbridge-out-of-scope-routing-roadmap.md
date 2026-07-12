# HtmlBridge Out-of-Scope Routing Roadmap

Status: **reference / routing map** — the disposition of every `Broiler.HtmlBridge.*` surface the DOM/CSS
promotion program explicitly declared **out of scope** for promotion into `Broiler.DOM` / `Broiler.CSS`.
Nothing here is DOM/CSS-promotion work; this doc records *where each concern belongs instead* (another
component's roadmap, or "permanently bridge-owned") so the boundary is not re-litigated and no item is lost
between roadmaps.
Date: 2026-07-12

## Why this doc exists

The promotion program ([`htmlbridge-dom-css-promotion-roadmap.md`](htmlbridge-dom-css-promotion-roadmap.md))
has two exclusion lists — the **Deferred** row of its promotion-candidate table and its **Non-candidates**
list. Those items are correctly *not* in the promotion backlog
([`htmlbridge-promotion-backlog-roadmap.md`](htmlbridge-promotion-backlog-roadmap.md)), but "don't promote to
DOM/CSS" is a decision, not a destination. This roadmap gives each excluded surface a destination and a
status, in three buckets:

1. **Route to the media/graphics roadmaps** — shared engine capabilities that belong to `Broiler.Media` /
   `Broiler.Graphics`, not DOM/CSS.
2. **Route to `Broiler.Layout`** — layout/paint/geometry that belongs to the layout engine; the bridge keeps
   only the JS-facing wrappers.
3. **Permanent bridge residents** — JS object identity, runtime state, and host/resource integration that are
   *definitionally* bridge responsibilities and never move.

The routing rule is the inverse of the promotion rule: a surface is out of scope for DOM/CSS precisely when it
needs JavaScript object identity, callbacks, **layout geometry, paint, resource loading, or media/graphics
capability** — each of which has its own owning component.

---

## Bucket 1 — Media & graphics capabilities → `Broiler.Media` / `Broiler.Graphics`

**Promotion-roadmap origin:** the **Deferred** candidate row — *image decoder, SVG parser/renderer, canvas
helpers → do not move to DOM/CSS; align with the media/graphics roadmap.*

**Bridge surface today.** `Broiler.HtmlBridge.Rendering/ImagePipeline.cs` (the `<img>`/image decode + view-box
pipeline); SVG and canvas rasterization paths reached through the bridge's rendering/graphics adapters.

**Destination & owning roadmaps.**

| Concern | Destination | Owning roadmap | Status |
| --- | --- | --- | --- |
| Image decode (PNG/APNG/JPEG/BMP) | `Broiler.Media.Image.Managed` | [`broiler-media-component.md`](broiler-media-component.md) | **Proposed** — the plan explicitly moves the existing managed image codecs out of `Broiler.Graphics` into `Broiler.Media.Image.Managed`. |
| Audio / video decode | `Broiler.Media.Audio` / `Broiler.Media.Video.MediaFoundation` | [`broiler-media-component.md`](broiler-media-component.md) | **Proposed** (decode-first; `IMFMediaEngine` is the fixed first video provider). |
| SVG parse / render, canvas rasterization | `Broiler.Graphics` (+ `Broiler.Graphics.Windows` surface) | media/graphics + [`skia-replacement-roadmap.md`](skia-replacement-roadmap.md) | Graphics-owned; not DOM/CSS. |

**HtmlBridge's stake.** The bridge keeps the **DOM/JS element behavior** (`HTMLImageElement`,
`HTMLCanvasElement`, `<svg>` element wiring, `viewBox` IDL, network/loading policy) and hands pixels/decoding
to Media/Graphics through their host seams. When `Broiler.Media` lands, `ImagePipeline` should consume the
`Broiler.Media.Image` codec contract instead of `Broiler.Graphics` image types — tracked **there**, gated on
the media roadmap leaving "Proposed."

**Action for this program:** none. Do not promote to DOM/CSS. Revisit `ImagePipeline`'s codec dependency when
`broiler-media-component.md` starts implementation.

---

## Bucket 2 — Layout, paint & geometry → `Broiler.Layout`

**Promotion-roadmap origin:** the **Non-candidates** entry — *layout metrics, hit-testing, scroll geometry,
paint staging, and rendering logs should follow `Broiler.Layout` or rendering roadmaps, not DOM/CSS
extraction* — plus the RF-BRIDGE-1a dead-rendering types.

**Bridge surface today.** `DomBridge/LayoutMetrics.cs`, `HitTesting.cs`, `AnchorResolver/ScrollSimulation.cs`,
`SharedLayoutGeometry.cs`, `CheckLayoutAssertions.cs`, and `Rendering/SharedLayoutGeometryProvider.cs`.

**Disposition.**

- **Geometry read-model — already unified (RF-BRIDGE-1b, done).** The bridge no longer owns a parallel box
  model: the ~2950-LOC recursive `LayoutMetrics` estimators and `LayoutRuntimeState` are **deleted**, and the
  bridge now reads real geometry from `Broiler.Layout` through `SharedLayoutGeometryProvider`. See
  [`htmlbridge-blocked-items-completion-roadmap.md`](htmlbridge-blocked-items-completion-roadmap.md) Track 2
  and [`rf-bridge-1b-layout-unification.md`](rf-bridge-1b-layout-unification.md). The layout engine itself is
  [`broiler-layout-component.md`](broiler-layout-component.md) (**Complete**).
- **RF-BRIDGE-1a dead paint types — deleted.** `CssBoxModel`, `CssTextProperties`, `RenderingStages` (~29
  types) were removed at the Phase 5 public-surface boundary (promotion roadmap Phase 5). Closed.
- **What stays bridge-owned (correctly).** The remaining files are the **JS-facing geometry wrappers**
  (`getBoundingClientRect`/`client*`/`offset*`/`scroll*`, `elementFromPoint` hit-testing, `scrollIntoView` /
  scroll simulation) and the provider seam. These marshal canonical layout geometry into JS/CSSOM object
  behavior — a bridge responsibility by definition. They are **not** candidates for DOM/CSS or for further
  extraction into `Broiler.Layout` (the engine supplies geometry; it does not own the DOM/JS API surface).

**Action for this program:** none. The only future layout-side movement is whatever the `Broiler.Layout`
roadmap itself schedules; the bridge↔layout seam (`SharedLayoutGeometryProvider`) is the established boundary.

---

## Bucket 3 — Permanent bridge residents (never promote)

**Promotion-roadmap origin:** the **Non-candidates** list. These are definitionally bridge responsibilities —
they carry JavaScript object identity, host integration, or runtime state, so no canonical DOM/CSS home is
even meaningful.

| Surface | Bridge location (representative) | Why it stays |
| --- | --- | --- |
| JS object wrappers — DOM, CSSOM, events, ranges, iterators, style declarations, collections | `DomBridge/JsFunctionCallbacks/*`, `JsObjects.cs`, `StyleSheets.cs` | Live object identity + JS callback marshaling; DOM/CSS own the *algorithms*, the bridge owns the *wrappers* over them. |
| `ElementRuntimeState` (as a whole) | `ElementRuntimeState.cs` | Aggregates JS identity, listeners, form/scroll/dialog/shadow state, stylesheet runtime state, animations — bridge runtime, not a DOM node concern. |
| Resource loading — `fetch`, `XMLHttpRequest`, external stylesheet fetching | `DomBridge/Registration/Fetch.cs`, `XmlHttpRequest.cs` | Networking + host policy; the CSS scope builder (§2.4) deliberately takes host-supplied text so fetching stays here. |
| Timers, host callbacks, browser-app integration | bridge registration/host surfaces | Host-runtime integration, no canonical component. |

**Action for this program:** none, ever. Recorded here only so a future "what else can we promote?" pass does
not re-open them. If a *neutral algorithm* is ever found hiding inside one of these wrappers (as happened with
`classList` → `DomTokenList` and mutation-observer filtering in Phase 4), that algorithm is a promotion
candidate for the backlog doc — the *wrapper* around it still stays here.

---

## Summary

| Bucket | Items | Destination | Actionable here? |
| --- | --- | --- | --- |
| 1 | image/audio/video decode, SVG, canvas | `Broiler.Media` / `Broiler.Graphics` | No — tracked in the media/graphics roadmaps (Media is *Proposed*). |
| 2 | layout metrics, hit-testing, scroll/paint geometry | `Broiler.Layout` (via the provider seam) | No — geometry read-model already unified (RF-BRIDGE-1b done); JS wrappers stay bridge-owned. |
| 3 | JS wrappers, `ElementRuntimeState`, resource loading, host integration | none — permanent bridge residents | No — never promote (extract only a neutral algorithm if one surfaces). |

**Net:** there is **no open DOM/CSS-promotion work in any of these buckets.** Bucket 1's only future move is
gated on the `Broiler.Media` roadmap starting implementation; Buckets 2 and 3 are settled boundaries. The only
open *promotion* item across the whole HtmlBridge program remains **P3 HTML-serialization policy**, tracked in
[`htmlbridge-promotion-backlog-roadmap.md`](htmlbridge-promotion-backlog-roadmap.md).
