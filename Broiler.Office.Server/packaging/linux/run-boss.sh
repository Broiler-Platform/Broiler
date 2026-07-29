#!/bin/sh
#
# run-boss.sh — start BOSS in the foreground from the extracted package.
#
#   ./run-boss.sh                              # http://0.0.0.0:5555
#   ./run-boss.sh --urls http://127.0.0.1:8080 # any argument the server accepts
#   BOSS_URLS=http://[::]:5555 ./run-boss.sh   # or set the port via the environment
#
# Convenience only — for a machine that should serve the Writer across reboots, install the service:
#   sudo ./service/install-service.sh

set -eu

BOSS_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
BOSS_EXEC=$BOSS_DIR/Broiler.Office.Server
BOSS_URLS=${BOSS_URLS:-http://0.0.0.0:5555}

[ -x "$BOSS_EXEC" ] || {
    # Extracting the archive on Windows and copying it over loses the executable bit.
    if [ -f "$BOSS_EXEC" ]; then
        chmod +x "$BOSS_EXEC"
    else
        echo "run-boss.sh: $BOSS_EXEC not found — run this from the extracted BOSS directory." >&2
        exit 1
    fi
}

[ -d "$BOSS_DIR/wwwroot" ] || {
    echo "run-boss.sh: $BOSS_DIR/wwwroot is missing — the server has no Writer bundle to serve." >&2
    exit 1
}

export DOTNET_NOLOGO=1
export ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Production}

# Caller arguments win: they come last, and --urls is last-wins in the configuration provider.
if [ $# -gt 0 ]; then
    exec "$BOSS_EXEC" --urls "$BOSS_URLS" "$@"
fi

echo "BOSS — Broiler Office Standalone Server"
echo "  Writer:  ${BOSS_URLS%%;*}/"
echo "  Health:  ${BOSS_URLS%%;*}/healthz"
echo "  Stop:    Ctrl+C"
echo
exec "$BOSS_EXEC" --urls "$BOSS_URLS"
