# Phase 0 Record

Date: 2026-07-03

Roadmap source: `docs/roadmap/broiler-media-component.md`, Phase 0.

Objective: establish evidence and repository/package decisions without changing
existing components.

## Scope landed

- Added a new root component folder: `Broiler.Media`.
- Recorded the canonical Graphics checkout and duplicate-copy policy.
- Captured the current public Graphics image API and direct consumers.
- Ran the existing Graphics image and Direct2D image baselines.
- Wrote Phase 0 ADRs for topology, buffer ownership, image pixel format,
  compatibility, and the Windows `IMFMediaEngine` borrowed-HWND boundary.
- Left the aggregate solution and existing component source files unchanged.

## Environment

Observed with `dotnet --info`:

| Item | Value |
| --- | --- |
| SDK | 10.0.301 |
| Host runtime | 10.0.9 |
| MSBuild | 18.6.4 |
| OS | Windows 10.0.26200 |
| RID | win-x64 |

## Canonical checkout

Command:

```powershell
git submodule status --recursive
```

Observed relevant entries:

| Checkout | Commit | Branch |
| --- | --- | --- |
| `Broiler.Graphics` | `94e6709fd05f8828d188d95539be54c50e93d628` | `heads/main` |
| `Broiler.HTML/Broiler.Graphics` | `94e6709fd05f8828d188d95539be54c50e93d628` | `heads/main` |
| `Broiler.HTML` | `5c26c21e6c31ae8ab53f3f323616e5aea5392034` | `heads/main` |

Decision: `Broiler.Graphics` at the aggregate root is the canonical editable
checkout for media extraction. The nested `Broiler.HTML/Broiler.Graphics`
checkout is a mirrored submodule at the same revision and must not become a
second editable source of Media code.

## Package and project-reference policy

During aggregate development, the workspace may use one root `Broiler.Media`
checkout. Standalone downstream components should consume released packages once
packages exist. Conditional local project references are allowed only for the
single aggregate checkout and must not point through nested component copies.

Duplicate editable `Broiler.Media` copies are prohibited.

## Baseline commands

| Area | Command | Result |
| --- | --- | --- |
| Graphics PNG/APNG/JPEG/BMP, bitmap/canvas, CPU image renderer | `dotnet run --project Broiler.Graphics\Broiler.Graphics.Tests\Broiler.Graphics.Tests.csproj --no-restore` | Passed: `76/76`, exit code `0` |
| Direct2D image/backend smoke tests | `dotnet run --project Broiler.Graphics\Broiler.Graphics.Windows.Tests\Broiler.Graphics.Windows.Tests.csproj` | Passed: `9/9`, exit code `0` |

The first Direct2D attempt used `--no-restore` and failed before running tests
because `Broiler.Graphics.Windows.Tests/obj/project.assets.json` was absent.
The normal `dotnet run` command restored and executed successfully.

## Baselines not executed in this pass

The broader CLI, WPT, DevSite, and pixel-diff suites are documented as required
baselines before parser movement, but were not executed here because they are
larger integration suites and several use third-party test packages in existing
projects. Phase 0 keeps the new Media component dependency-free; those existing
test projects can still be run by the aggregate workspace.

Recommended follow-up commands before Phase 2 parser moves:

```powershell
dotnet test src\Broiler.Cli.Tests\Broiler.Cli.Tests.csproj
dotnet test src\Broiler.Wpt.Tests\Broiler.Wpt.Tests.csproj
dotnet run --project src\Broiler.Engines.Baseline\Broiler.Engines.Baseline.csproj
```

## Exit gate status

| Gate | Status |
| --- | --- |
| Baseline commands/results documented | Complete for Graphics core and Direct2D; broader integration commands recorded for follow-up |
| No canonical-source ambiguity remains | Complete: root Graphics is canonical; nested Graphics is read-only mirror |
| Decisions needed to scaffold public contracts are approved enough for Phase 1 | Complete as ADR defaults; revisit before API freeze |

