# BOSS — Broiler Office Standalone Server

A self-contained **ASP.NET Core / Kestrel** web server that hosts the Broiler Office web apps. It
serves the **Broiler Writer** WebAssembly word processor (`Broiler.Writer.WebAssembly`) directly from
Kestrel — no external static file server (Python `http.server`, nginx, …) required.

## Hosting model — vendored publish

The Writer is a `Microsoft.NET.Sdk.WebAssembly` app (not Blazor). The classic Blazor *hosted* model
(`Microsoft.AspNetCore.Components.WebAssembly.Server` + a `ProjectReference`) serves it fine in
development but, on **publish**, never bakes the WebAssembly SDK's transformed `index.html` (the
import map + content-hashed script references) into the output — it ships the raw placeholder
`index.html` and the app cannot boot.

So BOSS instead **vendors the Writer's *published* `wwwroot`** — the transformed `index.html`, the
`_framework/` mono-wasm runtime, and every content-hashed asset — and serves it as plain static files.
That is exactly the bundle the standalone `python -m http.server` flow serves, just hosted by Kestrel,
and it behaves identically under `dotnet run` and `dotnet publish`.

How the bundle gets here (see [`Broiler.Office.Server.csproj`](Broiler.Office.Server.csproj)):

1. `PublishWriterClient` publishes `Broiler.Writer.WebAssembly` **trimmed** (untrimmed crashes mono at
   boot) into `obj/writer-client/`. It is skipped once staged; force a refresh with
   `-p:ForceWriterClientPublish=true` after changing the Writer.
2. `VendorWriterClientForRun` copies that bundle into this project's `wwwroot/` for `dotnet run`.
3. `VendorWriterClientForPublish` copies it into the publish output `wwwroot/` for `dotnet publish`.

`wwwroot/` is generated and git-ignored. [`Program.cs`](Program.cs) serves it with a content-type
provider that covers the `_framework` runtime blobs (`.wasm` → `application/wasm`, ICU `.dat`, …).

## Endpoints

| Route        | Purpose                                                            |
| ------------ | ----------------------------------------------------------------- |
| `/`          | Broiler Writer (WebAssembly).                                      |
| `/healthz`   | Liveness probe — returns `OK`.                                     |
| `/api/info`  | JSON: server identity, version, and the list of hosted apps.      |

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

Publish the **project file**, not the directory. The directory also contains
[`Broiler.Office.Server.slnx`](Broiler.Office.Server.slnx), and `dotnet publish` prefers the solution —
which fans the single-file / RID / self-contained flags out to the two WebAssembly projects in the
solution (`Broiler.Writer.WebAssembly`, `Broiler.Graphics.WebAssembly`), and those cannot be
single-file-published (`MSB4057` / `NETSDK1099`). Naming the `.csproj` avoids that.

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

## Adding more Office apps

Vendor another client's published `wwwroot` (under its own sub-path) and add an entry to the
`hostedApps` list in [`Program.cs`](Program.cs).

## Solution

[`Broiler.Office.Server.slnx`](Broiler.Office.Server.slnx) bundles the server, the Writer WebAssembly
client, and the direct-Canvas graphics backend. Like the other WebAssembly samples, it is a
repo-root sibling with its own solution file and is not part of the root `Broiler.slnx`.
