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
PENDING_PATCHES=(
  "Broiler.CSS|patches/0135-css-media-queries-range-and-custom-media.patch"
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
