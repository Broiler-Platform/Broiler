# Submodule patches waiting to be applied

**Four patches are waiting on a maintainer.** See the index below.

`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`, `Broiler.JS` and `Broiler.Graphics`
are git submodules with their own remotes. A session whose GitHub scope is this
repository alone cannot push to them — the git proxy answers **403** — so a fix
that belongs in a submodule is committed there, exported with
`git format-patch`, and left here for a maintainer to apply. The submodule
working tree is then reverted to its pinned commit and **the gitlink is not
bumped**: CI clones a submodule by pointer, and a pointer to a commit that was
never pushed would break the build.

Applying one:

```sh
cd <Submodule>
git checkout -b <branch> && git am ../patches/NNNN-<slug>.patch
git push origin HEAD
cd .. && git add <Submodule>      # bump the pointer only once the push succeeds
```

## This directory is a backlog, not an archive

A patch is deleted from here the moment its fix is upstream and the submodule
pointer is bumped, because from then on it reaches CI through the pointer and a
file that can only ever be skipped is noise. `scripts/apply-pending-wpt-patches.sh`
holds the matching list — the subset whose fix can move rendered pixels, so a
WPT run exercises it rather than testing against the un-fixed pointer — and is
idempotent, so a patch already contained in the pinned pointer is skipped rather
than re-applied.

**Check the pointer, not this file, before concluding a fix is pending.** The
numbering is *recycled*: numbers are assigned from `0001` against whatever the
directory holds at the time, so a patch number in an older commit message, code
comment or document does **not** identify the same change as today's patch of
that number. Prose that names a patch by number alone is evidence about the past
only. To decide whether a submodule fix is live, look for its commit:

```sh
git -C <Submodule> log --oneline --grep '<the commit subject>'
git -C <Submodule> merge-base --is-ancestor <sha> HEAD && echo "live on CI"
```

## Recently emptied

The six patches this directory held until 2026-08-12 (`0001`–`0006`) are all
upstream, and each is an ancestor of the pinned pointer, so every one of them is
live on CI:

| was | submodule | commit | subject |
| --- | --- | --- | --- |
| `0001` | `Broiler.HTML` | `1bf117a` | Let a caller say what the canvas composites its background against |
| `0002` | `Broiler.HTML` | `be76c7f` | Treat an empty inset clip as a clip, not as no clip |
| `0003` | `Broiler.DOM` | `55057b8` | Treat `<frame>` as a void element in the parser and serializer |
| `0004` | `Broiler.HTML` | `d1cdad4` | Paint a frame's canvas opaque only when its colour scheme differs from its embedder's |
| `0005` | `Broiler.HTML` | `f8db3c6` | Paint the four 3D border styles as bevels instead of flat sides |
| `0006` | `Broiler.HTML` | `f86b655` | Mitre a border's corners and anti-alias the diagonal |

Pinned at the time of writing: `Broiler.HTML` `f86b655`, `Broiler.DOM` `55057b8`
— the tips of `0006` and `0003` respectively.

They were not merely *semantically* present: each is a real commit in its
submodule's history and an ancestor of the pinned pointer, checked with
`merge-base --is-ancestor`. Worth recording, because by then **none of the six
applied cleanly in either direction any more** — the surrounding code had moved
on, so `git am` would have failed and a reverse-apply check would have said "not
applied". A drifted patch file for an applied fix is worse than no file: it reads
as outstanding work and cannot be applied to find out otherwise.

## Index

The first three come from
[the test-suite retirement roadmap item](../docs/ROADMAP.md#retire-obsolete-test-suites-and-historical-test-artifacts);
`0004` is the submodule half of a WPT fix. All four apply cleanly to the pointers
pinned as of this writing. Identify them by commit subject rather than by number —
the numbering restarts against whatever this directory holds.

Only `0004` is registered in `scripts/apply-pending-wpt-patches.sh`: it is the
only one of the four that can move a rendered pixel, so it is the only one a WPT
run needs applied on top of the pinned pointer.

| # | submodule | subject |
| --- | --- | --- |
| `0001` | `Broiler.JS` | Retire the Repro scratch tests and the legacy solution |
| `0002` | `Broiler.HTML` | Drop the deleted WPF adapter from the public surface |
| `0003` | `Broiler.HTML` | Keep dashed and dotted strokes on the raster path |
| `0004` | `Broiler.HTML` | Size a `<canvas>` as a replaced element, not from presentation width/height |

### Size a `<canvas>` as a replaced element, not from presentation width/height — `Broiler.HTML`

HTML §4.12.5 gives a `<canvas>` its bitmap dimensions from the `width`/`height`
content attributes, defaulting to 300×150. Those are the element's **natural**
size, and the Rendering section maps no presentation `width`/`height` for it —
unlike `<img>` or `<table>`. `TranslateAttributes` projected them onto CSS
`width`/`height` anyway, which made both axes independently *stated*, so
`max-width` and `max-height` clamped each on its own instead of keeping the
natural ratio. Left alone entirely, a `<canvas>` laid out as a non-replaced
inline — the one box type those two properties do not apply to at all.

`CorrectCanvasBoxes` records the attributes as `CssBox.IntrinsicReplacedSize` (a
main-repo property) and makes the box atomic inline-level, so the layout engine
sizes it through the CSS2.1 §10.4 replaced-element rules that landed in
`Broiler.Layout.Engine.ReplacedBoxSizing`. Only the UA default `display` is
replaced; an author `display` is kept. Fallback content between the tags is
hidden, as in any UA that supports canvas.

Listed for the WPT run: without it WPT `css-sizing/replaced-max-size-saturation`
(issue #1624 problem 12) stays at 8.3 %, and every `<canvas>` on every page keeps
laying out with no size at all. See
[WPT rendering gaps, #1624](../docs/wpt-rendering-gaps.md#the-next-run-issue-1624-2026-08-12).

### Retire the Repro scratch tests and the legacy solution — `Broiler.JS`

`ReproTests` and `ReproT` contained no assertion between them. `ReproT` printed
six regex probes to the console; `ReproTests` appended to a hard-coded
`D:\Broiler.JS\repro-out.txt`, which on Linux is a relative filename, so it
created a file with a colon in its name and passed. That is why the submodule's
status document recorded `ReproTests.Repro` as a host-environment *failure*.

`ReproT` goes outright — `Issue725Tests` and `Issue723Tests` already assert that
an unmatched optional group comes back `undefined` from `exec`. `ReproTests` was
probing something nothing else covers: `super` property lookup inside a class
**field initializer** under direct eval, through an arrow inside eval, and
through an arrow declared in one eval statement and called in the next. That is a
different binding from the derived-constructor `super()` case
`Issue814DerivedConstructorEvalSuperTests` covers. The patch turns those probes
into `ClassFieldInitializerEvalSuperTests`, six asserting tests, all passing
against the pinned engine.

`BroilerJS.sln` is deleted: it cannot restore, referencing `Broiler.Regex` paths
that moved. The standalone `JIntPerfTests` executable goes with it — its eleven
scenarios are exactly the `[Params]` list of `JIntSmokeBenchmarks`, which globs
the same `Scripts` directory. **The script corpus itself is kept.**

Deleting the solution leaves `Broiler.JavaScript.Network` and
`Broiler.JavaScript.NodePollyfill` in no solution. The patch deliberately does
**not** register them, because neither compiles — both still open
`Broiler.JavaScript.Core`, a namespace the engine refactor removed. Reviving them
means repairing the namespace first.

Not listed for the WPT run: it touches no rendering code.

### Drop the deleted WPF adapter from the public surface — `Broiler.HTML`

Three assemblies still granted internals access to the removed
`Broiler.HTML.WPF` — `InternalsVisibleTo("Broiler.HTML.WPF")` in
`Broiler.HTML.Core`, `Broiler.HTML.Dom`, and `Broiler.HTML.Orchestration`. The
assembly is never built, so the grants widened nothing, but a friend grant to a
non-existent assembly is an invitation to recreate it.

The documentation was further out of date than the code: the README described the
renderer as ending in "WPF hosting", listed the assembly among the shipped set,
and documented four public types on it; `docs/architecture.md` named it as one of
two concrete backends and gave it its own hosting section; and two gate lists
still required WPF checks to pass.

The main-repo half has already landed: `SkiaDecouplingGuardTests` asserted that
the deleted `Source/Broiler.HTML.WPF` directory still existed, and that
assertion — its only failure — is gone.

Not listed for the WPT run: documentation and friend grants only, no rendering
change.

### Keep dashed and dotted strokes on the raster path — `Broiler.HTML`

**Fixes a live rendering defect.** A `border-style: dashed` or `dotted` edge
painted **nothing at all**, while `solid`, `double`, and `groove` painted
normally, so a box simply lost its outline.

`GraphicsAdapter.DrawLine`/`DrawRectangle` hand a stroke to the raster canvas
only when the pen `HasSimpleStroke` — a solid colour *and* `DashStyle.Solid`.
Everything else fell through to the graphics compatibility seam, which on a host
with no OS backend resolves to `StubCompatBackend`: `CreatePenPaint` returns an
inert `StubPaint` and `DrawLine` is an empty method body. The stroke was
discarded silently.

The reduction itself —
[`Broiler.Layout.IR.DashedStrokeGeometry`](../Broiler.Layout/Broiler.Layout/IR/DashedStrokeGeometry.cs)
— **is already in this repository**, so the patch is only the two call sites plus
an internal `CurrentDashStyle` accessor on `PenAdapter` (`RPen.DashStyle` is
set-only and the raster path has to read the style back). The geometry is covered
by `DashedStrokeGeometryTests`, 17 cases, which pass without the patch.

The new branch only catches pens with a solid colour and a non-solid dash style —
precisely the case that previously reached the inert stub and painted nothing.
Pens with no solid colour still take the compat path, so **nothing that renders
today changes**; the patch can only add paint where there was none.

Measured on a 3px border around a 60x20 box: dashed 0 -> 388 painted pixels,
dotted 0 -> 484, while solid stays 516, double 344, groove 516.

**Listed in `scripts/apply-pending-wpt-patches.sh`.** It is the one of the three
that moves pixels, and it moves them in the direction the pixel suite measures:
every `css/css-backgrounds` dashed- and dotted-border case currently renders a
blank edge. Without the entry the WPT run would keep scoring the un-fixed
pointer. Until it is applied, dashed and dotted borders remain invisible in this
repository, and no test here detects it — the call site is a submodule file with
no equivalent main-repo layer.
