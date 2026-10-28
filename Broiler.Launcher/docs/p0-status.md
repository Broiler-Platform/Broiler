# P0 delivery status and Office integration contracts

Updated 2026-10-28. This record supplements the aggregate [Office roadmap](../../docs/broiler-office-roadmap.md).

## Delivered

- O0.2/O0.3: platform capability descriptors, explicit Linux Cell/Plate exclusion (also for custom overrides), platform-filtered workspace probes, deterministic ordered root discovery with OS-specific deduplication, correct DLL invocation and token-preserving CLI forwarding. Platform, environment, current-directory and process-start boundaries are injectable for deterministic tests.
- Legacy string arguments remain supported through a documented double-quote/backslash parser. CLI tokens bypass parsing; settings tokens precede CLI tokens. ProcessStartInfo uses ArgumentList without shell expansion. Process handles are disposed after obtaining the PID; startup failures retain their exception and mention permissions/runtime prerequisites.
- O0.4: atomic settings replacement with previous-file backup, normalization of null dictionaries/entries, load diagnostics exposed through service contracts and dashboard refresh, save failures leave active settings unchanged. The settings page validates argument syntax and displays save errors without emitting Saved or leaving the page.
- Linux dispatch now passes shared CLI flags to the shared handler, including missing-value error cases. README offscreen instructions match Linux's interactive default. Platform solutions declare their platform build configurations.

## O0.5: contracts selected for P1

These are version 1 design contracts, not claims that installation, manifest parsing or update services exist. P1 must add executable schema validators and fixtures before consuming downloaded packages.

### Catalog and support

Stable component IDs: `launcher`, `browser`, `writer`, `cell`, `plate`, `boss`. Existing BroilerAppId numeric values and serialized enum names remain unchanged. BOSS and launcher use component identities, without inserting enum members ahead of existing products.

Version 1 supported RIDs: `win-x64` for all six components; `linux-x64` for launcher, browser, writer and boss. Cell and Plate are explicitly unsupported on Linux. Other RIDs are rejected until validated and added to the catalog.

Source entry points:

| Component | Windows project | Linux project |
| --- | --- | --- |
| launcher | `src/Broiler.Launcher.Windows/Broiler.Launcher.Windows.csproj` (under Broiler.Launcher) | `src/Broiler.Launcher.Linux/Broiler.Launcher.Linux.csproj` (under Broiler.Launcher) |
| browser | `Broiler.Browser/src/Broiler.Browser.Windows/Broiler.Browser.Windows.csproj` | `Broiler.Browser/src/Broiler.Browser.Linux/Broiler.Browser.Linux.csproj` |
| writer | `Broiler.Writer/src/Broiler.Writer.Windows/Broiler.Writer.Windows.csproj` | `Broiler.Writer/src/Broiler.Writer.Linux/Broiler.Writer.Linux.csproj` |
| cell | `Broiler.Cells/src/Broiler.Cells.Windows/Broiler.Cells.Windows.csproj` | unsupported |
| plate | `Broiler.Plate/src/Broiler.Plate.Windows/Broiler.Plate.Windows.csproj` | unsupported |
| boss | `Broiler.Office.Server/Broiler.Office.Server.csproj` | same project |

### Release manifest

`office-release.json` has schemaVersion 1, officeVersion, channel (`stable` or `preview`), tag (`office-v<officeVersion>`), commit (full source SHA), minimumLauncherVersion, components and unsupported entries. Components identify product ID, semantic component version, RID, exact asset name, positive byte size, SHA-256 (64 lowercase hex digits), archive format (`zip` or `tar.gz`), relative entryPoint, invocation (`native` or `dotnet`), publishMode and prerequisites. Every product is published into an independent complete dependency closure. BOSS includes the matching complete wwwroot. User documents/configuration are never packaged into version directories.

Illustrative component fragment (not a downloadable release):

```json
{
  "id": "writer", "version": "1.0.0", "rid": "linux-x64",
  "asset": "Broiler-writer-1.0.0-linux-x64.tar.gz",
  "size": 123456, "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
  "archive": "tar.gz", "entryPoint": "Broiler.Writer.Linux",
  "invocation": "native", "publishMode": "self-contained", "prerequisites": []
}
```

Reject unknown schema versions, unknown IDs/RIDs, duplicate component/RID pairs, invalid semantic versions, invalid hashes, non-positive/oversized payloads, and absolute or escaping paths. Examples that MUST fail: `../writer.exe` entry point; `cell/linux-x64`; two writer/linux-x64 entries; a stable release with a prerelease officeVersion. Define limits and signing algorithm/key rotation before implementing remote consumption. No production remote install is enabled until trusted signature verification exists.

### Receipts and activation

A version 1 receipt contains schemaVersion, productId, scope (`user` or `machine`), version, rid, releaseTag, asset, sha256, relative entryPoint, invocation and installedAtUtc. The inventory root determines scope/ownership; receipt text cannot authorize writing another scope. A separate active-version record identifies the committed receipt/version. Stage and validate an entire version on the destination filesystem, then atomically activate; retain the prior version and recover an interrupted commit without claiming an incomplete version is ready.

Roots and privileges follow the aggregate roadmap: user-local data for user installs, ProgramData/Broiler/Office on Windows and /opt/broiler/office on Linux for machine binaries, protected machine inventory, and a narrow elevated helper. P1 must enforce ACL/mode and path validation itself, including revalidation after elevation.

### Resolution and updates

Current implementation precedence: explicit custom override, local payload, ordered workspace candidates, PATH. P1 inserts managed user then managed machine installations between custom and portable/local origins, with a saved preferred installed scope. Inventory enumerates both scopes independently of launch resolution. Custom, workspace, PATH and portable origins are unmanaged; only receipts establish update ownership. Updates preserve scope and require compatible newer semantic component versions. Two scopes require an explicit target. Drafts and unrelated release families never qualify.

## Review dispositions and remaining gates

| Finding | Disposition |
| --- | --- |
| R01/R02/R04/R05/R07/R09 | Implemented; regression coverage in P0LaunchTests and P0PersistenceTests, plus existing tests. Linux runtime/desktop validation remains an external gate. |
| R03 | P1 inventory implementation required; contract above selected. |
| R06 | P1 split-button integration must remove card launch fall-through before new interactive regions ship. |
| R08 | P1 isolated publish closure and BOSS adapter required; existing preview packaging remains unchanged. |
| R10 | User confirmed P0 sources committed and pushed before M1. Launcher sources are now tracked. Existing unrelated submodule content is preserved. An isolated public-feed restore of the new M1 package pin remains a publication gate. |

Baseline: 35 existing tests. Current shared-core suite: 49 passing tests on Windows. Windows Release-Windows project and Linux Release-Linux solution builds succeeded with restore. Compilation on Windows does not establish Linux runtime behavior. A fresh checkout with an empty dependency cache, Linux process/permission behavior, and graphical host interaction are not yet verified.

The user confirmed a successful Windows graphical build test and committed/pushed
sources before M1. P0 is accepted for the Windows-first scope; Linux runtime
validation is deferred. A fresh checkout with an empty dependency cache remains
a distribution validation task, including the new UI package publication in M1.
