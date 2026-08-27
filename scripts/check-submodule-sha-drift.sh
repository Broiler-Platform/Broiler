#!/usr/bin/env bash
#
# HtmlBridge complexity-reduction Phase 1 (project-graph repair), item 4.
#
# Root builds collapse each canonical kernel (Broiler.Dom, Broiler.Graphics) to the ROOT
# submodule checkout via $(BroilerDomPath) / $(BroilerGraphicsPath) (see the root
# Directory.Build.props). Several submodules carry a *nested* checkout of those kernels for
# their own standalone builds, among them:
#
#   Broiler.CSS/Broiler.DOM        vs  root Broiler.DOM
#   Broiler.HTML/Broiler.Graphics  vs  root Broiler.Graphics
#
# If a nested checkout is bumped to a different commit than the root one, a standalone
# submodule build would compile different sources than the canonical node used by root
# builds. This guard fails when a nested submodule gitlink SHA drifts from the root one.
#
# Broiler.Graphics is deliberately NOT checked. Since the component directories became
# submodules, the root Broiler.Graphics is on the src/ layout that Broiler.Documents,
# Broiler.Media, Broiler.UI, Broiler.Writer and Broiler.Browser all pin, while Broiler.HTML
# still pins the pre-src/ commit. That divergence is expected and is what the two rewrite
# blocks in Directory.Build.targets exist for; restore the check once Broiler.HTML follows
# Broiler.Graphics into src/ and its gitlink is bumped to the root commit.
#
# It reads recorded gitlink SHAs with `git ls-tree`. The root gitlinks are read from the
# parent repo; the nested gitlinks are read from each first-level submodule's own tree, so
# Broiler.CSS must be checked out (its nested content is not needed).
# Run from the repository root.
set -euo pipefail

cd "$(dirname "$0")/.."

gitlink_sha() {
  # $1 = repo dir ("." for root, else the submodule path); $2 = path within that repo.
  git -C "$1" ls-tree HEAD "$2" | awk '$2 == "commit" { print $3 }'
}

status=0

check() {
  local label="$1" root_sha="$2" nested_sha="$3"
  if [ -z "$root_sha" ] || [ -z "$nested_sha" ]; then
    echo "ERROR: could not resolve a gitlink SHA for $label (root='$root_sha' nested='$nested_sha')"
    status=1
  elif [ "$root_sha" != "$nested_sha" ]; then
    echo "DRIFT: $label"
    echo "         root   = $root_sha"
    echo "         nested = $nested_sha"
    status=1
  else
    echo "OK: $label ($root_sha)"
  fi
}

check "Broiler.DOM (root vs Broiler.CSS/Broiler.DOM)" \
  "$(gitlink_sha . Broiler.DOM)" \
  "$(gitlink_sha Broiler.CSS Broiler.DOM)"

echo "SKIP: Broiler.Graphics (root vs Broiler.HTML/Broiler.Graphics) — the root leads Broiler.HTML's"
echo "      pre-src/ pin on purpose; see the header comment."

if [ "$status" -ne 0 ]; then
  echo
  echo "Nested submodule checkout(s) drifted from the canonical root. Re-point the nested"
  echo "checkout to the same commit as the root submodule (or bump both together)."
fi

exit "$status"
