# BOSS — Broiler Office Standalone Server

A self-contained **ASP.NET Core / Kestrel** web server and local WebAssembly launcher that hosts
the **Broiler Office** web suite directly from Kestrel — no external static file server (Python
`http.server`, nginx, …) required:

- **`/`** — **Broiler Office Launcher** (`Broiler.Launcher.WebAssembly`)
- **`/writer/`** — **Broiler Writer** (`Broiler.Writer.WebAssembly`)
- **`/cell/`** — **Broiler Cell** (`Broiler.Cells.WebAssembly`)
- **`/plate/`** — **Broiler Plate** (`Broiler.Plate.WebAssembly`)

## Hosting model — vendored publish

Each Broiler Office web app is a `Microsoft.NET.Sdk.WebAssembly` app (not Blazor). The classic
Blazor *hosted* model (`Microsoft.AspNetCore.Components.WebAssembly.Server` + a `ProjectReference`)
serves it fine in development but, on **publish**, never bakes the WebAssembly SDK's transformed
`index.html` (the import map + content-hashed script references) into the output — it ships the raw
placeholder `index.html` and the app cannot boot.

So BOSS instead **vendors each client's *published* `wwwroot`** — the transformed `index.html`, the
`_framework/` mono-wasm runtime, and every content-hashed asset — and serves them as plain static
files. It behaves identically under `dotnet run` and `dotnet publish`.

How the bundles get here (see [`Broiler.Office.Server.csproj`](Broiler.Office.Server.csproj)):

1. `PublishOfficeClients` publishes `Broiler.Launcher.WebAssembly`, `Broiler.Writer.WebAssembly`,
   `Broiler.Cells.WebAssembly`, and `Broiler.Plate.WebAssembly` **trimmed** (untrimmed crashes mono
   at boot) into `obj/<app>-client/`. Each is skipped once staged; force a refresh with
   `-p:ForceOfficeClientsPublish=true` (or per-client `ForceLauncherClientPublish`,
   `ForceWriterClientPublish`, `ForceCellClientPublish`, `ForcePlateClientPublish`).
2. `VendorOfficeClientsForRun` copies those bundles into this project's `wwwroot/` (`wwwroot/`,
   `wwwroot/writer/`, `wwwroot/cell/`, `wwwroot/plate/`) for `dotnet run`.
3. `VendorOfficeClientsForPublish` copies them into the publish output `wwwroot/` for
   `dotnet publish`.

`wwwroot/` is generated and git-ignored. [`Program.cs`](Program.cs) serves it with a content-type
provider that covers the `_framework` runtime blobs (`.wasm` → `application/wasm`, ICU `.dat`, …),
provides local same-origin catalog metadata at `/office-web.json`, and adapts the HTML shell links
so the Launcher and every Office app navigate locally without modifying any SRI-hashed asset.

## Endpoints

| Route              | Purpose                                                                |
| ------------------ | ---------------------------------------------------------------------- |
| `/`                | Broiler Office Launcher (WebAssembly).                                 |
| `/writer/`         | Broiler Writer — word processor (WebAssembly).                         |
| `/cell/`           | Broiler Cell — spreadsheet (WebAssembly).                              |
| `/plate/`          | Broiler Plate — document & graphics viewer (WebAssembly).              |
| `/office-web.json` | Local same-origin Office catalog consumed by the WebAssembly Launcher. |
| `/healthz`         | Liveness probe — returns `OK`.                                         |
| `/api/info`        | JSON: server identity, version, and the list of hosted apps.           |

## Run (development)

```powershell
dotnet run --project Broiler.Office.Server
```

Kestrel listens on `https://localhost:7300` and `http://localhost:5300` (see
[`Properties/launchSettings.json`](Properties/launchSettings.json)). Open the root URL for the Writer.

> The **first** build publishes the trimmed WebAssembly client (native emscripten link — a minute or
> two) and vendors it. Subsequent builds reuse the staged bundle. Requires the wasm workload:
> `dotnet workload install wasm-tools`.

## Publish (single self-contained bundle)

Publish the **project file**, not the root
[`Broiler.Office.Server.slnx`](../Broiler.Office.Server.slnx). Publishing the solution would fan the
single-file / RID / self-contained flags out to its WebAssembly projects, and those cannot be
single-file-published (`MSB4057` / `NETSDK1099`). Naming the `.csproj` avoids that.

Within the project the same flags are stripped before the Writer client is published (the `MSBuild`
task hands its caller's global properties to the child, so `-r linux-x64` would otherwise reach a
browser-wasm project and fail the same way) — so any RID, self-contained or single-file combination
below is safe.

Framework-dependent (portable, needs the .NET runtime on the target):

```powershell
dotnet publish Broiler.Office.Server/Broiler.Office.Server.csproj -c Release -o out/boss
```

Single-file, self-contained, for a specific target (no .NET runtime needed on the target) — e.g.
`linux-arm64` (swap the RID for `linux-x64`, `win-x64`, `osx-arm64`, …):

```powershell
dotnet publish Broiler.Office.Server/Broiler.Office.Server.csproj -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true -o out/boss-linux-arm64
```

Prefer an output path **outside** the project tree (`out/…` above, or an absolute path). A publish
that lands inside the project directory is re-swept into the next publish via the Web SDK's `**/*.json`
content glob; the `.csproj`/`.gitignore` guard the usual sinks (`artifacts/`, `publish/`, RID folders),
but out-of-tree sidesteps the whole class.

The output is one deployable folder: the single executable (its `.pdb`, `appsettings*.json`) plus a
`wwwroot/` holding the complete, trimmed Writer bundle — it boots the Writer over Kestrel with no other
dependencies. Run it directly (on a matching linux-arm64 host):

```bash
./out/boss-linux-arm64/Broiler.Office.Server
```

The published server finds its `wwwroot/` next to the executable, so it can be started from any
working directory — which is what a service manager does. `Program.cs` also calls `UseSystemd()` and
`UseWindowsService()`, so the same binary runs unchanged as a `Type=notify` systemd unit or a
Windows service.

## Shipping: preview packages and service files

BOSS ships inside every **Broiler Preview Package** (BPP) — Linux and Windows, self-contained and
framework-dependent — in the **package root**, alongside `Broiler.Browser` and `Broiler.Writer`,
whose runtime files it shares. The
[`Prepare Broiler Preview Package`](../.github/workflows/broiler-preview-package.yml) workflow
publishes the server, lays the packaging assets around it
([`scripts/package-boss.ps1`](../scripts/package-boss.ps1)), and smoke-tests the result
([`scripts/smoke-test-boss.ps1`](../scripts/smoke-test-boss.ps1)) before anything reaches a release.
Every desktop assembly in a BPP is published ReadyToRun, so the server starts without JITting its
startup path.

What a user gets next to the binary — end-user README, foreground launcher, and ready-to-install
service definitions (a hardened systemd unit, an LSB SysV init script, a Windows service installer,
nginx/Apache reverse-proxy samples) — lives in [`packaging/`](packaging/README.md).

```bash
sudo ./service/install-service.sh --urls http://0.0.0.0:5555     # systemd or SysV
```

## Adding more Office apps

Vendor another client's published `wwwroot` (under its own sub-path) and add an entry to the
`hostedApps` list in [`Program.cs`](Program.cs).

## Solution

The root [`Broiler.Office.Server.slnx`](../Broiler.Office.Server.slnx) bundles the server, the Writer
WebAssembly client, and their complete transitive project-reference closure. It is separate from
the desktop application and test solutions.
