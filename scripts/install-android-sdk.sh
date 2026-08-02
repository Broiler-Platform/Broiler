#!/bin/bash
# Provisions everything needed to build net10.0-android projects in a Broiler
# container: the .NET `android` workload and the Android SDK.
#
# This is deliberately NOT part of the SessionStart hook. The workload is roughly
# 4 GB and the SDK another 0.5 GB, which is too much to pay on every session when
# most work never touches Android. Run it when you need Android.
#
# Idempotent and safe to re-run. Requires the session's egress policy to allow
# dl.google.com; see docs/architecture/android.md for what happens when it does
# not.
#
# Usage:
#   scripts/install-android-sdk.sh
#   ANDROID_SDK_HOME=/custom/path scripts/install-android-sdk.sh
#
# Afterwards, export the SDK location for the build:
#   export ANDROID_HOME=<path>  ANDROID_SDK_ROOT=<path>
set -uo pipefail

SDK_DIR="${ANDROID_SDK_HOME:-/root/android-sdk}"

# Versions verified together on 2026-07-30. The API level and build-tools track
# the workload's Microsoft.Android.Sdk major (36); bump them together.
CMDLINE_TOOLS_BUILD="${ANDROID_CMDLINE_TOOLS_BUILD:-15859902}"
ANDROID_API="${ANDROID_API_LEVEL:-36}"
BUILD_TOOLS="${ANDROID_BUILD_TOOLS:-36.0.0}"

log() { echo "[install-android-sdk] $*"; }
fail() { echo "[install-android-sdk] ERROR: $*" >&2; exit 1; }

# --- 1. .NET android workload --------------------------------------------------
#
# The packs come from nuget.org. Note the re-run below: on a container whose
# user-level manifest is newer than the SDK's built-in one, the first install
# downloads packs for the older manifest and then garbage-collects them as
# unreferenced, reporting success while leaving nothing on disk. Running it again
# against the now-resolved manifest installs the matching packs. Checking for the
# reference assembly is what makes this detectable rather than silent.
android_ref_present() {
  { [ -n "${DOTNET_ROOT:-}" ] && compgen -G "${DOTNET_ROOT}/packs/Microsoft.Android.Ref.*/*/ref/*/Mono.Android.dll" >/dev/null 2>&1; } ||
    compgen -G "${HOME}/.dotnet/packs/Microsoft.Android.Ref.*/*/ref/*/Mono.Android.dll" >/dev/null 2>&1 ||
    compgen -G "/usr/lib/dotnet/packs/Microsoft.Android.Ref.*/*/ref/*/Mono.Android.dll" >/dev/null 2>&1
}

if android_ref_present; then
  log "android workload already installed (Mono.Android.dll present)."
else
  log "Installing the .NET android workload ..."
  dotnet workload install android || fail "dotnet workload install android failed."

  if ! android_ref_present; then
    log "Reference pack missing after install (manifest/GC mismatch); re-running ..."
    dotnet workload install android || fail "dotnet workload install android failed on re-run."
  fi

  android_ref_present || fail "android workload installed but Mono.Android.dll is still missing."
  log "android workload installed."
fi

# --- 2. Android SDK ------------------------------------------------------------
SDKMANAGER="$SDK_DIR/cmdline-tools/latest/bin/sdkmanager"

if [ ! -x "$SDKMANAGER" ]; then
  log "Downloading Android command-line tools (build $CMDLINE_TOOLS_BUILD) ..."
  tmp_zip="$(mktemp --suffix=.zip)"
  tmp_dir="$(mktemp -d)"
  url="https://dl.google.com/android/repository/commandlinetools-linux-${CMDLINE_TOOLS_BUILD}_latest.zip"

  if ! curl -sSL --connect-timeout 20 --max-time 600 "$url" -o "$tmp_zip"; then
    rm -rf "$tmp_zip" "$tmp_dir"
    fail "Could not download $url. If this is a 403, the session's egress policy does not allow dl.google.com — see docs/architecture/android.md."
  fi

  unzip -q "$tmp_zip" -d "$tmp_dir" || { rm -rf "$tmp_zip" "$tmp_dir"; fail "Could not unpack the command-line tools."; }
  mkdir -p "$SDK_DIR/cmdline-tools"
  rm -rf "$SDK_DIR/cmdline-tools/latest"
  mv "$tmp_dir/cmdline-tools" "$SDK_DIR/cmdline-tools/latest"
  rm -rf "$tmp_zip" "$tmp_dir"
  log "Command-line tools installed at $SDK_DIR."
else
  log "Command-line tools already present at $SDK_DIR."
fi

export ANDROID_HOME="$SDK_DIR"
export ANDROID_SDK_ROOT="$SDK_DIR"

# sdkmanager is a Java tool, so it picks up the proxy truststore that the
# container already exports through JAVA_TOOL_OPTIONS. No extra CA wiring needed.
if [ ! -f "$SDK_DIR/platforms/android-$ANDROID_API/android.jar" ]; then
  log "Accepting SDK licenses ..."
  yes | "$SDKMANAGER" --licenses --sdk_root="$SDK_DIR" >/dev/null 2>&1

  log "Installing platform-tools, platforms;android-$ANDROID_API, build-tools;$BUILD_TOOLS ..."
  "$SDKMANAGER" --sdk_root="$SDK_DIR" \
    "platform-tools" \
    "platforms;android-$ANDROID_API" \
    "build-tools;$BUILD_TOOLS" >/dev/null 2>&1 ||
    fail "sdkmanager could not install the SDK packages."
fi

[ -f "$SDK_DIR/platforms/android-$ANDROID_API/android.jar" ] ||
  fail "android.jar is missing after installation."

log "Android SDK ready at $SDK_DIR (API $ANDROID_API, build-tools $BUILD_TOOLS)."
log "Export these before building a net10.0-android project:"
log "  export ANDROID_HOME=$SDK_DIR ANDROID_SDK_ROOT=$SDK_DIR"
