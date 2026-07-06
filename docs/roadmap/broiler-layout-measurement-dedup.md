# Broiler.Layout — Measurement & Constant De-duplication Roadmap

**Status:** COMPLETE (2026-07-06) — Phases M0–M6 landed. R1, R2, R4, R5, R6, R7
fully resolved; R3 resolved as far as is behavior-safe (M4 pure-redundancy subset +
M4b lazy-parse win; the remaining table double-parse is proven irreducible without a
behavior change, so intentionally left).
**Goal:** Remove the residual redundancy in *constants*, *unit conversion*, and
*length parsing* that the layout/CSS extractions left behind as "temporary
duplication," and give the engine a single source of truth for every physical
measurement factor. This is an **optimization / cleanup** roadmap — no
layout-algorithm behavior should change.

**Why now.** The `Broiler.Layout` extraction
([`broiler-layout-component.md`](broiler-layout-component.md)) and the
`Broiler.CSS` extraction ([`broiler-css-next-steps.md`](broiler-css-next-steps.md))
both explicitly deferred a "Phase 7 CSS cleanup dedups" step for the measurement
primitives. The kernel-facing keyword table (`CssConstants`) and the syntax-only
parsers were unified, but the **numeric** side — DPI factors, unit→pixel tables,
default font size, and the two coexisting length abstractions — was left
duplicated and is now the largest remaining redundancy the engine carries. Every
comment of the form *"until the Phase 7 CSS cleanup dedups"* in `CssConstants.cs`,
`CssLength.cs`, and `CssLengthParser.cs` points at this work.

> **Submodule note.** `CssLength`, `CssLengthParser`, `CssConstants`, and
> `CssStyleEngine.*` live in the **`Broiler.CSS` submodule**; `Broiler.Layout` is
> parent-repo. Per [`CLAUDE.md`](../../CLAUDE.md), CSS-side edits follow the
> push-or-patch workflow (push to `MaiRat/Broiler.CSS`, bump the pointer;
> `Broiler.CSS` push currently redirects in-scope). Each phase below flags which
> repo it touches.

---

## 1. The redundancy inventory (current state, with evidence)

### R1 — Duplicated unit→pixel conversion tables (CSS)
`CssLengthParser` resolves a unit to a pixel factor in **two** near-identical
switches that must stay in lockstep by hand:

- `ParseLength` — `switch (unit)`, [`CssLengthParser.cs:185-258`](../../Broiler.CSS/Broiler.CSS/CssLengthParser.cs)
- `TryParseSimpleLength` — `factor = unit switch { … }`, [`CssLengthParser.cs:469-490`](../../Broiler.CSS/Broiler.CSS/CssLengthParser.cs)

Both hard-code the same magic factors: `mm = 3.779527559`, `cm = 37.795275591`,
`in = 96`, `pt = 96/72`, `pc = 16`, `Q = 37.795275591/40`, `ex = em/2`,
`ch = em/2`. A change to any factor (e.g. hi-DPI support, the `//TODO:a check
support for hi dpi` at line 210) must be made in two places or the two code paths
diverge silently.

### R2 — A third, independent unit parser (CSS)
`CssLength` (the *class*) re-implements unit detection in its constructor with yet
another hand-rolled substring switch —
[`CssLength.cs:47-141`](../../Broiler.CSS/Broiler.CSS/CssLength.cs) — duplicating
`CssLengthParser.GetUnit`
([`CssLengthParser.cs:636-706`](../../Broiler.CSS/Broiler.CSS/CssLengthParser.cs)).
Two independent parsers for the same grammar, with slightly different unit
coverage (the class knows nothing about `lh`/`rlh`/`Q`/`calc()`).

### R3 — Layout double-parses the same length string (Layout)
Because `CssLength` (units only) and `CssLengthParser` (pixels) are separate,
the sizing paths build **both** from one string. Example — image sizing:
`new CssLength(OwnerBox.Width)` for `.Number`/`.IsPercentage`
([`CssLayoutEngine.cs:85`](../../Broiler.Layout/Broiler.Layout/Engine/CssLayoutEngine.cs))
**and** `TryResolveDefiniteImageLength(OwnerBox.Width, …)` which re-parses the
same string through `CssLengthParser`
([`CssLayoutEngine.cs:95`](../../Broiler.Layout/Broiler.Layout/Engine/CssLayoutEngine.cs)).
The same `new CssLength(...)`/`ParseLength(...)` pairing recurs in
`CssBoxImage.cs`, `CssLayoutEngineTable.cs` (1253/1257), and the min/max-width
block of `CssLayoutEngine.cs` (122-232). Redundant code *and* redundant work per
layout pass.

### R4 — The 96-DPI assumption is encoded as scattered literals (CSS + Layout + CSS.Dom)
The single fact "96 CSS px per inch" appears in mutually-inconsistent literal
forms across three components with no shared constant:

| Form | Location |
|---|---|
| `96.0 / 72.0` (pt→px), inline | `CssBoxProperties.cs:1548,1623`, `CssLengthParser.cs:52,158,192,472,481` |
| `PtToCssPx = 96.0 / 72.0`, **named const** | [`CssLayoutEngine.cs:41`](../../Broiler.Layout/Broiler.Layout/Engine/CssLayoutEngine.cs) |
| `96f` (in), `3.779527559f` (mm), `37.795275591f` (cm) | `CssLengthParser.cs:210-237,477-488` |
| `DeviceDpi = 96.0` | [`CssStyleEngine.Values.cs:1265`](../../Broiler.CSS/Broiler.CSS.Dom/CssStyleEngine.Values.cs) |

`CssLayoutEngine.PtToCssPx` *already* names the ratio but nothing else reuses it —
the named constant and the raw literals coexist.

### R5 — The default line-height factor `1.2` is un-named and repeated (CSS + Layout)
`* 1.2` (the CSS `normal` line-height fallback) is inline magic in
`CssBoxProperties.cs:1616,1633-1634` and `CssLengthParser.cs:52-54,156,158`. No
`NormalLineHeightFactor` constant.

### R6 — Default font size is duplicated *and* inconsistent (multiple components)
The "initial font size" constant has three values in three places:

- `CssConstants.FontSize = 12f` — [`CssConstants.cs:142`](../../Broiler.CSS/Broiler.CSS/CssConstants.cs) (used as the root-em base in Layout)
- `DefaultFontSize = 12f` — `Broiler.HTML/…/PaintWalker.cs:20`
- `DefaultFontSize = 16f` — `src/Broiler.HtmlBridge.Rendering/CssBoxModel.cs:183`

The `12` vs `16` split is a latent correctness smell, not just duplication.

### R7 — `HtmlConstants` / `CommonUtils` / `HtmlUtils` subsets duplicated (Layout + HTML)
`Broiler.Layout/LayoutHtmlSupport.cs` holds layout-only copies of
`HtmlConstants` (and `CommonUtils.IsAsianCharecter`/`Max`, `HtmlUtils.DecodeHtml`)
that also exist in `Broiler.HTML.Core/Utils/HtmlConstants.cs`. The layout
component cannot reference the renderer, so this was accepted as "inherent" in the
extraction — but it is the same string tables in two places. (See §4 for the
principled fix: promote the shared subset into `Broiler.CSS` or a small kernel,
the same move that resolved `CssConstants`.)

---

## 2. Design target

One authority per measurement concept:

```
Broiler.CSS/CssMetrics.cs   (new, public static)
 ├─ Dpi = 96.0                         // the one DPI assumption
 ├─ PtToPx = Dpi / 72.0                // pt → px
 ├─ PxPerInch / PxPerCm / PxPerMm / PxPerQ / PxPerPc   // derived, not literal
 ├─ NormalLineHeightFactor = 1.2
 └─ DefaultFontSizePt = 12             // the single initial font size
```

- **R1/R2:** collapse the three unit parsers into one. `CssLengthParser.GetUnit`
  becomes the sole tokenizer; `CssLength` (the class) either delegates to it or is
  retired in favor of a `readonly record struct` returned by the parser
  (`{ double Number, CssUnit Unit, bool IsPercentage, bool HasError }`).
- **R1/R4/R5:** both `CssLengthParser` switches read factors from `CssMetrics`;
  the two switches fold into one shared `UnitToPixelFactor(unit, ctx)` helper.
- **R3:** layout sizing paths call the parser **once**, consuming the struct for
  both the percentage test and the pixel value — no paired `new CssLength(...)`.
- **R4:** `CssLayoutEngine.PtToCssPx`, `CssStyleEngine.DeviceDpi`, and the
  `CssBoxProperties` inline `96.0/72.0` all reference `CssMetrics`.
- **R6/R7:** one `DefaultFontSizePt`; reconcile the 12/16 discrepancy explicitly
  (decide the correct default, land it once). Promote the shared `HtmlConstants`
  subset the same way `CssConstants` was promoted.

---

## 3. Phased plan (each phase behavior-neutral & independently shippable)

Ordered cheapest-first so early wins land without touching the algorithm. Every
phase is verified against the same gate (see §4).

- **Phase M0 — Introduce `CssMetrics` (CSS submodule). ✅ DONE 2026-07-06.** Added
  [`Broiler.CSS/Broiler.CSS/CssMetrics.cs`](../../Broiler.CSS/Broiler.CSS/CssMetrics.cs)
  (`public static`): `Dpi=96` + all absolute-unit factors **derived** from it
  (`PtToPx`, `PxToPt`, `PxPerInch`, `PxPerCm=Dpi/2.54`, `PxPerMm=Dpi/25.4`,
  `PxPerQ`, `PxPerPica`), plus `NormalLineHeightFactor`, `DefaultFontSizePt`,
  `DefaultFontSizePx`. *No callers yet* — pure addition. **Precision note:** the
  historical `cm`/`mm`/`Q` factors were truncated `float` literals
  (`37.795275591f`, `3.779527559f`); the derived `double` values are exact and
  differ by ~1e-10 relative — a precision *improvement* far below sub-pixel, not a
  regression. `pt→px` (`96/72`), `in` (`96`), and `px→pt` (`0.75`) are
  bit-identical to the old literals. Guard test
  [`CssMetricsTests.cs`](../../Broiler.CSS/Broiler.CSS.Tests/CssMetricsTests.cs)
  (4 facts) pins each factor to both its definition and the historical literal it
  replaces in M1. `Broiler.CSS` builds clean; full `Broiler.CSS.Tests` 27/27 green.

- **Phase M1 — Route `CssLengthParser` through `CssMetrics` (CSS). ✅ DONE 2026-07-06.**
  Replaced every inline literal in both conversion switches (`ParseLength` and
  `TryParseSimpleLength`) plus the two line-height fallbacks with `CssMetrics`
  references (R1/R4/R5): `mm/cm/in/pt/pc/Q` factors, the `rem`/default-em base
  (`DefaultFontSizePx`), the `px`-fontAdjust factor (`PxToPt`), and the `1.2`
  line-height factor (`NormalLineHeightFactor`). A grep of `CssLengthParser.cs`
  for the old literals now returns nothing. Still two switches (M2 folds them).
  **Behavioral delta:** `in` and `px`-fontAdjust are bit-identical; `cm/mm/Q/pt`
  shift by the old *float*-rounding error (~1e-7 relative) since the historical
  literals were truncated `float`s and `CssMetrics` uses exact `double`s. Proven
  pixel-safe by `ParseLength_Matches_Historical_Float_Factors_Within_SubPixel`
  (6 units × 8 magnitudes 0.1–1000): the **rounded device pixel is identical**
  everywhere; the raw delta stays ≤1e-6 relative. `Broiler.CSS` builds clean;
  `Broiler.CSS.Tests` 33/33 green.
  *Gate note:* `Broiler.Layout.Tests` is broken at baseline (a pre-existing
  `CssBox` internal-visibility compile error in `AbsposSelfAlignmentTests`,
  unrelated to this change), so the layout-level parity relies on the numeric
  proof above; the full WPT pixel gate was not re-run (M1 is a non-pixel-moving
  phase per §4).

- **Phase M2 — Collapse the two `CssLengthParser` switches (CSS). ✅ DONE 2026-07-06.**
  Extracted one private `UnitToPixelFactor(unit, emFactor, fontAdjust,
  lineHeightFactor, rootLineHeightFactor)` returning the pixel factor (or
  `double.NaN` for an unrecognized unit). Both `ParseLength` (statement-switch)
  and `TryParseSimpleLength` (expression-switch) now call it, so the unit→factor
  table exists in exactly one place — R1's hand-lockstep hazard is gone. Each call
  site keeps its own divergent tail: `ParseLength` maps `NaN`→`0` (legacy default)
  and short-circuits `pt`+`returnPoints`; the math evaluator maps `NaN`→parse
  failure. Behavior-neutral by construction (every case value preserved); the M1
  parity theory exercises this path (`1cm`/`1mm`/… resolve via
  `TryParseSimpleLength`→the helper). `Broiler.CSS.Tests` 33/33 green; net −22 lines.
  *Not yet folded:* the unit-*recognition* switches in `IsValidLength`/`GetUnit`
  are R2 (separate parser), addressed in M3.

- **Phase M3 — Unify unit detection; retire the `CssLength` parser (CSS). ✅ DONE 2026-07-06.**
  Made `CssLengthParser.GetUnit` `internal` and had `CssLength`'s constructor
  delegate its ~90-line inline substring/unit-matching switch to it (R2), then
  project the token onto `CssUnit` via a small `TryMapUnit`. The scanning
  algorithm now lives in exactly one place; `CssLength` keeps only the trivial
  string→enum projection. **Behavior preserved exactly**, including `CssLength`'s
  deliberately *narrower* unit set — `lh`/`rlh`/`Q` (which `GetUnit` recognizes but
  legacy `CssLength` did not) map to `HasError`, and the case-sensitivity quirks
  (`5EM`→error, `50VMIN`→ok) are unchanged. Public shape of `CssLength` untouched
  (M4 repoints layout callers). New `CssLengthTests` (30 cases) pins the contract:
  supported units + `CssUnit`/`IsRelative`/`Number`, the `%`-fraction, empty/zero,
  the `lh`/`rlh`/`Q`/case/malformed error set, and the `ConvertEmToPoints`
  font-size path. `Broiler.CSS.Tests` 63/63; `Broiler.Layout.Tests` 13/13 (builds
  against the rewritten `CssLength`). Net −70 lines in `CssLength`.

- **Phase M4 — Kill the layout double-parse (Layout). ✅ DONE (pure-redundancy
  subset) 2026-07-06; M4b deferred.** Auditing the sites showed the "double
  parses" split into two kinds, and only one is *pure* redundancy:
  - **Pure redundancy (fixed):** (1) a dead `var height = new CssLength(…)` in
    `CssLayoutEngine.MeasureImageSize` (never read — confirmed by grep) removed;
    (2) `CssLayoutEngineTable` column-percentage width computed `ParseNumber(w,
    avail)` after already building `new CssLength(w)` — replaced with
    `len.Number * avail` (provably equal: `len.Number` is the fraction parsed
    against a 100%-basis of 1). Both zero-risk; `Broiler.Layout.Tests` 13/13.
  - **Two-different-computations (→ M4b):** the remaining pairings —
    `MeasureImageSize` width (`%`-branch via `CssLength` vs definite-px via
    `TryResolveDefiniteImageLength`, which resolves `em`/`vw`/`calc`) and the table
    `GetTableWidth`/`GetMaxTableWidth` (an avail-*independent* "is a width
    specified" guard via `CssLength.Number>0` vs the avail-*resolved* px via
    `ParseLength`) — parse the same string twice but extract **different** facts.

- **Phase M4b — the remaining double-parses. ✅ ANALYZED + one win 2026-07-06;
  struct API declined.** The proposed unified
  `CssLengthParser.TryResolve → { IsPercentage, RawNumber, Unit, ResolvedPx }`
  struct turns out to be **cosmetic, not a parse reduction**, and is *not*
  behavior-safe to build as a true single pass, because `CssLength`'s quirks are
  **load-bearing**: `CssLength` has no `calc()` handling, so `CssLength("calc(…)")
  .Number == 0` while `ParseLength("calc(…)") > 0`. The table guard
  (`CssLength.Number > 0`) therefore *deliberately* ignores `calc` widths and
  **cannot be derived from the resolved px** — a single-pass merge would silently
  start honoring `calc` table widths. So the two facts genuinely require two
  computations; a wrapper struct would still parse twice (no perf gain) while
  adding kernel API surface for two call sites. **Declined** as over-engineering.
  - **The one genuine, safe reduction (done):** in `MeasureImageSize` the `width`
    `CssLength` is only read on the non-definite path, yet was parsed
    unconditionally. Deferred it into the `else` branch (behavior-identical
    `if/else-if` → nested `if/else`), so a definite-tag-width image skips that parse
    entirely. `Broiler.Layout.Tests` 13/13.
  - **Verdict:** the table double-parses are irreducible without a behavior change
    and are left as-is (two cheap, correct computations). No WPT pixel run was
    needed — nothing that could move a pixel was changed.

- **Phase M5 — Single default font size (R6). ✅ DONE 2026-07-06 (no decision
  needed).** Investigation dissolved the "12 vs 16" question: **12 is points**,
  used consistently across the live Layout + paint paths (`CssBoxProperties`
  writes the root size as `"12pt"`; `PaintWalker` comments confirm 12pt), and
  **16 is dead** — the bridge's `CssBoxModel` `16f` is inside a class marked
  `[Obsolete("Unused at runtime… Deprecated for removal")]`, a crude px
  placeholder. Since 12pt = 16px (·96/72) they are the *same* physical size, so
  there was never a live conflict and no pixel bisect was required. Scoped to the
  Layout+CSS surface:
  - `CssConstants.FontSize` now forwards to `CssMetrics.DefaultFontSizePt` (was a
    second `12f` literal in the same assembly) — the same-assembly duplication is
    gone; all its Layout consumers are transitively single-sourced.
  - **Folded in the orphaned Layout-side R4/R5 literals** (no prior phase owned
    them, all in the region being edited): `CssLayoutEngine.PtToCssPx` →
    `CssMetrics.PtToPx`; `CssBoxProperties.GetEmHeight` `·(96/72)` → `·PtToPx`;
    `GetRootEmHeight` base → `DefaultFontSizePx`; the two `·1.2` normal-line-height
    fallbacks → `NormalLineHeightFactor`. Every substitution is **bit-identical**
    (e.g. `PtToPx` ≡ `96.0/72.0`, `DefaultFontSizePx` ≡ `12·96/72` ≡ 16.0), so
    behavior-neutral. A grep confirms **zero** `96.0/72.0` / `*1.2` / bare-`FontSize`
    literals remain in `Broiler.Layout`. `Broiler.CSS.Tests` 63/63;
    `Broiler.Layout.Tests` 13/13.
  - *Out of the stated Layout+CSS scope (documented non-targets):* `PaintWalker`'s
    `12f` (renderer/`Broiler.HTML`) is already consistent and can be pointed at
    `CssMetrics` whenever the renderer is next touched; `CssBoxModel`'s `16f`
    (obsolete, removal-pending) is intentionally left for its deprecation.

- **Phase M6 — De-duplicate `HtmlConstants` (Layout/HTML). ✅ DONE 2026-07-06 —
  far smaller than feared.** Two findings shrank this from "the largest
  cross-component move" to a 5-line edit:
  1. The `CommonUtils`/`HtmlUtils` twins the roadmap listed are **already gone** —
     the renderer's `CommonUtils`/`HtmlUtils` no longer contain
     `IsAsianCharecter`/`Max`/`DecodeHtml`, so `Broiler.Layout` holds the only copy
     (not duplication). Only the 5 `HtmlConstants` names were still duplicated.
  2. `Broiler.HTML.Core` **already references `Broiler.Layout`** (renderer → layout),
     and `Broiler.Layout.HtmlConstants` is `public`. So the shared home is Layout
     itself (the lower layer) — no CSS/DOM-kernel layering compromise, no new
     reference.
  Fix: the renderer's `Broiler.HTML.Utils.HtmlConstants` now forwards its
  `A`/`Hr`/`Iframe`/`Img`/`Href` members to `Broiler.Layout.HtmlConstants`
  (identical values → behavior-neutral); `Broiler.Layout` is now the single source.
  `Broiler.HTML.Core` and `Broiler.HTML.Orchestration` build clean.
  *Touches the `Broiler.HTML` submodule* (5 lines in one file) — commit via the
  push-or-patch workflow in `CLAUDE.md`.

Phases M0–M3 are self-contained inside the `Broiler.CSS` submodule. M4 is
parent-repo only. M5/M6 cross the submodule boundary — sequence their
push/pointer-bump per [`CLAUDE.md`](../../CLAUDE.md).

---

## 4. Verification

Every phase reuses the extraction's proven gate — this is a refactor, so parity is
the whole test:

- **Equality unit tests** for `CssMetrics` derived factors (M0) and for
  `ParseLength`/`GetActualBorderWidth` over a fixed corpus of unit strings
  (`px/em/rem/ex/ch/lh/rlh/vw/vh/vmin/vmax/mm/cm/in/pt/pc/Q/%/calc()/min()/max()`)
  — snapshot before M1, assert unchanged after each CSS phase.
- **Architecture tests** stay green (`Broiler.Layout` still references only
  `Broiler.CSS`/`.CSS.Dom`/`.Dom`; no new leak).
- **Pixel parity**: the curated WPT baseline (`dotnet run --project src/Broiler.Wpt`,
  ≤1% differing pixels) and Acid2/Acid3, run before/after via stash A/B to
  neutralize the suite's documented order-flakiness. M4 and M5 are the only phases
  that can move pixels; both must show **zero** regression (M5 may show an
  intentional, documented change if 12↔16 is corrected).
- `scripts/run-rf-layout-validation.ps1` as the closeout runner (same as the
  extraction).

---

## 5. Non-goals

- No new geometry primitive set (replacing `System.Drawing` `RectangleF`/`SizeF`)
  — that is a separate, larger initiative noted in the layout roadmap §4.
- No layout-algorithm changes, no new CSS unit support (the `hi-dpi` TODO at
  `CssLengthParser.cs:210` is enabled by this cleanup but is out of scope here).
- No change to the `ILayoutEnvironment` seam.

---

## 6. Expected payoff

- One edit site per physical factor (hi-DPI, unit tables) instead of 2–5.
- The 12-vs-16 default-font-size inconsistency (R6) is resolved, not just hidden.
- Fewer redundant length re-parses per layout pass (R3/M4).
- Three unit parsers → one; two conversion tables → one.
