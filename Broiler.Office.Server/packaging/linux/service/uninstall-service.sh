#!/bin/sh
#
# uninstall-service.sh — remove the BOSS service installed by install-service.sh.
#
#   sudo ./uninstall-service.sh                 # stop, disable, remove unit + install tree
#   sudo ./uninstall-service.sh --keep-config   # leave /etc/broiler/office-server.env in place
#   sudo ./uninstall-service.sh --keep-files    # leave /opt/broiler/office-server in place
#
# The service account is never deleted: it may own files outside the install tree, and removing a
# uid that is still referenced elsewhere is the more dangerous default. Remove it by hand with
# `userdel broiler` once you are sure.

set -eu

PREFIX=/opt/broiler/office-server
SERVICE_NAME=broiler-office-server
KEEP_CONFIG=0
KEEP_FILES=0

fail() { echo "uninstall-service.sh: $*" >&2; exit 1; }

while [ $# -gt 0 ]; do
    case "$1" in
        --prefix) PREFIX=${2:-}; shift 2 ;;
        --keep-config) KEEP_CONFIG=1; shift ;;
        --keep-files) KEEP_FILES=1; shift ;;
        -h|--help) sed -n '3,12p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) fail "unknown option '$1' (try --help)" ;;
    esac
done

[ "$(id -u)" = "0" ] || fail "must run as root (try: sudo $0 $*)"

if [ -f "/etc/systemd/system/$SERVICE_NAME.service" ]; then
    echo "==> removing the systemd unit"
    systemctl stop "$SERVICE_NAME" 2>/dev/null || true
    systemctl disable "$SERVICE_NAME" 2>/dev/null || true
    rm -f "/etc/systemd/system/$SERVICE_NAME.service"
    systemctl daemon-reload
    systemctl reset-failed "$SERVICE_NAME" 2>/dev/null || true
fi

if [ -f "/etc/init.d/$SERVICE_NAME" ]; then
    echo "==> removing the SysV init script"
    "/etc/init.d/$SERVICE_NAME" stop 2>/dev/null || true
    if command -v update-rc.d >/dev/null 2>&1; then
        update-rc.d -f "$SERVICE_NAME" remove >/dev/null 2>&1 || true
    elif command -v chkconfig >/dev/null 2>&1; then
        chkconfig --del "$SERVICE_NAME" 2>/dev/null || true
    elif command -v rc-update >/dev/null 2>&1; then
        rc-update del "$SERVICE_NAME" default 2>/dev/null || true
    fi
    rm -f "/etc/init.d/$SERVICE_NAME"
fi

if [ "$KEEP_CONFIG" = 0 ]; then
    echo "==> removing configuration"
    rm -f /etc/broiler/office-server.env /etc/broiler/office-server.env.new
    rm -f "/etc/default/$SERVICE_NAME" "/etc/default/$SERVICE_NAME.new"
    rm -f "/etc/sysconfig/$SERVICE_NAME" "/etc/sysconfig/$SERVICE_NAME.new"
    rmdir /etc/broiler 2>/dev/null || true
else
    echo "==> keeping configuration"
fi

if [ "$KEEP_FILES" = 0 ]; then
    echo "==> removing $PREFIX"
    rm -rf "$PREFIX"
    rm -rf "/var/cache/$SERVICE_NAME"
    rm -f "/var/run/$SERVICE_NAME.pid"
    # /opt/broiler is ours only if nothing else lives there; rmdir declines otherwise.
    rmdir "$(dirname "$PREFIX")" 2>/dev/null || true
else
    echo "==> keeping $PREFIX"
fi

echo
echo "Removed. The '$SERVICE_NAME' log file (/var/log/$SERVICE_NAME.log, SysV only) and the service"
echo "account were left untouched."
