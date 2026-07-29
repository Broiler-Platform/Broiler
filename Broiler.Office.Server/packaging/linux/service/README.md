# Running BOSS as a Linux service

Everything here installs **BOSS — the Broiler Office Standalone Server** as a system service so the
Broiler Writer is served across reboots.

```
service/
├── install-service.sh      installs for systemd or SysV (auto-detected)
├── uninstall-service.sh    undoes it
├── systemd/
│   ├── broiler-office-server.service    the unit
│   └── broiler-office-server.env        settings → /etc/broiler/office-server.env
├── sysv/
│   ├── broiler-office-server            LSB init script
│   └── broiler-office-server.default    settings → /etc/default (or /etc/sysconfig)
└── reverse-proxy/
    ├── nginx-broiler-office-server.conf
    └── apache-broiler-office-server.conf
```

---

## Install

From the extracted package, as root:

```bash
sudo ./service/install-service.sh                                  # auto-detect, port 5555
sudo ./service/install-service.sh --urls http://127.0.0.1:5555     # loopback only (reverse proxy)
sudo ./service/install-service.sh --init sysv --prefix /srv/boss --user www-data
```

| Option | Default | Meaning |
| --- | --- | --- |
| `--init auto\|systemd\|sysv` | `auto` | Which service manager to target. `auto` picks systemd when `/run/systemd/system` exists. |
| `--prefix DIR` | `/opt/broiler/office-server` | Where the server and its `wwwroot/` are installed. |
| `--user NAME` | `broiler` | Service account; created as a system user if missing. |
| `--urls URLS` | `http://0.0.0.0:5555` | Listening addresses baked into the configuration file. |
| `--no-start` | — | Install and enable, but leave the service stopped. |

The script copies the payload into `--prefix`, creates the account, installs the unit or init script
plus its configuration file, and starts the service. **Re-running it upgrades in place** — the
payload is refreshed (`wwwroot/` replaced wholesale) and an existing configuration file is kept, with
the new version written beside it as `*.new`.

Check it:

```bash
curl -fsS http://localhost:5555/healthz && echo
```

---

## systemd

```bash
systemctl status broiler-office-server
systemctl restart broiler-office-server
journalctl -u broiler-office-server -f
sudoedit /etc/broiler/office-server.env      # listening addresses, environment, log levels
```

The unit is `Type=notify`: the server links `Microsoft.Extensions.Hosting.Systemd` and signals
`READY=1` only once Kestrel is actually listening, so `systemctl start` returning success means the
site is up — and another unit may order itself `After=broiler-office-server.service`.

**Settings live in `/etc/broiler/office-server.env`, not in the unit.** The unit declares defaults
with `Environment=` and then reads the env file, which is applied afterwards and therefore wins. An
upgrade replaces the unit and leaves your env file alone.

```ini
BOSS_ARGS=--urls http://0.0.0.0:5555
ASPNETCORE_ENVIRONMENT=Production
```

`$BOSS_ARGS` is word-split by systemd, so several arguments work:
`BOSS_ARGS=--urls http://127.0.0.1:5555 --Logging:LogLevel:Microsoft.AspNetCore=Information`.

### Sandboxing

The unit is confined: `ProtectSystem=strict`, `PrivateTmp`, `NoNewPrivileges`, a restricted syscall
filter, and no writable path other than `/var/cache/broiler-office-server`. Two consequences worth
knowing:

* `MemoryDenyWriteExecute` is deliberately **absent** — the .NET JIT needs writable-executable
  pages, and the service will not start with it on.
* If you point the server at a certificate or content outside the install tree, add
  `ReadWritePaths=` / relax `ProtectHome=` accordingly with a drop-in:
  `systemctl edit broiler-office-server`.

### Privileged ports

Binding 80 or 443 as a non-root user needs a capability. Uncomment in the unit (or add a drop-in):

```ini
AmbientCapabilities=CAP_NET_BIND_SERVICE
CapabilityBoundingSet=CAP_NET_BIND_SERVICE
```

Terminating TLS in a reverse proxy is usually the better answer — see `reverse-proxy/`.

---

## SysV init

For systems without systemd (older RHEL/CentOS, Devuan, busybox images):

```bash
service broiler-office-server start|stop|restart|status
tail -f /var/log/broiler-office-server.log
$EDITOR /etc/default/broiler-office-server        # or /etc/sysconfig/… on RHEL
```

The init script is LSB-compliant (`status` returns 0 when running, 3 when not), uses
`start-stop-daemon` where available and a plain `su` + PID file elsewhere, and waits up to
`STOP_TIMEOUT` seconds for a graceful stop before `SIGKILL`. There is no journal here, so stdout and
stderr go to `LOGFILE` — add a `logrotate` rule if the service runs long enough for that to matter:

```
/var/log/broiler-office-server.log {
    weekly
    rotate 8
    compress
    missingok
    copytruncate
}
```

---

## Behind a reverse proxy

Run BOSS on loopback and let nginx or Apache terminate TLS:

```bash
sudo ./service/install-service.sh --urls http://127.0.0.1:5555
sudo cp service/reverse-proxy/nginx-broiler-office-server.conf /etc/nginx/sites-available/boss.conf
sudo ln -s /etc/nginx/sites-available/boss.conf /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

Both samples pass `X-Forwarded-*` through and leave BOSS's own `Cache-Control` headers alone — the
Writer bundle depends on them (content-hashed assets `immutable`, the shell `no-cache`). The nginx
sample can also serve the pre-compressed `.br`/`.gz` siblings the build emits instead of
recompressing the wasm payload per request.

---

## Manual install (no scripts)

```bash
sudo useradd --system --no-create-home --shell /usr/sbin/nologin broiler
sudo mkdir -p /opt/broiler/office-server
sudo cp -a ./. /opt/broiler/office-server/
sudo cp service/systemd/broiler-office-server.service /etc/systemd/system/
sudo install -Dm640 service/systemd/broiler-office-server.env /etc/broiler/office-server.env
sudo systemctl daemon-reload
sudo systemctl enable --now broiler-office-server
```

Edit the unit if you install somewhere other than `/opt/broiler/office-server` or run as another
user — the paths and `User=`/`Group=` are literal in the shipped file.

---

## Upgrade

Extract the new package and re-run the installer; the service is restarted for you:

```bash
sudo ./service/install-service.sh --urls http://0.0.0.0:5555
```

Your `/etc/broiler/office-server.env` (or `/etc/default/broiler-office-server`) is preserved.

---

## Uninstall

```bash
sudo ./service/uninstall-service.sh                  # stop, disable, remove unit + files
sudo ./service/uninstall-service.sh --keep-config    # keep the settings file
```

The service account is intentionally left behind — delete it with `userdel broiler` once you are
sure nothing else uses it.

---

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `systemctl start` hangs, then times out | `Type=notify` and the process never reported ready. `journalctl -u broiler-office-server -n 50` shows why — usually a port already in use. |
| `status=203/EXEC` | The executable bit is missing or the path in the unit is wrong: `chmod +x /opt/broiler/office-server/Broiler.Office.Server`. |
| `status=209/STDOUT`, `226/NAMESPACE` | A sandbox directive is too strict for your kernel or filesystem. Relax it in a drop-in: `systemctl edit broiler-office-server`. |
| Service runs, pages 404 | `wwwroot/` did not make it into the install prefix. Re-run the installer from the *complete* extracted package. |
| `Permission denied` on port 80/443 | See **Privileged ports** above. |
| Env file edits do nothing | `systemctl restart broiler-office-server` — the file is read at start. Check for a stray `.new` file next to it. |

For anything above the service layer — arguments, endpoints, configuration precedence, caching —
see [`../README.md`](../README.md).
