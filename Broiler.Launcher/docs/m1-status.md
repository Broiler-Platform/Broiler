# M1: split-button delivery

M1 implements the reusable UI control and its launcher integration. P0 Windows
graphical validation and source publication were confirmed by the user; Linux
runtime support is deferred.

- `UiSplitButton` and `StandardSplitButton` live in the existing Button abstraction
  and standard packages. No new package IDs or Graphics changes are required.
- Launcher cards use separate Launch and arrow segments. Only explicit controls
  launch products; card background clicks no longer launch. Missing products
  disable Launch while keeping the arrow available. Refresh updates that state.
- Menus contain Install for user, Install for all and Start BOSS, disabled until
  their service milestones are implemented. Settings is usable now. There is no
  update button until release checks and managed inventory exist in M5.
- UI menu command dispatch now rejects disabled/separator/non-leaf items before
  executing a named command. Split buttons reuse the existing menu implementation,
  including capture, focus, navigation and popup rendering.

## Package handoff

The launcher pins the UI package family to `0.1.0-preview.24`, the next candidate
after the public preview.23 observed during implementation. Preview.24 publication
is now confirmed on NuGet.org. The launcher restored exclusively from the public
feed into an empty package cache, built with zero warnings/errors, and passed all
51 tests. Commit the aggregate submodule pointer together with the launcher
integration changes. Graphics preview.13, already consumed by UI, suffices.

For local package-based verification from the aggregate repository:

```powershell
./Broiler.UI/eng/pack.ps1 -Version 0.1.0-preview.24 -Output artifacts/office-m1-release
./Broiler.Launcher/eng/verify-m1.ps1 -PackageFeed ./Broiler.UI/artifacts/office-m1-release
```

The local feed is a development artifact, not a checked-in NuGet source or a
project-reference override. Public-only restore of the new launcher pin is now
verified. Repeat the Windows graphical check for split-button interaction with
the published packages.

The verification helper uses a temporary NuGet configuration and isolated cache,
mapping UI packages to the candidate feed and external dependencies to NuGet.org.
It builds and tests the Windows release configuration without changing checked-in
NuGet sources. Temporary diagnostics are retained at the printed location.

The pack script requires an empty output directory; choose a fresh directory when
repeating package generation, or restore directly from the generated feed above.

## Validation completed

- UI standard controls: 643 passed, including eight split-button tests covering
  primary/menu independence, disabled state, keyboard, touch, Tab traversal,
  dismissal, focus/capture, viewport placement and popup semantic rows.
- UI foundation: 55 passed.
- Launcher: 51 passed against the packed candidate packages.
- Windows Release-Windows solution: built with zero warnings and zero errors
  after an isolated-cache candidate restore. Transitive UI controls brought in
  by Hosting are pinned to the same preview.24 family.
- All 66 UI packages packed and passed metadata/assembly/symbol/dependency checks;
  the component's feed-verification script restored the complete family with an
  isolated cache and public external dependencies.
- Aggregate and UI whitespace checks passed. Broiler.Graphics has no changes.

The Windows executable for the user's interaction check is
`src/Broiler.Launcher.Windows/bin/Release-Windows/net10.0-windows/win-x64/Broiler.Launcher.Windows.exe`.
New graphical interaction has automated session coverage; this task did not
perform a human Windows screen-reader or graphical desktop inspection. Publication
and public-feed-only restore are verified. The aggregate integration commit
(including the UI submodule pointer) and a Windows graphical recheck remain.
Linux runtime acceptance is deferred with the platform scope.
