#!/bin/bash
# SessionStart hook for Broiler.
#
# Prepares a Claude Code on the web container so the .NET solution can be
# built and the test suites run:
#   1. Installs the .NET 10 SDK (the solution targets net10.0 / net10.0-windows).
#   2. Initializes the git submodules (Broiler.CSS / .DOM / .HTML / .JS / .Graphics).
#
# Both steps are idempotent (safe to re-run) and the whole hook is best-effort:
# a step that cannot complete because the session's egress policy blocks a host
# is logged and skipped rather than aborting session startup. In particular the
# submodules live in separate GitHub repos (MaiRat/Broiler.CSS, ...); a session
# whose egress scope is limited to MaiRat/Broiler will get a 403 cloning them and
# the build of the layout/render projects will be unavailable until a session (or
# CI) with the broader scope runs.
set -uo pipefail

# Only run inside the remote (Claude Code on the web) container.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

REPO_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"
log() { echo "[session-start] $*"; }

# --- 1. .NET 10 SDK ------------------------------------------------------------
if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  log ".NET 10 SDK already present: $(dotnet --version 2>/dev/null)"
else
  log "Installing .NET 10 SDK ..."
  export DEBIAN_FRONTEND=noninteractive

  # The Ubuntu 24.04 archive ships dotnet-sdk-10.0; the Microsoft prod feed is a
  # fallback source for it. Add the feed only if it is reachable and not present.
  if [ ! -f /etc/apt/sources.list.d/microsoft-prod.list ] && \
     [ ! -f /etc/apt/sources.list.d/packages-microsoft-prod.list ]; then
    tmp_deb="$(mktemp --suffix=.deb)"
    if curl -sSL --connect-timeout 20 \
        "https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb" \
        -o "$tmp_deb"; then
      dpkg -i "$tmp_deb" >/dev/null 2>&1 || log "war: could not register Microsoft apt feed"
    else
      log "war: Microsoft apt feed unreachable (egress policy?) — relying on the Ubuntu archive"
    fi
    rm -f "$tmp_deb"
  fi

  apt-get update >/dev/null 2>&1 || log "war: apt-get update reported errors (ignored)"
  if apt-get install -y dotnet-sdk-10.0 >/dev/null 2>&1; then
    log ".NET 10 SDK installed: $(dotnet --version 2>/dev/null)"
  else
    log "ERROR: dotnet-sdk-10.0 install failed — the .NET build will be unavailable"
  fi
fi

# Make sure dotnet is on PATH for the rest of the session.
if command -v dotnet >/dev/null 2>&1; then
  echo "export PATH=\"\$PATH:$(dirname "$(command -v dotnet)")\"" >> "${CLAUDE_ENV_FILE:-/dev/null}"
fi
# Don't phone home / slow first build with the .NET first-run experience.
echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1' >> "${CLAUDE_ENV_FILE:-/dev/null}"
echo 'export DOTNET_NOLOGO=1' >> "${CLAUDE_ENV_FILE:-/dev/null}"

# --- 2. Submodules -------------------------------------------------------------
# The layout/render projects depend on Broiler.CSS/.DOM/.HTML/.JS/.Graphics
# (separate MaiRat/Broiler.* repos pulled in as submodules).
#
# In the web container the agent proxy injects a git config that rewrites
# https://github.com/ to a dedicated git relay; that relay only serves the
# primary repo, so submodule clones 403 through it. The HTTPS egress proxy
# ($HTTPS_PROXY), however, does reach github.com when the session's policy
# allows the submodule repos. So we clone submodules through the egress proxy
# with a clean git config (no insteadOf rewrite). We fall back to a plain
# `submodule update` for non-proxied environments (e.g. CI).
update_submodules() {
  if git -C "$REPO_DIR" submodule update --init --recursive >/dev/null 2>&1; then
    return 0
  fi
  if [ -n "${HTTPS_PROXY:-}" ]; then
    log "Direct submodule clone was blocked; retrying via the egress proxy ..."
    local clean_cfg ca
    clean_cfg="$(mktemp)"
    ca="/root/.ccr/ca-bundle.crt"
    {
      echo "[http]"
      echo "    proxy = ${HTTPS_PROXY}"
      [ -f "$ca" ] && echo "    sslCAInfo = ${ca}"
    } > "$clean_cfg"
    GIT_CONFIG_GLOBAL="$clean_cfg" GIT_CONFIG_SYSTEM=/dev/null \
      git -C "$REPO_DIR" submodule update --init --recursive >/dev/null 2>&1
    local rc=$?
    rm -f "$clean_cfg"
    return $rc
  fi
  return 1
}

if [ -f "$REPO_DIR/.gitmodules" ]; then
  log "Updating git submodules ..."
  if update_submodules; then
    log "Submodules initialized."
  else
    log "war: submodule update failed — the layout/render projects depend on"
    log "      Broiler.CSS/.DOM/.HTML/.JS/.Graphics. This usually means the"
    log "      session's egress policy does not include those repos (403). Re-run"
    log "      in a session/CI with access to MaiRat/Broiler.* to build them."
  fi
fi

# --- 3. Branch-base freshness check (warn only) --------------------------------
# The container's repo baseline is snapshotted when the environment is
# provisioned. `main` here is force-rewritten as each change lands (its history
# churns rather than growing linearly), so a freshly-designated task branch can
# be based on a commit that `main` no longer contains — docs it added (the
# roadmap files) and prior work are then missing locally.
#
# Detect that specific case — HEAD has *diverged* from origin/main (neither
# contains the other), which is what a force-rewrite of the base looks like —
# and warn. A branch that is merely ahead (local commits on top of main) or
# behind (main advanced normally) is NOT flagged. This never resets anything:
# a branch may carry unpushed work, so reconciliation is the agent's call.
check_branch_base() {
  if ! git -C "$REPO_DIR" fetch --quiet origin main 2>/dev/null; then
    log "war: could not fetch origin/main to check branch freshness (egress?)."
    return 0
  fi
  # origin/main ⊇ HEAD (behind/current) or HEAD ⊇ origin/main (ahead) → healthy.
  if git -C "$REPO_DIR" merge-base --is-ancestor HEAD FETCH_HEAD 2>/dev/null \
     || git -C "$REPO_DIR" merge-base --is-ancestor FETCH_HEAD HEAD 2>/dev/null; then
    return 0
  fi
  local base
  base="$(git -C "$REPO_DIR" merge-base HEAD FETCH_HEAD 2>/dev/null || echo '?')"
  log "war: this branch has DIVERGED from the latest origin/main — its base is"
  log "     not in main's current history. origin/main was likely force-rewritten"
  log "     after this container was provisioned, so files/work it added (e.g."
  log "     current documentation) may be MISSING here. Reconcile before working:"
  log "       git fetch origin main"
  log "     then, if this branch has no commits of its own beyond the shared base"
  log "     (${base}), recreate it from main keeping the name:"
  log "       git checkout -B <this-branch> origin/main"
  log "     otherwise REBASE your commits onto it (git rebase origin/main)."
  log "     Never reset/force-restart a branch that carries unpushed commits."
}
check_branch_base

log "Done."
