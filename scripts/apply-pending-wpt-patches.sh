#!/usr/bin/env bash
#
# Apply pending submodule patches to the checked-out submodule working trees
# before a WPT run, so a fix that could not be pushed to its `MaiRat/`/
# `Broiler-Platform/` submodule remote (push 403 → captured under patches/) is
# still exercised on CI, which otherwise runs strictly against the pinned
# submodule pointers.
#
# Scope: ONLY the patches listed in PENDING_PATCHES below. Patches whose fix is
# already contained in the pinned submodule pointer (e.g. 0013-0016) are NOT
# listed here — they are live on CI through the pointer and must not be
# re-applied (a second application would conflict).
#
# Idempotent: a patch already present in the checked-out tree (reverse-apply
# succeeds) is skipped, so this stays correct after a maintainer applies the
# fix upstream and the pointer is bumped. The build compiles submodule source
# in place, so applying to the working tree is sufficient — no commit, no
# pointer bump.
#
# Each entry is "<submodule-dir>|<patch-file-relative-to-repo-root>".

set -euo pipefail

# Patches whose fix is not in the pinned submodule pointer and could not be
# pushed to the submodule remote (push 403 → captured under patches/).
PENDING_PATCHES=(
  "Broiler.HTML|patches/0012-html-bg-clip-text-tables.patch"
  "Broiler.JS|patches/0013-js-ilcodegen-declare-temp-fallback.patch"
  "Broiler.JS|patches/0014-js-ilcodegen-assignparameter-temp-fallback.patch"
)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

applied=0
skipped=0

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
