# Broiler.UI Directory Structure Path Inventory

**Status:** Phase 0 record  
**Date:** 2026-07-09  
**Roadmap:** `docs/roadmap/broiler-ui-directory-structure-refactor.md`

This inventory lists the path-sensitive surfaces that must be reviewed before
moving `Broiler.UI` project directories into the proposed `src/`, `tests/`, and
`samples/` topology. It intentionally ignores ordinary C# namespaces and
package IDs because Phase 0 approved a path-only refactor.

## 1. Decisions

- ADR 0019 approves the target taxonomy.
- The first filesystem move must not rename assemblies, namespaces, package IDs,
  public types, or project filenames.
- Live build surfaces must be updated when projects move.
- Historical phase records may remain archival if updating them would rewrite
  evidence rather than current build instructions.

## 2. Primary move-sensitive files

These files must be updated as part of the runtime/test/sample directory moves:

| Surface | File | Why it is sensitive |
|---|---|---|
| Component solution | `Broiler.UI/Broiler.UI.slnx` | Contains every UI project path and solution folder grouping |
| Aggregate solution | `Broiler.slnx` | Contains aggregate project paths for UI runtime, tests, and demos |
| UI project references | `Broiler.UI/**/*.csproj` | Most UI projects reference sibling UI projects by relative path |
| Browser graphics app | `src/Broiler.Browser.Windows/Broiler.Browser.Windows.csproj` | References selected standard UI controls |
| Writer app | `src/Broiler.Writer.Windows/Broiler.Writer.Windows.csproj` | References selected standard UI controls, RichEdit, and dialogs |
| Packaging script | `scripts/pack-all.ps1` | Packs `Broiler.UI.slnx` and `Broiler.UI.All` by path |
| Packaging sync script | `scripts/sync-packaging.ps1` | Contains the `Broiler.UI` component directory name |
| Linux CI | `.github/workflows/linux-port-build.yml` | Publishes the Linux UI demo by project path |
| Component README | `Broiler.UI/README.md` | Lists projects and validation commands using current paths |
| Root README | `README.md` | Mentions `Broiler.UI.All` as a package; verify no path changes are needed |

## 3. Architecture tests with embedded project paths

These tests contain literal project paths that will need updates when project
directories move:

| File | Embedded path role |
|---|---|
| `Broiler.UI/Broiler.UI.Standard.Tests/StandardArchitectureTests.cs` | Standard/core dependency allowlist |
| `Broiler.UI/Broiler.UI.Toolbar.Tests/ToolbarArchitectureTests.cs` | Toolbar abstraction and implementation boundaries |
| `Broiler.UI/Broiler.UI.RichEdit.Tests/RichEditArchitectureTests.cs` | RichEdit abstraction boundaries |
| `Broiler.UI/Broiler.UI.RichEdit.Standard.Tests/StandardRichEditArchitectureTests.cs` | RichEdit standard implementation boundaries |

## 4. Live docs and commands to update

These files contain current build/test/run commands or live component
navigation. Update them when paths move:

| File | Examples |
|---|---|
| `Broiler.UI/README.md` | `dotnet test Broiler.UI\Broiler.UI.slnx`, Linux demo run/build commands |
| `scripts/pack-all.ps1` | `Broiler.UI/Broiler.UI.slnx`, `Broiler.UI/Broiler.UI.All/Broiler.UI.All.csproj` |
| `.github/workflows/linux-port-build.yml` | `Broiler.UI/Broiler.UI.Linux.Demo/Broiler.UI.Linux.Demo.csproj` |
| `docs/roadmap/broiler-ui-directory-structure-refactor.md` | Phase 1-3 commands and examples |

## 5. Phase boundary records

Boundary JSON files record historical architecture snapshots. They include
relative project paths and reference paths:

```text
Broiler.UI/docs/phase1/phase1-boundary.json
Broiler.UI/docs/phase2/phase2-boundary.json
Broiler.UI/docs/phase3/phase3-boundary.json
Broiler.UI/docs/phase4/phase4-boundary.json
Broiler.UI/docs/phase5/phase5-boundary.json
Broiler.UI/docs/phase6/phase6-boundary.json
Broiler.UI/docs/phase7/phase7-boundary.json
```

Recommended handling:

- keep historical records unchanged if they are treated as evidence for the
  phase as it was implemented;
- add a new topology boundary record after the move if tests need current paths;
  and
- do not silently rewrite old phase records without marking them as migrated.

## 6. Historical implementation records

The phase implementation records contain commands that mention old paths,
including now-historical PhaseN test commands. Treat these as archival unless a
document is still used as a live validation guide:

```text
Broiler.UI/docs/phase1/implementation-record.md
Broiler.UI/docs/phase2/implementation-record.md
Broiler.UI/docs/phase3/implementation-record.md
Broiler.UI/docs/phase4/implementation-record.md
Broiler.UI/docs/phase5/implementation-record.md
Broiler.UI/docs/phase6/implementation-record.md
Broiler.UI/docs/phase7/implementation-record.md
Broiler.Documents/docs/phase-0.md
```

## 7. External roadmap references

Roadmaps mention UI project names and sometimes project paths. Most are
conceptual and should not block the move, but search them during the path update
PR:

```text
docs/roadmap/broiler-ui-component.md
docs/roadmap/broiler-ui-rich-edit-control.md
docs/roadmap/broiler-ui-ux-guidelines.md
docs/roadmap/broiler-documents-component.md
docs/roadmap/nuget-packaging-roadmap.md
```

## 8. Search commands used for this inventory

```powershell
rg -n "Broiler\.UI[/\\].*\.csproj|Broiler\.UI\.slnx" . `
  --glob "!**/bin/**" --glob "!**/obj/**" `
  --glob "!tests/wpt/**" --glob "!tests/wpt-baseline/**"

rg -n "Broiler\.UI\.[A-Za-z0-9_.-]+/Broiler\.UI\.[A-Za-z0-9_.-]+\.csproj|Broiler\.UI\\Broiler\.UI\.[A-Za-z0-9_.-]+\\Broiler\.UI\.[A-Za-z0-9_.-]+\.csproj" `
  docs Broiler.UI README.md scripts .github `
  --glob "!**/bin/**" --glob "!**/obj/**"
```

## 9. Phase 0 exit status

Phase 0 is complete when reviewed with ADR 0019:

- taxonomy approved: yes;
- category mapping approved: yes;
- no API/package rename in first move: yes;
- path-updating surfaces inventoried: yes.
