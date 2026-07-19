# Submodule patches (pending application)

These patches target the `MaiRat/`-style submodule remotes that are outside this
session's GitHub push scope, so the change could not be pushed to the submodule
and its pointer bumped (the push returns 403). Each is captured here as a
`git format-patch` file for a maintainer to apply to the submodule and bump the
gitlink separately. Until a patch is applied, the submodule pointer in this repo
is **unchanged** (CI clones the submodule by its pinned SHA), so anything that
would depend on the patched symbol is left **unwired** in the parent — the parent
still compiles against the pinned submodule.

## 0001-broiler-css-calc-zoom-length-scaling.patch

- **Target submodule:** `Broiler.CSS` (`Broiler-Platform/Broiler.CSS`)
- **Pinned SHA (apply onto):** `f6cef83faf744e600f80ffa0a5c6f6b4ed6719ea`
- **What it does:** adds `CssLengthParser.SetElementZoom(absoluteZoom, percentZoom)`
  — thread-static factors (mirroring the existing viewport-factor pattern) applied
  during length evaluation so a `calc()`/`min()`/`max()` whose sub-terms mix units
  scales each term correctly: absolute units (`px`/`mm`/`cm`/`in`/`pt`/`pc`/`q`) and
  root-relative `rem`/`rlh` by `absoluteZoom`; percentages by `percentZoom`;
  `em`/`ex`/`ch`/`ic`/`lh` and viewport units left untouched. Both factors default to
  the neutral `1.0`, so a caller that never opts in is byte-identical to the
  pre-zoom parser. Includes `Broiler.CSS.Tests/CssLengthZoomTests.cs`.
- **Why it can't be a main-repo change:** the hardcoded absolute unit→pixel factors
  inside `CssLengthParser` are the only lever for per-term `calc()` scaling and are
  not reachable from outside the parser.
- **Parent wiring (deferred until this patch is applied + the pointer bumped):**
  `CssBoxProperties.ParseLengthWithLineHeight` and `ParseInsetLength` should, for a
  length containing `(` and while `NativeZoom` is enabled, call
  `CssLengthParser.SetElementZoom(EffectiveZoom, percentAgainstContainingBlock ? OwnZoom : 1.0)`
  around the `ParseLength` call and reset it to `1.0, 1.0` in a `finally`
  (`ApplyZoomToLength` already skips the `(` so the parser-scaled value is returned
  as-is). This wiring is intentionally **not** committed here because it references
  `SetElementZoom`, which does not exist on the pinned submodule SHA — committing it
  would break the CI build that clones `Broiler.CSS` at `f6cef83`. The whole CSS
  `zoom` feature is gated behind `NativeZoom` (off by default), so nothing on CI
  exercises this path today; the wiring lands together with the pointer bump.
- **Verification:** `Broiler.CSS.Tests` (217 pass, incl. the new `CssLengthZoomTests`);
  the parent wiring was validated locally against the patched submodule via
  `Broiler.Layout.Tests` (a `calc(20px + 10%)` inset under `zoom:2` centred exactly)
  before being reverted for the pinned-pointer build.

## 0002-broiler-html-text-shadow-zoom-scaling.patch

- **Target submodule:** `Broiler.HTML` (`Broiler-Platform/Broiler.HTML`)
- **Pinned SHA (apply onto):** `3319ede468eb78340aa2995bd0528d8c87cf9fd6`
- **What it does:** in `PaintWalker.Text.cs` (the paint IR walker), scales the
  `text-shadow` offsets (`shadowX`/`shadowY`) by the box's effective CSS `zoom`.
  Those offsets are resolved in the paint walker from the raw `text-shadow`
  string rather than from the zoomed box geometry, so under the native-zoom
  engine they would otherwise stay unscaled. Guarded on `EffectiveZoom != 1.0`,
  so it is inert while the native-zoom engine is off (the default).
- **Parent hook (kept in the main repo, unlike patch 0001):** this patch reads a
  *new* field, `Broiler.Layout.IR.ComputedStyle.EffectiveZoom` (populated from
  `CssBox.EffectiveZoom` in `ComputedStyleBuilder.FromBox`), which is committed to
  the parent now. The dependency direction is the reverse of patch 0001 — here the
  *submodule* reads a *parent* field — so keeping the field in the parent is safe:
  the pinned (unpatched) submodule simply does not reference it, and the parent
  compiles either way. The field is the general paint-layer zoom hook (any
  paint-only length the paint layer resolves from a raw string can scale by it),
  so it is useful to land ahead of the patch; it is `1.0` while the flag is off.
- **Why it can't be a main-repo change:** `text-shadow` is parsed and its offsets
  emitted entirely inside the submodule paint walker (`ParseTextShadow` /
  `EmitText`), which the main repo does not own.
- **Verification:** the parent hook is pinned by `ZoomLengthTests`
  (`ComputedStyleIr_Carries_EffectiveZoom_For_PaintOnly_Lengths` — `1.0` off, the
  compounded factor on); the submodule scaling compiled against the patched tree
  (`Broiler.HTML.Orchestration` builds clean) before being reverted for the
  pinned-pointer build. Its rendered effect needs pixel validation (no reftest
  corpus), so it lands behind the `NativeZoom` flag with the rest of the engine.
