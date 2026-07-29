# BOSS packaging assets

Everything that ships **beside** the BOSS binaries in a Broiler Preview Package (BPP): service unit
files, install scripts, launchers and the end-user documentation.

Nothing here is compiled. The `Prepare Broiler Preview Package` workflow
([`.github/workflows/broiler-preview-package.yml`](../../.github/workflows/broiler-preview-package.yml))
publishes `Broiler.Office.Server` into `BOSS/` inside each package and copies this tree in around it,
so the layout below *is* the layout a user extracts.

```
packaging/
├── common/README.md                       → BOSS/README.md            (both platforms)
├── linux/
│   ├── run-boss.sh                        → BOSS/run-boss.sh
│   └── service/                           → BOSS/service/
│       ├── README.md                        Linux service guide
│       ├── install-service.sh               systemd or SysV, auto-detected
│       ├── uninstall-service.sh
│       ├── systemd/broiler-office-server.service
│       ├── systemd/broiler-office-server.env
│       ├── sysv/broiler-office-server
│       ├── sysv/broiler-office-server.default
│       └── reverse-proxy/{nginx,apache}-broiler-office-server.conf
└── windows/
    ├── Start-BOSS.cmd                     → BOSS/Start-BOSS.cmd
    └── service/                           → BOSS/service/
        ├── README.md                        Windows service guide
        ├── Install-BossService.ps1
        └── Uninstall-BossService.ps1
```

`common/` goes into every package; `linux/` and `windows/` go into the packages for that platform
only — so a user never has to work out which half of a folder applies to them.

## Conventions worth keeping

* **Defaults are literal, not templated.** The unit file really says
  `/opt/broiler/office-server`, `User=broiler` and `--urls http://0.0.0.0:5555`, so it can be copied
  to `/etc/systemd/system/` by hand and work. `install-service.sh` rewrites those literals when
  `--prefix`/`--user`/`--urls` say otherwise, which is also why they must stay spelled exactly that
  way — the `sed` expressions in the installer match on them.
* **Settings live outside the unit.** `/etc/broiler/office-server.env` (systemd) and
  `/etc/default/broiler-office-server` (SysV) are never overwritten on upgrade; a new version is
  written as `*.new` beside the existing file.
* **`wwwroot/` is replaced wholesale, never merged.** The Writer bundle is content-hashed, and
  mixing assets from two releases desyncs the Canvas replay stream at load
  (`Unknown replay op …`). Every installer removes the old directory first.
* **The service host is real.** `Program.cs` calls `UseSystemd()` and `UseWindowsService()`, so the
  systemd unit can be `Type=notify` and the Windows service needs no wrapper. It also anchors the
  content root at the executable's directory, so a service manager's working directory cannot hide
  `wwwroot/`.
* Shell scripts are POSIX `sh` (they run on busybox and on RHEL without bash-isms); PowerShell
  scripts stay compatible with Windows PowerShell 5.1, which is what a bare Windows Server has.

## Testing a change

Publish the server and assemble a package by hand:

```bash
dotnet publish Broiler.Office.Server/Broiler.Office.Server.csproj -c Release-Linux \
    -r linux-x64 --self-contained true -o /tmp/BOSS
cp -a Broiler.Office.Server/packaging/common/README.md /tmp/BOSS/
cp -a Broiler.Office.Server/packaging/linux/run-boss.sh /tmp/BOSS/
cp -a Broiler.Office.Server/packaging/linux/service /tmp/BOSS/
/tmp/BOSS/run-boss.sh --urls http://127.0.0.1:5555 &
curl -fsS http://127.0.0.1:5555/healthz
```

The workflow does exactly this and then smoke-tests `/healthz`, `/api/info` and `/` on both
platforms, so a broken package fails the run rather than reaching a release.

For the server itself — the vendored-publish hosting model, endpoints, how to add another Office
app — see [`../README.md`](../README.md).
