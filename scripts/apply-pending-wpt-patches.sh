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
PENDING_PATCHES=(
  # `:dir()` was a recognised-but-unmodelled pseudo-class, so it matched every
  # element. WPT-relevant (issue #1538 problems 16 and 20,
  # css/css-shadow/shadow-directionality-001 and -002) and it applies cleanly to
  # the pinned Broiler.CSS pointer.
  "Broiler.CSS|patches/0102-css-dir-pseudo-class.patch"
  # An outer <svg> was sized to the 300×150 default object size instead of
  # taking its width from the containing block and its height from the viewBox
  # ratio. WPT-relevant (issue #1552 problems 25–28, inert/inert-svg-hittest,
  # the-dialog-element/inert-svg-hittest, accessibility/svg-mouse-listener and
  # svg/animations/svgrect-animation-invalid-value-1) and it applies cleanly to
  # the pinned Broiler.HTML pointer. Needs the main-repo half (the aspect-ratio
  # transfer widened past block-level boxes) to have any effect; that half is
  # already in-tree.
  "Broiler.HTML|patches/0116-html-svg-viewbox-intrinsic-sizing.patch"
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
