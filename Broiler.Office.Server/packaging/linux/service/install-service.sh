#!/bin/sh
#
# install-service.sh — install BOSS (the Broiler Office Standalone Server) as a system service.
#
# Run it from the extracted preview package, as root:
#
#   sudo ./service/install-service.sh                        # auto-detect systemd or SysV
#   sudo ./service/install-service.sh --urls http://0.0.0.0:5555
#   sudo ./service/install-service.sh --init sysv --prefix /srv/boss --user www-data
#
# It copies the server next to its vendored wwwroot into --prefix, creates the service account,
# installs the unit / init script plus its configuration file, and (unless --no-start) enables and
# starts the service. Re-running it upgrades the payload in place; an existing configuration file is
# never overwritten (the new one is written alongside as *.new).
#
# The payload is the whole extracted package directory: the server shares its runtime files with
# Broiler.Browser and Broiler.Writer, which sit in the same folder, so those two ride along into
# --prefix as well. They are inert there — nothing starts them, and the service only ever runs
# Broiler.Office.Server.
#
# Undo with ./uninstall-service.sh.

set -eu

PREFIX=/opt/broiler/office-server
SERVICE_USER=broiler
SERVICE_NAME=broiler-office-server
URLS="http://0.0.0.0:5555"
INIT=auto
START=1

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# This script lives in <package>/service/, so the parent is the package root — which is also where
# the server, its appsettings.json and its wwwroot/ are published.
PAYLOAD_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)

usage() {
    # The header comment above is the help text: print from line 3 up to the first line that is not
    # a comment, so editing the header cannot leave --help printing shell code after it.
    sed -n '3,$ { /^#/!q; s/^# \{0,1\}//p; }' "$0"
    cat <<EOF

Options:
  --init auto|systemd|sysv   Service manager to target (default: auto).
  --prefix DIR               Install directory (default: $PREFIX).
  --user NAME                Service account, created if missing (default: $SERVICE_USER).
  --urls URLS                Listening addresses (default: $URLS).
  --no-start                 Install and enable, but do not start the service.
  -h, --help                 Show this help.
EOF
}

fail() { echo "install-service.sh: $*" >&2; exit 1; }

while [ $# -gt 0 ]; do
    case "$1" in
        --init) INIT=${2:-}; shift 2 ;;
        --prefix) PREFIX=${2:-}; shift 2 ;;
        --user) SERVICE_USER=${2:-}; shift 2 ;;
        --urls) URLS=${2:-}; shift 2 ;;
        --no-start) START=0; shift ;;
        -h|--help) usage; exit 0 ;;
        *) fail "unknown option '$1' (try --help)" ;;
    esac
done

[ "$(id -u)" = "0" ] || fail "must run as root (try: sudo $0 $*)"
[ -f "$PAYLOAD_DIR/Broiler.Office.Server" ] || \
    fail "Broiler.Office.Server not found in $PAYLOAD_DIR — run this script from the extracted package."
[ -d "$PAYLOAD_DIR/wwwroot" ] || \
    fail "$PAYLOAD_DIR/wwwroot is missing — the package is incomplete; the server has nothing to serve."

case "$INIT" in
    auto)
        if [ -d /run/systemd/system ] && command -v systemctl >/dev/null 2>&1; then
            INIT=systemd
        elif [ -d /etc/init.d ]; then
            INIT=sysv
        else
            fail "could not detect a service manager; pass --init systemd or --init sysv"
        fi
        ;;
    systemd|sysv) ;;
    *) fail "--init must be auto, systemd or sysv" ;;
esac

echo "BOSS install"
echo "  service manager : $INIT"
echo "  install prefix  : $PREFIX"
echo "  service account : $SERVICE_USER"
echo "  listening on    : $URLS"
echo

# ── Service account ──────────────────────────────────────────────────────────────────────────────
if id "$SERVICE_USER" >/dev/null 2>&1; then
    echo "==> account '$SERVICE_USER' already exists"
else
    echo "==> creating system account '$SERVICE_USER'"
    if command -v groupadd >/dev/null 2>&1 && command -v useradd >/dev/null 2>&1; then
        groupadd --system "$SERVICE_USER" 2>/dev/null || true
        useradd --system --gid "$SERVICE_USER" --home-dir "$PREFIX" \
                --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"
    elif command -v addgroup >/dev/null 2>&1 && command -v adduser >/dev/null 2>&1; then
        # busybox / Alpine
        addgroup -S "$SERVICE_USER" 2>/dev/null || true
        adduser -S -D -H -G "$SERVICE_USER" -h "$PREFIX" -s /sbin/nologin "$SERVICE_USER"
    else
        fail "no useradd/adduser available — create the '$SERVICE_USER' account manually and re-run"
    fi
fi

# ── Payload ──────────────────────────────────────────────────────────────────────────────────────
echo "==> installing the server into $PREFIX"
mkdir -p "$PREFIX"
# The vendored wwwroot is content-hashed: a stale asset from a previous release can be paired with
# a freshly-hashed runtime by a cached index.html, so replace it wholesale rather than merging.
rm -rf "$PREFIX/wwwroot"
# `.` copies the contents (including dotfiles) rather than nesting the directory.
cp -a "$PAYLOAD_DIR/." "$PREFIX/"
chown -R root:root "$PREFIX" 2>/dev/null || true
chmod 0755 "$PREFIX/Broiler.Office.Server"

# ── Service manager ──────────────────────────────────────────────────────────────────────────────
# Replaces the packaged defaults; '|' as the sed delimiter keeps URLs and paths readable.
rewrite() {
    sed -e "s|/opt/broiler/office-server|$PREFIX|g" \
        -e "s|^User=broiler$|User=$SERVICE_USER|" \
        -e "s|^Group=broiler$|Group=$SERVICE_USER|" \
        -e "s|^BOSS_USER=broiler$|BOSS_USER=$SERVICE_USER|" \
        -e "s|--urls http://0.0.0.0:5555|--urls $URLS|g" \
        "$1" > "$2"
}

# Writes to $2 unless it already exists, in which case the new content lands in $2.new so a local
# edit survives an upgrade.
install_config() {
    if [ -f "$2" ]; then
        rewrite "$1" "$2.new"
        echo "    kept existing $2 (new version written to $2.new)"
    else
        rewrite "$1" "$2"
        chmod 0640 "$2"
        echo "    wrote $2"
    fi
}

if [ "$INIT" = systemd ]; then
    echo "==> installing the systemd unit"
    rewrite "$SCRIPT_DIR/systemd/$SERVICE_NAME.service" "/etc/systemd/system/$SERVICE_NAME.service"
    chmod 0644 "/etc/systemd/system/$SERVICE_NAME.service"
    mkdir -p /etc/broiler
    install_config "$SCRIPT_DIR/systemd/$SERVICE_NAME.env" /etc/broiler/office-server.env

    systemctl daemon-reload
    systemctl enable "$SERVICE_NAME" >/dev/null
    if [ "$START" = 1 ]; then
        systemctl restart "$SERVICE_NAME"
        # Type=notify: systemctl returns once the server reports READY=1, so this reflects a real bind.
        systemctl --no-pager --lines=0 status "$SERVICE_NAME" || true
    fi

    echo
    echo "Done. Manage it with:"
    echo "  systemctl {start|stop|restart|status} $SERVICE_NAME"
    echo "  journalctl -u $SERVICE_NAME -f"
    echo "  \$EDITOR /etc/broiler/office-server.env   # listening addresses and environment"
else
    echo "==> installing the SysV init script"
    rewrite "$SCRIPT_DIR/sysv/$SERVICE_NAME" "/etc/init.d/$SERVICE_NAME"
    chmod 0755 "/etc/init.d/$SERVICE_NAME"

    if [ -d /etc/sysconfig ]; then
        install_config "$SCRIPT_DIR/sysv/$SERVICE_NAME.default" "/etc/sysconfig/$SERVICE_NAME"
    else
        mkdir -p /etc/default
        install_config "$SCRIPT_DIR/sysv/$SERVICE_NAME.default" "/etc/default/$SERVICE_NAME"
    fi

    if command -v update-rc.d >/dev/null 2>&1; then
        update-rc.d "$SERVICE_NAME" defaults >/dev/null
    elif command -v chkconfig >/dev/null 2>&1; then
        chkconfig --add "$SERVICE_NAME"
        chkconfig "$SERVICE_NAME" on
    elif command -v rc-update >/dev/null 2>&1; then
        rc-update add "$SERVICE_NAME" default >/dev/null
    else
        echo "    no update-rc.d/chkconfig/rc-update found — register /etc/init.d/$SERVICE_NAME manually"
    fi

    [ "$START" = 1 ] && "/etc/init.d/$SERVICE_NAME" restart

    echo
    echo "Done. Manage it with:"
    echo "  service $SERVICE_NAME {start|stop|restart|status}"
    echo "  tail -f /var/log/$SERVICE_NAME.log"
fi

echo
echo "Check it is serving:"
echo "  curl -fsS ${URLS%%;*}/healthz && echo"
