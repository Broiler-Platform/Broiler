#!/usr/bin/env bash
#
# Apply pending submodule patches to the checked-out submodule working trees
# before a WPT run, so a fix that could not be pushed to its `MaiRat/`/
# `Broiler-Platform/` submodule remote (push 403 → captured under patches/) is
# still exercised on CI, which otherwise runs strictly against the pinned
# submodule pointers.
#
# Scope: ONLY the patches listed in PENDING_PATCHES below. A patch whose fix is
# already contained in the pinned submodule pointer is NOT listed here — it is
# live on CI through the pointer and must not be re-applied. The idempotence
# guard below also means a listed patch stops being applied automatically once a
# maintainer lands it upstream and bumps the pointer (its reverse-apply check
# then succeeds and it is skipped).
#
# Idempotent: a patch already present in the checked-out tree (reverse-apply
# succeeds) is skipped, so this stays correct after a maintainer applies the
# fix upstream and the pointer is bumped. The build compiles submodule source
# in place, so applying to the working tree is sufficient — no commit, no
# pointer bump.
#
# Each entry is "<submodule-dir>|<patch-file-relative-to-repo-root>".
#
# NOTE: entries here are applied on top of the pinned submodule pointers on the
# WPT CI run. The mechanism is idempotent (a patch already contained in the
# pinned pointer is skipped), so an entry stops applying automatically once a
# maintainer lands the fix upstream and bumps the pointer.

set -euo pipefail

# Patches whose fix is not in the pinned submodule pointer and could not be
# pushed to the submodule remote (push 403 → captured under patches/).
#
# Each was checked against the pinned pointer when it was added here: it does
# not reverse-apply (so the pointer does not contain it) and does apply cleanly
# (so it is not stale). The idempotence guard keeps that true — once a
# maintainer lands one upstream and bumps the pointer, its reverse-apply check
# starts succeeding and it is skipped rather than re-applied.
#
# Deliberately NOT listed:
#   * 0040 (Broiler.Graphics, Android OpenGL ES backend) — an Android
#     presentation backend that no WPT test exercises, and it no longer applies
#     to the pinned pointer either.
#   * 0041 (Broiler.HTML, animated-image frame at presentation time) — WPT-
#     relevant (the four css-image-animation *-paused tests) but it NO LONGER
#     APPLIES: the pinned Broiler.HTML has drifted since it was generated.
#     Listing it would fail this script and take the whole run down, so it needs
#     regenerating against the current pointer before it can go back in.
#   * 0123 (Broiler.CSS, cascade rule index) — landed upstream as Broiler.CSS
#     377c6dd and the submodule pointer is bumped, so it reaches CI through the
#     pointer. The idempotence guard would skip it from here on anyway.
#   * 0124 (Broiler.HTML, band-parallel scanline fills) and 0125 (Broiler.Graphics,
#     glyph outline cache) — both landed upstream and both pointers are bumped, so
#     they reach CI through the pointer. Their patch files are deleted for the same
#     reason: this directory is a backlog, not an archive.
#   * 0126 (Broiler.HTML, tile-parallel replay) and 0127 (Broiler.Graphics, raster
#     band parallelism) — both landed upstream and both submodule pointers are
#     bumped, so they reach CI through the pointer. Their patch files are deleted
#     for the same reason: patches/ is a backlog, not an archive. 0126 was listed
#     here until then; its entry is removed rather than left to skip forever,
#     because an entry that can only ever skip is noise.
#   * 0128 (Broiler.CSS, cache sharding) and 0129 (Broiler.HTML, sub-stage trace
#     and warm pass) — multithreading item #12, both upstream and both pointers
#     bumped, so they reach CI through the pointer. They were listed here, and
#     for a stated reason: they *could* have moved rendering if the warm pass and
#     the box walk ever disagreed about an element's cascade. That check has since
#     been run against the pointer rather than against a patch, so the entries go
#     the way 0126's did.
#   * 0130 (Broiler.JS, test parallelization) — upstream. It was never listed:
#     it changes only how `Broiler.JavaScript.BuiltIns.Tests` schedules its own
#     cases, and the WPT run never builds that assembly.
#   * The four "Add a Timeout to every xUnit [Fact]" patches (Broiler.CSS,
#     Broiler.DOM, Broiler.JS and the nested Broiler.Regex) — for the same reason
#     0130 was never listed, only more so: they add an attribute argument to test
#     methods and touch no production code at all, so nothing they change can
#     move a pixel, and the WPT run neither builds nor runs the assemblies they
#     belong to.
#
# Listed below, and deliberately so: 0132 decides whether the render tree is
# rebuilt after a DOM mutation, so a wrong classification is a *stale page* —
# the one failure mode a pixel-comparing suite is built to catch, and one no
# unit test over the classifier can reach. Unlike a thread-safety or scheduling
# patch, this one can move pixels even when the engine below it is perfect.
# (0131 was the first half of the same item and is upstream now, so its entry
# is gone with it: an entry whose idempotence guard can only ever skip is noise.)
#
# 0135 (Broiler.CSS, media-query range syntax and @custom-media) is listed for
# the same reason: it decides whether an `@media` block's rules cascade at all,
# so getting it wrong is a whole stylesheet applying — or not applying — to a
# page. Its unit tests pin the grammar; only the pixel suite can say the rules
# reached the render.
#
# The dashed-stroke patch (Broiler.HTML, "Keep dashed and dotted strokes on the
# raster path") is listed for the plainest reason of all: without it a
# `border-style: dashed` or `dotted` edge paints *nothing*. Such a stroke has a
# solid colour but a non-solid dash style, so it misses the raster fast path and
# falls through to the compat seam, which on a host with no OS backend is an
# inert stub — an empty DrawLine and a StubPaint. Every css/css-backgrounds
# dashed- and dotted-border case therefore renders a blank edge against the
# reference. Its geometry is unit-tested in the main repo
# (DashedStrokeGeometryTests), but only the pixel suite can say the runs reached
# the canvas.
#
# Its two companions from the same roadmap item are deliberately NOT listed:
#   * "Retire the Repro scratch tests and the legacy solution" (Broiler.JS) —
#     deletes assertion-free scratch tests and a solution that cannot restore.
#     It touches no rendering code, and the WPT run never builds the assemblies
#     it changes.
#   * "Drop the deleted WPF adapter from the public surface" (Broiler.HTML) —
#     documentation and three InternalsVisibleTo grants to an assembly that is
#     never built. Nothing it removes can move a pixel.
#
# The canvas patch (Broiler.HTML, "parse: size a <canvas> as a replaced element,
# not from presentation width/height") is listed because it is the half of a
# two-repo fix that decides an element's *box type*. Without it every <canvas>
# lays out as a non-replaced inline — the one box type max-width/max-height do
# not apply to at all — and the main-repo half it feeds (CSS2.1 §10.4 in
# ReplacedBoxSizing, reached through CssBox.IntrinsicReplacedSize) never runs, so
# WPT css-sizing/replaced-max-size-saturation stays at its 8.3 %. The §10.4 table
# itself is unit-tested in the main repo (ReplacedBoxSizingTests); only the pixel
# suite can say a real <canvas> reaches it.
#
# The SVG-image patch that was 0001 before this one ("image: render an SVG image through
# Broiler.Layout's SvgRenderer") landed upstream as Broiler.HTML c77f0f0 and its pointer
# is bumped, so it reaches CI through the pointer and its entry is gone with it.
#
# The object-fit patch that was 0001 before this one ("paint: place replaced content by
# object-fit and object-position") landed upstream as Broiler.HTML d762937 and its pointer
# is bumped, so it reaches CI through the pointer and its entry is gone with it. That
# emptied patches/ entirely, which is why the numbering restarts at 0001 below.
#
# The root-relative stylesheet-href patch that was 0001 before this one landed upstream as
# Broiler.HTML 1d11065 — which is the pinned pointer itself — so it reaches CI through the
# pointer and its entry and file are gone with it. Worth recording *how* it was found, because
# the idempotence guard did not catch it: its reverse-apply check failed rather than succeeded
# (the upstream commit is not byte-identical to the patch as exported), so instead of being
# skipped it was reported as drifted and this script exited 1 on every run — taking down the
# runs it exists to serve, and every later entry with it. A stale entry here is not inert. When
# this script reports drift, check whether the fix is simply upstream before regenerating:
#   git -C <Submodule> log --oneline --grep '<the commit subject>'
#
# The postfix-++ parser patch that was 0001 before this one landed upstream as Broiler.JS
# 434db760 — the pinned pointer itself — so it reaches CI through the pointer and its entry and
# file are gone with it. The @supports-evaluator patch that replaced it went the same way, as
# Broiler.CSS 8be7a65.
#
# The program-numbering and program-dump patches that held 0001 before this one both landed
# upstream, so their files are gone with them.
#
# The eight patches that held 0001–0008 before the two below are all upstream and all reachable
# through the pinned pointers, so their files are deleted and the numbering has restarted. Two of
# them mattered here. The direct-eval one (Broiler.JS 60c9182a) was never listed — it decides
# whether page script runs at all rather than what any of it paints. The media-element one
# (Broiler.HTML a9be60a) WAS listed and is now the pointer itself, so its entry is removed rather
# than left to skip forever: an entry whose idempotence guard can only ever skip is noise.
#
# The one-semicolon `for` head patch (Broiler.JS, "Reject a `for` head that carries only one
# semicolon") IS listed, because what it fixes is a *timeout*, and a timeout is the one failure
# a pixel comparison cannot even reach: the run is aborted before anything is rendered. Without
# it `for(;)` — a SyntaxError — parses as `for(;;)` and spins forever, so all five
# html/webappapis/scripting/processing-model-2 compile-error tests burn the full 30-second
# per-test budget and are reported as timeouts rather than run. With it they finish in ~3 s.
# It is also the only entry here whose absence costs the run *wall-clock* as well as results:
# 2.5 minutes of a shard's budget spent waiting on loops that cannot end.
#
# 0002 (Broiler.HTML, cap the image fetch timeout) is deliberately NOT listed, even though it
# fixes a timeout — but the reason recorded here was wrong for one run, and the correction is worth
# keeping. It said the main-repo half already covered the WPT run: WptTestRunner's image handler
# marks an off-corpus http(s) <img> handled, so the runner never reaches the downloader. That is
# true of the container the runner *paints* with, and only of it. A run lays each document out
# twice — the script bridge lays it out headlessly to answer geometry reads, in a container of its
# own that no handler was ever attached to — and that pass fetched all 31 off-corpus hosts of
# conformance-checkers/html/elements/img/src-isvalid.html at the uncapped 100-second default. The
# test timed out again on 2026-08-16 with the handler in place, which is what showed it.
# Broiler.Layout's OfflineSubresources now declines those loads in the engine, for every container
# rather than one, so the claim holds as stated: no <img> in a run reaches the downloader with an
# off-corpus http(s) URL, and the default this patch caps cannot be hit here. The patch still
# matters for the real browser, where no such policy is installed and an unreachable <img> stalls
# the render — a correctness fix to land upstream, not something a WPT run needs on top of the
# pointer.
#
# The off-corpus stylesheet patch (Broiler.HTML, "load: let the host decline a stylesheet fetch it
# knows cannot succeed") IS listed, and it is the other half of the same finding. Images are
# reachable from the main repo — the load starts in Broiler.Layout's CssBoxImage — but a <link> is
# loaded from DomParser through StylesheetLoadHandler, both submodule files, so the geometry pass's
# sheets cannot be declined without this one line. Without it a run still fetches every off-corpus
# <link> once, at five seconds each: css/CSS2/cascade-import/cascade-import-009.xht (three <link>s
# to a CGI that pauses 2, 5 and 8 seconds by design) renders in 14 s instead of 2 s. That is under
# the 30-second budget — the main-repo half alone already stops it timing out — so this patch buys
# the rest of the budget back rather than the test, and the main repo builds and runs correctly
# whether or not it is applied: it names nothing new in the submodule, and the type the patch calls
# is main-repo.
#
# The anonymous-table-parent patch (Broiler.HTML, "parse: generate the anonymous table a
# misparented table box needs") IS listed, and it is the plainest case for listing there is:
# without it a `display: table-row` / `table-row-group` / `table-cell` box with no table around
# it is neither IsBlock nor IsInline, so block layout walks past it and it paints *nothing*.
# The pass it calls lives in the main repo (Broiler.Layout.Engine.AnonymousTableBoxes) and its
# behaviour is unit-tested there (AnonymousTableBoxParentTests); this one line is what reaches
# it from the box fix-ups, so without the patch applied the main-repo half never runs and every
# css/CSS2/tables/table-anonymous-objects-* test still renders its bare red table with the green
# layer missing.
#
# 0002 (Broiler.JS, "Carry a direct eval's bindings into the closures its functions create")
# landed upstream as Broiler.JS 8564eee2 and the pinned pointer is that commit, so it reaches CI
# through the pointer and its entry is removed rather than left to skip forever. It was listed for
# the real-world render suite rather than for WPT — the same script runs both — because without it
# google.com's bot-detection VM throws "g is not defined" out of the first closure one of its
# evalled opcode handlers builds, and what renders is the interstitial rather than the page.
# The six patches below are the MediaWiki/Vector 2022 rendering item, and every one of them is
# listed for the same reason: each decides what reaches the canvas, not merely how fast it gets
# there, and the main-repo half of each is already unit-tested where a unit test can reach it.
#
#   * 0001 (Broiler.CSS, math functions where a length is expected) — a media feature value that
#     will not parse makes the whole @media block malformed, so its rules never cascade. On the
#     narrow-viewport branch of a skin that writes every breakpoint as `calc(1120px - 1px)` that
#     is a whole stylesheet not applying. The grammar is pinned by Broiler.CSS's own tests in the
#     patch; only a pixel run can say the rules reached the render.
#   * 0002 (Broiler.CSS, :link and :visited apart) — `:visited` matching every link repaints every
#     link on a page in the visited colour.
#   * 0003 (Broiler.HTML, a face per style) — without it `font-weight: bold` and `font-style:
#     italic` draw as regular text, so every glyph of every emphasised run differs.
#   * 0004 (Broiler.HTML, filter a scaled bitmap) — point sampling at a non-integer scale is
#     visibly unlike a browser's output, which is exactly what a pixel comparison measures.
#   * 0005 (Broiler.HTML, itemise flex/grid children and keep a split box's element) — a floated
#     flex item is otherwise taken out of flow and its container sizes as though it were not there;
#     the pass it calls is main-repo (Broiler.Layout.Engine.FlexGridItemBlockification, unit-tested
#     in VectorSkinLayoutTests) and this is what reaches it at the right point in the box fix-ups.
#     The same patch keeps an inline box's element alive when a block-inside-inline split consumes
#     all of its content, which is what WPT
#     css-backgrounds/background-color-body-propagation-003 needs: without it a <body> broken that
#     way has no box for its background to propagate from and the page renders white.
#   * 0006 (Broiler.HTML, clip an outset box-shadow) — an unclipped outer shadow fills the element
#     it is cast by, so a card with a shadow paints as a solid block of shadow colour.
#
# All six of those landed upstream and the pinned pointers contain them, so each reaches CI
# through the pointer now and their entries and patch files are gone. The numbering restarted at
# 0001 against what was left, which was nothing.
#
# 0001 (Broiler.CSS, "Resolve the absolute length units in ParseToPixels") is listed, and it is
# as plain a case as the anonymous-table one above: without it the `border` shorthand reads
# `border: 72pt solid red`'s first component as a *colour*, so the width falls back to `medium`
# and the colour is dropped — a declaration that should paint a 96px red band paints a 3px black
# line instead. The CSS2.1 suite states its geometry in physical units by convention, so this is
# not a corner: `css/CSS2/positioning` alone goes 364 → 394 of 520 reftests with the patch
# applied and none lost. There is no main-repo half to fall back on — the shorthand expansion is
# entirely inside Broiler.CSS — which is precisely why it has to be applied here to reach a run.

# 0002 gives a media query a paged formatting context: `@media print` matching while a document
# is being printed, and `width`/`height` describing the page area rather than the surface the
# runner allocated. `css-page/media-queries-001-print` needs it and 0001 together — it states its
# whole assertion in inches — and it is listed after 0001 for that reason, though they touch
# different files and apply in either order. The main-repo call sites are already in, behind a
# `BROILER_CSS_PAGED_MEDIA` probe that defines itself only once this patch puts CssPagedMedia.cs
# on disk, so applying this here is what switches them on for a run.
# 0003 carries a block-level image's page name onto the anonymous block that replaces it.
# CorrectImgBoxes demotes the image to `display: inline` inside a wrapper, which is how a
# block-level replaced element is laid out here, but a page name hangs on a block-level box and so
# was being dropped — `page-name-img-003`/`-004` broke a page their references do not. It only
# moves pixels under BROILER_WPT_PAGED_PRINT=1, where the page name is read at all; the default
# unpaginated render is unchanged.
# 0006 paints an element its transform mirrors. Without it a `scaleX(-1)`, `scale(-1)` or
# `rotate(180deg)` box paints *nothing at all* — the raster canvas mapped its rectangle to a
# negative extent, which spans no rows or columns — and the compat backend it would otherwise
# fall back to is an inert stub on a host with no OS backend. That is the same failure mode as
# the dashed-stroke and anonymous-table entries above: not slower or coarser output, but absent
# output. Its geometry is unit-tested in the main repo (MirroredTransformPaintTests), which
# feature-probes and self-skips until this is applied; only the pixel suite can say a mirrored
# element reached the canvas.
# 0002 keeps a CSS escape from ending the rule it sits in, and drops three kinds of invalid
# declaration. The escape half is what makes it WPT-relevant beyond the value rules: an escaped
# `\}` inside a declaration value closed its rule, and every rule after it in that stylesheet was
# dropped — a whole-stylesheet failure, not a one-property one, on any sheet that contains one.
# 0003 lets both box-tree correction passes descend into a float. A `display: inline` box holding
# block children anywhere inside a float never had its anonymous blocks built, so that subtree laid
# out at zero size and painted nothing at all — the same absent-output failure mode as the entries
# above, and it needs the pixel suite to say the content reached the canvas. It must stay ordered
# after 0002, whose CssDocumentMode it calls: Broiler.HTML does not compile without it.
#
# Deliberately NOT listed:
#   * 0001 (Broiler.JS, compiled-site lifetime) — a retention fix with no effect on rendered
#     output, so a pixel run has nothing to say about it.
PENDING_PATCHES=(
)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

applied=0
skipped=0

# No pending patches configured: nothing to apply. (Guard the expansion below,
# which would trip `set -u` on an empty array in older bash.)
if [ "${#PENDING_PATCHES[@]}" -eq 0 ]; then
  echo "Pending WPT patches: none configured — nothing to apply."
  exit 0
fi

for entry in "${PENDING_PATCHES[@]}"; do
  submodule="${entry%%|*}"
  patch_rel="${entry##*|}"
  patch_abs="$REPO_ROOT/$patch_rel"
  submodule_dir="$REPO_ROOT/$submodule"

  if [ ! -f "$patch_abs" ]; then
    echo "::error::pending patch not found: $patch_rel"
    exit 1
  fi
  if [ ! -d "$submodule_dir/.git" ] && [ ! -f "$submodule_dir/.git" ]; then
    echo "::error::submodule not checked out: $submodule (need submodules: recursive)"
    exit 1
  fi

  # Already applied? (the pinned pointer, or an earlier run, already contains it)
  if git -C "$submodule_dir" apply --reverse --check "$patch_abs" >/dev/null 2>&1; then
    echo "skip  $patch_rel — already present in $submodule (pinned pointer contains it)"
    skipped=$((skipped + 1))
    continue
  fi

  # Not applied — does it apply cleanly to the checked-out tree?
  if git -C "$submodule_dir" apply --check "$patch_abs" >/dev/null 2>&1; then
    git -C "$submodule_dir" apply "$patch_abs"
    echo "apply $patch_rel → $submodule"
    applied=$((applied + 1))
    continue
  fi

  # Neither already applied nor cleanly applicable: the submodule context has
  # drifted from what the patch was generated against. Surface it — the patch
  # needs regenerating against the current pinned pointer.
  echo "::error::$patch_rel does not apply to $submodule and is not already present — the pinned pointer has drifted; regenerate the patch."
  git -C "$submodule_dir" apply --check "$patch_abs" || true
  exit 1
done

echo "Pending WPT patches: $applied applied, $skipped already present."
