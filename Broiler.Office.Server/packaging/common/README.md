# BOSS — Broiler Office Standalone Server

A single Kestrel web server that hosts the **Broiler Office** web apps. Today that is **Broiler
Writer**, the word processor, compiled to WebAssembly and served from this folder — no external web
server, no Node, no Python `http.server`.

Everything the server needs is in this directory: the executable, `appsettings.json`, and `wwwroot/`
holding the complete Writer bundle (the mono-wasm runtime and its content-hashed assets).

---

## Quick start

**Linux**

```bash
./run-boss.sh                                  # http://0.0.0.0:5555
./Broiler.Office.Server --urls http://0.0.0.0:5555
```

**Windows**

```bat
Start-BOSS.cmd
Broiler.Office.Server.exe --urls http://0.0.0.0:5555
```

Then open <http://localhost:5555/> — the Writer boots in the browser.

With no arguments the server falls back to ASP.NET Core's own default, `http://localhost:5000`,
which is reachable **from that machine only**. Pass `--urls` whenever anyone else should reach it.

> **Framework-dependent packages** need the **ASP.NET Core 10 runtime** installed
> (<https://dotnet.microsoft.com/download/dotnet/10.0> — the *ASP.NET Core Runtime*, not just the
> .NET Runtime). **Self-contained packages** carry their own runtime and need nothing installed.

---

## Sample arguments

| Argument | What it does |
| --- | --- |
| `--urls http://0.0.0.0:5555` | Listen on **every** IPv4 interface, port 5555. The usual choice for a server other machines connect to. |
| `--urls http://127.0.0.1:5555` | Loopback only — the right setting when a reverse proxy sits in front. |
| `--urls "http://[::]:5555"` | Every interface, IPv4 **and** IPv6. Quote it: brackets are shell metacharacters. |
| `--urls "http://127.0.0.1:5555;http://10.0.0.4:8080"` | Several endpoints at once, semicolon-separated. |
| `--urls http://0.0.0.0:80` | Port 80. Needs root on Linux (or `CAP_NET_BIND_SERVICE`, see `service/`) and an elevated prompt on Windows. |
| `--environment Development` | Turns on the developer exception page and loads `appsettings.Development.json`. Do not use for a public deployment. |
| `--contentRoot /srv/boss` | Look for `wwwroot/` (and `appsettings.json`) somewhere other than beside the executable. |
| `--Logging:LogLevel:Default=Debug` | Raise the log level — any configuration key works as `--Section:Key=value`. |
| `--Logging:LogLevel:Microsoft.AspNetCore=Information` | Log every request (URL, status, duration). Off by default. |

Common combinations:

```bash
# Public listener, quiet logs — a plain deployment.
./Broiler.Office.Server --urls http://0.0.0.0:5555

# Behind nginx/Apache: loopback only, request logging on.
./Broiler.Office.Server --urls http://127.0.0.1:5555 --Logging:LogLevel:Microsoft.AspNetCore=Information

# Two ports, one of them HTTPS with a PFX bundle.
./Broiler.Office.Server --urls "http://0.0.0.0:5555;https://0.0.0.0:5556" \
    --Kestrel:Certificates:Default:Path=/etc/broiler/office-server.pfx \
    --Kestrel:Certificates:Default:Password=secret
```

Every argument has an environment-variable twin, which is what the service unit files use:

```bash
ASPNETCORE_URLS=http://0.0.0.0:5555
ASPNETCORE_ENVIRONMENT=Production
Logging__LogLevel__Default=Information          # ':' becomes '__' in an environment variable
```

Precedence, lowest to highest: `appsettings.json` → `appsettings.<Environment>.json` → environment
variables → command-line arguments. So a command-line `--urls` always wins.

---

## Endpoints

| Route | Purpose |
| --- | --- |
| `/` | Broiler Writer. Any unknown path also returns the app shell, so client-side routes survive a refresh. |
| `/healthz` | Liveness probe. Returns `200 OK` with the body `OK` — point your load balancer or monitoring here. |
| `/api/info` | JSON: server name, version, and the list of hosted apps. |

```bash
curl -fsS http://localhost:5555/healthz && echo
curl -fsS http://localhost:5555/api/info | jq
```

---

## Running it as a service

Prepared unit files, install scripts and reverse-proxy samples live in [`service/`](service/):

```bash
sudo ./service/install-service.sh --urls http://0.0.0.0:5555     # Linux: systemd or SysV, auto-detected
```

```powershell
# Windows, from an elevated prompt
powershell -ExecutionPolicy Bypass -File .\service\Install-BossService.ps1 -OpenFirewall
```

See [`service/README.md`](service/README.md) for the details — the systemd unit, the SysV init
script, the Windows service, and nginx / Apache configuration.

---

## Configuration file

`appsettings.json` sits next to the executable and is read at startup:

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

Useful edits:

* `"AllowedHosts": "office.example.com"` — reject requests carrying any other `Host` header.
* `"Kestrel": { "Limits": { "MaxConcurrentConnections": 200 } }` — cap concurrent connections.
* `"Kestrel": { "Certificates": { "Default": { "Path": "…pfx", "Password": "…" } } }` — HTTPS
  without a reverse proxy.

Prefer environment variables or command-line arguments for anything host-specific: they survive an
in-place upgrade, which overwrites `appsettings.json`.

---

## Caching and upgrades

The Writer bundle is content-hashed: every runtime asset carries its hash in the file name and is
served `immutable`, while `index.html` and the Canvas replay module keep stable URLs and are served
`no-cache`. That combination is what makes an upgrade safe — a browser picks up the new shell
immediately and, through it, the new hashed assets.

When you replace this folder, **replace `wwwroot/` wholesale** rather than copying files over an
existing one. A leftover asset from an older release can be paired with the newer runtime by a
cached shell, and the Writer then fails at load with `Unknown replay op …`. The install scripts in
[`service/`](service/) already do this. If a browser is stuck on a stale bundle, a hard reload
(Ctrl+Shift+R) clears it.

---

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| `Failed to bind to address … address already in use` | Another process holds the port. Pick a different one with `--urls`, or find the holder: `ss -lptn 'sport = :5555'` / `netstat -ano \| findstr :5555`. |
| Page loads but everything 404s; log says `The WebRootPath was not found` | `wwwroot/` is missing next to the executable — the package was extracted partially, or only the binary was copied. Re-extract the whole folder. |
| `Permission denied` starting on Linux | The executable bit was lost (usually by unzipping on Windows): `chmod +x Broiler.Office.Server`. |
| `Permission denied` binding port 80/443 on Linux | Ports below 1024 are privileged. Use 5555 behind a reverse proxy, or grant `CAP_NET_BIND_SERVICE` (see `service/README.md`). |
| Reachable locally, not from another machine | Either the listener is loopback-only (use `--urls http://0.0.0.0:5555`) or a firewall is in the way (`ufw allow 5555/tcp`, or `-OpenFirewall` on Windows). |
| `You must install .NET to run this application` | A framework-dependent package without the ASP.NET Core 10 runtime installed. Install it, or use the self-contained package. |
| Writer fails at load with `Unknown replay op …` | A stale, cached bundle. Hard-reload the browser; on the server, replace `wwwroot/` wholesale rather than merging. |

Turn up the detail when something is unclear:

```bash
./Broiler.Office.Server --urls http://0.0.0.0:5555 \
    --Logging:LogLevel:Default=Debug --Logging:LogLevel:Microsoft.AspNetCore=Information
```

---

## What is in this package

```
BOSS/
├── Broiler.Office.Server[.exe]   the server
├── appsettings.json              configuration
├── wwwroot/                      the Broiler Writer WebAssembly bundle
├── README.md                     this file
├── run-boss.sh / Start-BOSS.cmd  foreground launcher
└── service/                      unit files, install scripts, reverse-proxy samples
```

Source and issue tracker: <https://github.com/Broiler-Platform/Broiler>.
