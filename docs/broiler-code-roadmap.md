# Broiler Code roadmap

- **Status:** Phases 0-3 delivered. The composed shell runs on Windows and
  Linux; the Linux head reports its clipboard and IME as unavailable, and its
  window has not yet been exercised on a Linux display
- **Scope:** A C#/.NET IDE that reuses the Broiler Writer application stack and
  supports multi-project workspaces, live diagnostics, and builds for ordinary
  .NET, Android, and browser WebAssembly
- **Last reconciled:** 2026-08-05

## Product goal

Broiler Code should feel like the development counterpart of Broiler Writer:
one platform-neutral application, rendered with Broiler.UI, with thin Windows,
Linux, Android, and WebAssembly hosts. Its first useful release must let a user:

1. open or create a solution containing multiple projects and source files;
2. edit C# with syntax highlighting, line numbers, undo/redo, search, and
   accessible keyboard, pointer, touch, and IME behavior;
3. see syntax, semantic, and build errors both in the editor and in a
   navigable Problems pane;
4. build ordinary .NET output and publish Android and browser-WebAssembly
   applications; and
5. use the same workspace and commands from the supported desktop, Android,
   and browser hosts, subject to an explicit build-execution policy.

The initial language scope is **C# on .NET 10 using SDK-style projects**. The
editor, workspace, diagnostic, and build contracts must remain language-neutral,
but a plug-in language marketplace and general-purpose multi-language IDE are
not part of the first release. Standard `.slnx`/`.sln` and `.csproj` files remain
canonical; Broiler Code must not invent a second project format.

## Current baseline

The repository supplies most of the platform shell, but not an IDE or reusable
compiler subsystem.

| Area | Current evidence | Consequence for Broiler Code |
| --- | --- | --- |
| Shared application | Windows, Linux, and Android instantiate `WriterApp` from `Broiler.Writer.Core`. | Start with one `Broiler.Code.Core`; platform heads contain only host and capability adapters. |
| WebAssembly application | `BrowserWriterDemo` duplicates the Writer shell instead of referencing Writer Core. | Do not create another browser-specific application copy. Extract/reuse a browser host and keep Code behavior in Core. |
| Broiler.UI | Menu, toolbar, tab, list, splitter, dialog, edit, RichEdit, rendering, input, and semantics foundations exist. | Reuse the shell primitives, but add a purpose-built CodeEditor. RichEdit's formatted-document model is not a source buffer. |
| IDE shell gaps | TabView cannot close/reorder/mark dirty tabs, ListView is flat, and no TreeView exists. | Add the shared tab and tree behavior needed by the project explorer and document area. |
| Workspace | Writer owns one path and one document. The repository `.slnx` generator is an engineering closure tool, not a runtime project system. | Add a neutral, versioned workspace and capability-based storage providers. |
| Compilation | Builds are performed by scripts and CI through `dotnet build`/`dotnet publish`; there is no IDE build broker or general C# language service. | Separate responsive language services from an authoritative, out-of-process SDK build. |
| Android | The shared SurfaceView host, input/IME bridge, Storage Access Framework integration, API 36 build, APK/AAB packaging, and signing pipeline exist. | Reuse the host and packaging conventions. A project workspace needs document-tree access rather than Writer's single-document picker. |
| WebAssembly | The .NET 10 browser host, Canvas renderer, input bridge, picker/download integration, and trimmed publish path exist. | Reuse the runtime host. A workspace needs directory/import-export persistence, and an SDK publish must execute outside the browser sandbox. |

## Capability boundary

"Runs Broiler Code" and "builds a target" are separate capabilities. The
first release should make the following matrix visible in the product rather
than silently changing behavior by platform.

| IDE host | Editing and syntax highlighting | Live C# diagnostics | Authoritative build execution | Build targets exposed by the IDE |
| --- | --- | --- | --- | --- |
| Windows / Linux | Local | Local Roslyn service | Trusted local out-of-process worker; optional remote worker | .NET, Android, WebAssembly when the worker has the required workloads |
| Android | Local portable classifier | Local Roslyn only if the measured footprint and target-evaluated graph delivery pass; otherwise compiler diagnostics update after a remote build | Remote isolated worker | .NET, Android, WebAssembly according to worker capabilities |
| Browser WebAssembly | Local portable classifier | Local Roslyn only if payload/memory/trimming and target-evaluated graph delivery pass; otherwise compiler diagnostics update after a remote build | Remote isolated worker | .NET, Android, WebAssembly according to worker capabilities |

The desktop worker uses the installed .NET SDK. Android packaging additionally
requires the .NET Android workload, JDK, and Android SDK. WebAssembly publishing
uses the `wasm-tools` workload and Emscripten-based toolchain. A browser process
cannot run that native SDK toolchain, and installing the full Android toolchain
inside the Android application is not a preview goal. Microsoft also documents
`dotnet publish` as the supported deployment preparation path, the Android
MSBuild packaging process, and the standalone WebAssembly Browser App toolchain:

- [dotnet publish](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish)
- [.NET for Android build process](https://learn.microsoft.com/en-us/dotnet/android/building-apps/build-process)
- [WebAssembly Browser App project](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-10.0)
- [Underlying .NET 10 WebAssembly workload and AOT guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0)

Offline behavior must be honest: desktop builds work offline once the SDK,
workloads, and restored packages are present; Android and browser clients keep
editing and portable syntax coloring, but compiler/build diagnostics and
authoritative builds are unavailable when their selected remote worker cannot
be reached unless the measured local Roslyn composition is installed.

## Architecture and ownership

| Concern | Proposed owner | Boundary |
| --- | --- | --- |
| Code-editor abstraction and neutral visual contracts | `Broiler.UI.CodeEditor` | Defines its own control-facing document/snapshot/span interfaces; no Roslyn, product workspace, filesystem, or platform dependency |
| Standard code-editor rendering and interaction | `Broiler.UI.CodeEditor.Standard` | Consumes those immutable control-facing snapshots; owns no compilation |
| Tree and enhanced tab controls | Broiler.UI abstraction/`.Standard` pairs | General controls with keyboard, focus, theme, and semantic behavior |
| Text buffers and declared workspace state | `Broiler.Code.Workspaces` | Stable IDs, versions, dirty overlays, lossless project data, recovery, and storage capabilities; does not depend on Broiler.UI |
| Portable C# classification | `Broiler.Code.Language.CSharp.Syntax` | Roslyn-free fallback composed into every host |
| C# parsing and semantic services | `Broiler.Code.Language.CSharp.Roslyn` | Optional composition root; Roslyn is not a transitive dependency of Code Core |
| Build request, progress, diagnostic, and artifact contracts | `Broiler.Code.Build` | Platform-neutral, serializable, versioned contracts |
| Trusted project evaluation and SDK/MSBuild execution | `Broiler.Code.Build.Worker` | Separate process or verified remote sandbox; never loaded into the UI process |
| Shared IDE shell and commands | `Broiler.Code.Core` | Explorer, tabs, Problems/Output panes, command state, and build coordination |
| Reusable browser application host | `Broiler.App.WebAssembly` | Canvas host, queued dispatcher, clipboard/text/cursor, and browser resource capabilities |
| Platform capabilities | `Broiler.Code.Windows`, `.Linux`, `.Android`, `.WebAssembly` | Rendering/input host, resource access, dispatcher, and worker transport only |

The Broiler.UI assembly rule applies: `UiCodeEditor` and
`StandardCodeEditor` live in separate abstraction and Standard implementation
assemblies. The same rule applies if TreeView becomes a shared control. CodeEditor
must not subclass RichEdit or parse colored display text. It may reuse extracted
caret, selection, scrolling, IME, visible-line, and token-rendering mechanics from
StandardRichEdit and StandardFormatCodeView.

`Broiler.Code.Core` adapts workspace snapshots to the control-facing CodeEditor
interfaces. This keeps the reusable UI component independent of a product-owned
workspace while keeping the workspace model independent of UI packages.
Workspace buffers are the sole authority for edit transactions, dirty versions,
and undo/redo history. CodeEditor owns caret, selection, scroll, composition, and
other view state; it emits versioned edit intents through its document interface
and renders the accepted snapshot returned by the workspace adapter.

The application split should begin as:

```text
src/Broiler.Code/
src/Broiler.Code.Workspaces/
src/Broiler.Code.Language.CSharp.Syntax/
src/Broiler.Code.Language.CSharp.Roslyn/
src/Broiler.Code.Build/
src/Broiler.Code.Build.Worker/
src/Broiler.App.WebAssembly/
src/Broiler.Code.Windows/
src/Broiler.Code.Linux/
src/Broiler.Code.Android/
src/Broiler.Code.WebAssembly/
```

Names may change during Phase 0, but the dependency direction may not: platform
heads and language/build adapters depend inward on neutral contracts, never the
reverse.

## Core contracts

### Versioned source and workspace

The runtime model is:

```text
CodeWorkspace -> CodeSolution -> CodeProject -> WorkspaceItem
WorkspaceItem -> SourceDocument | ProjectFile | Resource | Content | Configuration
SourceDocument -> TextSnapshot
```

Every item has a stable runtime ID independent of its current path. Text items
also have an immutable monotonic text version, a saved version, encoding/BOM and
line-ending policy, and an external revision token. Project and solution state
are snapshots too. Rename, move, and save operations act on capabilities exposed
by the storage provider, not on assumed local paths.

Source buffers own atomic edit transactions and undo/redo stacks. A transaction
names its base version and either produces one new snapshot or is rejected as
stale; the CodeEditor never mutates an independent copy of the source text.

User session state (open tabs, active document, selection, scroll positions,
expanded tree nodes, pane sizes, and recent workspaces) is separate from the
canonical solution/project files. Stable IDs are persisted in that per-user
state: an IDE rename retains identity; an external rename retains identity only
when the provider exposes a durable file ID or the user confirms the match.
Unknown project XML and unsupported settings survive a byte-identical no-op
round trip; supported structural edits produce minimal diffs. The lossless
declared model is distinct from the trusted worker's target-specific evaluated
MSBuild graph.

### Classifications and diagnostics

Syntax highlighting is a typed collection of classification spans over one
text snapshot. The CodeEditor maps classification kinds to theme tokens; it
does not know C# token kinds or call Roslyn.

One normalized diagnostic model serves the live language service and build
worker. It includes:

- stable diagnostic ID, severity, code, message, origin, and optional help URI;
- solution/project/document IDs and optional related locations;
- zero-based source span plus normalized line/column data;
- target framework/RID, build configuration, and build ID when applicable; and
- workspace and text-snapshot versions.

Diagnostics without a source span remain visible as project/toolchain errors.
Editor squiggles, gutter marks, tooltips, Problems rows, and navigation all
consume the typed diagnostic; no UI parses localized console text. A result may
be applied only when its exact snapshot is still current. Span translation for
older live-analysis results is deferred until a separately tested change-tracking
algorithm exists; authoritative build diagnostics are never rebased.

### Authoritative builds

An `IBuildService`-style contract accepts an immutable granted workspace
snapshot, pinned toolchain selection, target, configuration, restore policy,
and cancellation token. It streams typed progress, log, diagnostic, and
artifact events. Dirty overlays are included so a build cannot silently compile
different content from the editor.

The pre-evaluation snapshot contains every known granted workspace input:
solution/project files, sources, resources/content, manifests, `global.json`,
applicable ancestor `Directory.Build.*`/`Directory.Packages.props` and NuGet
configuration, linked items, known imports/references, and dirty non-code files.
`bin`, `obj`, VCS metadata, and worker caches are excluded by policy. Dynamic
imports, SDK inputs, packages, analyzers, generators, and generated files that
can only be found during trusted evaluation/restore are discovered inside the
worker and added, with origin and hashes, to the final build-input manifest.
Workspace-local discoveries outside a granted root fail with an actionable
capability diagnostic. Traversal, symlink, hardlink, and reparse-point escapes
are rejected at materialization and artifact collection. The worker builds from
a neutral per-job root outside the Broiler checkout so unrelated staging
ancestors cannot influence evaluation.

MSBuild project evaluation and targets can execute code. Even the local worker
runs out of process, and a remote worker runs each job in a disposable sandbox
with no host secrets, bounded CPU/memory/time/storage, explicit network/restore
policy, output limits, cancellation, and cleanup. Broiler Office Server may
serve the WebAssembly client, but its current static-file process must not also
execute user builds.

Opening a workspace does not imply trust. An untrusted workspace opens in a
non-evaluating edit mode that reads project structure without executing MSBuild,
restore, targets, analyzers, or source generators. Evaluation and build require
an explicit, scoped trust decision. Third-party analyzers and generators never
load into the IDE process; when enabled, they execute only inside the worker's
trust boundary.

## Phase 0 - Freeze the product and prove the risky seams

**Owner:** Broiler Code, with Broiler.UI, Android, WebAssembly, release, and
security owners consulted.

**Status: delivered, with two measurements owed.** The architecture document,
budgets, mutation matrix, Broiler.UI ADRs 0021-0024, fixture, and three
harnesses are committed and indexed. What remains open is carried forward below
rather than left inside a closed phase.

**Delivered:**

| Outcome | Where |
| --- | --- |
| Durable architecture record, web target frozen as the standalone `Microsoft.NET.Sdk.WebAssembly` profile | [`docs/architecture/broiler-code.md`](architecture/broiler-code.md) |
| CodeEditor, virtualized text semantics/IME, TreeView, and TabView decisions | Broiler.UI ADRs [0021-0024](../Broiler.UI/docs/adr/README.md) |
| SDK-project mutation matrix | [`docs/architecture/broiler-code-project-mutations.md`](architecture/broiler-code-project-mutations.md) |
| Two-project fixture with Android and browser-WebAssembly variants and a deliberate cross-project error | [`tests/broiler-code-phase0/fixture`](../tests/broiler-code-phase0/fixture/) |
| Snapshot, incremental-classification, and cancellation prototype; portable C# classifier | [`Broiler.Code.Phase0.Prototype`](../tests/broiler-code-phase0/Broiler.Code.Phase0.Prototype/) |
| Trusted out-of-process build worker with six proof scenarios | [`Broiler.Code.Phase0.Worker`](../tests/broiler-code-phase0/Broiler.Code.Phase0.Worker/) |
| Roslyn footprint and evaluated-graph delivery | [`Broiler.Code.Phase0.Roslyn`](../tests/broiler-code-phase0/Broiler.Code.Phase0.Roslyn/), [`payload-probes`](../tests/broiler-code-phase0/payload-probes/) |
| Named traces and frozen budgets with the minimum host matrix | [`docs/architecture/broiler-code-budgets.md`](architecture/broiler-code-budgets.md) |

**Decisions the measurements forced:**

- **Android and browser hosts ship portable classification only** in the first
  release. The fork is closed, not deferred: composing Roslyn there costs
  +1.79 MiB compressed, which is affordable, but device cold start, peak memory,
  and a trimming reflection-safety review are unmeasured, and a semantic claim
  needs all three. Both hosts report classification-only mode between builds.
- **Phase 1's buffer must not copy the document per edit.** A whole-string
  snapshot costs 17.63 ms and 21.4 MB per keystroke on the 10.5 MB fixture that
  Phase 1's own exit gate names, against 0.16 ms to analyse the result. The
  snapshot-update and per-edit allocation budgets are frozen as *not met*, and a
  piece table or rope is a Phase 1 requirement rather than an optimization.
- **The evaluated graph supplies the compile item list**, not just references.
  Deriving sources by scanning directories produced 17 errors where the build
  reports 1.

**Still owed, carried into the phases that need them:**

1. Android device and browser cold-start and peak-memory measurements for the
   semantic service, plus the trimming reflection-safety review. Required before
   any local-semantic claim for those hosts; until then the Phase 6 matrix keeps
   classification-only.
2. CI baselines for every timing budget. The budgets were recorded on one
   developer machine; Phase 1 owes CI figures so the ceilings become a gate
   rather than an inspection.
3. Input-to-present measurement, which needs a view. Phase 1 owns it, measured
   end to end rather than reconstructed by adding buffer and analysis figures.

**Exit gate:** met. The indexed architecture document, Broiler.UI ADRs, mutation
matrix, and fixture are committed; the edit-to-classification prototype rejects
stale work across every trace and in a deterministic completed-then-discarded
check; the out-of-process build maps the fixture's cross-project error back to a
workspace-relative document, a zero-based span, and the SHA-256 of the exact
bytes compiled; and the Android/WebAssembly measurements closed the composition
fork rather than leaving it open. Hosts without the complete semantic stack
report classification-only mode between builds.

## Phase 1 - Deliver the shared CodeEditor

**Owner:** Broiler.UI for the control; Broiler Code for the source-buffer
integration.

**Current evidence:** StandardRichEdit has editing, selection, clipboard, IME,
touch, and undo behavior, while StandardFormatCodeView has monospace token and
visible-line rendering. Neither is a source editor and neither exposes a
collection of versioned diagnostic adornments.

**Next actions:**

1. Add `Broiler.UI.CodeEditor` and `Broiler.UI.CodeEditor.Standard` following the
   abstraction/factory/Standard topology; update `Broiler.UI.slnx`, architecture
   tests, and the component README, then regenerate `Broiler.UI.All` with
   `scripts/gen-metapackages.ps1`.
2. Implement a large-text buffer integration with line indexing, Unicode-safe
   positions, newline preservation, selection/caret, scrolling, workspace-owned
   edit transactions and undo/redo,
   clipboard, IME composition, indentation, tab policy, find, and go-to-line.
   Extend the neutral text-input contract with bounded surrounding-text queries
   so IME requests never materialize a whole multi-megabyte source buffer.
   Migrate StandardEdit, StandardRichEdit, AndroidInputCoordinator,
   BroilerInputConnection, and their tests to the compatible bounded contract,
   with Writer regression gates.
3. Render only visible lines and spans. Add line numbers, current-line state,
   selections, bracket matches, classification colors, diagnostic squiggles,
   gutter marks, and accessible descriptions without creating a child element
   per line or token.
4. Make classification and diagnostic updates asynchronous, cancellable, and
   snapshot checked. Require a real host/UI dispatcher; compiler callbacks must
   not use Writer's immediate cross-thread dispatcher behavior.
5. Add a CodeEditor-specific classification palette mapped from existing theme
   roles, with compatible defaults so existing theme initializers do not break.
   States remain distinguishable in high contrast and never use color alone.
6. Add render, hit-test, input, IME, clipboard, semantics, bidi, high-contrast,
   touch, and fuzz tests. Use a deterministic fixture classifier here; real C#
   classification arrives in Phase 3.
7. Add `UiSemanticRole.CodeEditor` plus a virtualized text-range query/action
   contract so accessibility never requires one whole-document string. Test
   bounded range reads, caret/selection, edits, line navigation, and stale-range
   rejection independently of the later platform bridges.

**Status: delivered, with two measurements owed.** The control, its Standard
implementation, the non-copying buffer, the bounded text-input contract, and the
virtualized accessibility contract are committed and registered.

**Delivered:**

| Outcome | Where |
| --- | --- |
| Control abstraction and Standard implementation, registered in `Broiler.UI.slnx` and `Broiler.UI.All` | `Broiler.UI.CodeEditor`, `Broiler.UI.CodeEditor.Standard` |
| Non-copying text buffer: immutable chunk tree, line index, Unicode-safe caret, transactions, undo grouping, find | [`src/Broiler.Code.Workspaces`](../src/Broiler.Code.Workspaces/) |
| Workspace-to-control adapter and the snapshot-checked analysis controller | [`src/Broiler.Code.Core`](../src/Broiler.Code.Core/) |
| Bounded `IUiTextEditor` metrics and range queries; StandardEdit, StandardRichEdit, and AndroidInputCoordinator migrated | Broiler.UI foundation and Broiler.App.Android |
| `UiSemanticRole.CodeEditor` and `IUiVirtualizedTextProvider` | Broiler.UI foundation |
| Visible-range rendering with line numbers, current line, selection, bracket match, classification, squiggles, and gutter marks | `StandardCodeEditor` |

**What the measurements say:**

- **The Phase 0 buffer budgets are now met.** On the 10.5 MiB fixture a
  keystroke costs 0.0028 ms p95 and allocates 1,272 B, against ceilings of 1 ms
  and 4 KiB — and against Phase 0's whole-string 17.63 ms and 21.4 MB. Cost no
  longer scales with document size.
- **Incremental recolouring holds at scale.** A single-character edit in a
  100,000-line document re-lexes one line, and the renderer queries spans only
  for the lines it paints.
- **Stale results cannot paint**, and a result whose snapshot has been
  superseded is dropped rather than kept and filtered later.

**Still owed, carried forward:**

1. Input-to-present, which needs a host presenting real frames. It keeps its
   16 ms ceiling and belongs to the first phase that runs the editor in a
   platform head.
2. CI baselines for the timing budgets, inherited from Phase 0.
3. Bidi and RTL rendering. The control lays out by monospace column, which is
   correct for the classification and hit-test work Phase 1 owns and is not a
   bidi implementation; it is called out here rather than left implied.

**Exit gate:** met, apart from the two measurements above. The control edits and
incrementally recolours a 100,000-line fixture without rebuilding the document
or rendering off-screen lines; the buffer meets the Phase-0 p95 latency and
allocation budgets on the recorded baseline; stale classification and diagnostic
results cannot paint; bounded IME queries, keyboard behaviour, semantic
snapshots, virtualized accessibility ranges with stale-range rejection, and
touch and clipboard paths are covered by tests. StandardEdit, StandardRichEdit,
and the full Broiler.UI suite remain green after the text-input migration. Real
screen-reader support remains gated on platform accessibility bridges in Phase 7.

## Phase 2 - Add the multi-project workspace and IDE shell

**Owner:** Broiler Code for the workspace and shell; Broiler.UI for reusable tree
and tab capabilities.

**Current evidence:** Writer is single-document and path-oriented. TabView has
no close/dirty/reorder behavior, ListView cannot represent a project hierarchy,
and there is no runtime solution/project model.

**Next actions:**

1. Implement `CodeWorkspace`, `CodeSolution`, `CodeProject`, `WorkspaceItem`,
   `SourceDocument`, immutable snapshots, stable IDs, atomic edit transactions,
   workspace-owned undo/redo, dirty/saved overlays, and capability-based
   asynchronous storage contracts for source and non-source build inputs.
2. Load and preserve standard `.slnx`/`.sln` and SDK-style `.csproj` data through
   a lossless declared-project provider. Keep the repository's
   `eng/solutions.json` generator out of the runtime product. Unsupported dynamic
   constructs are read-only with an explicit diagnostic until trusted worker
   evaluation supplies a target-specific graph.
3. Extend TabView with close requests, dynamic/dirty headers, removal, reorder,
   overflow, focus, and semantics. Add a virtualized `UiTreeView`/Standard pair
   for solution, project, folder, source, reference, and diagnostic nodes.
4. Compose one `Broiler.Code.Core` shell: menu/toolbar, Solution Explorer,
   closable editor tabs, Problems and Output panes, splitters, status/progress,
   and named commands for New/Open/Save/Save All/Build/Rebuild/Cancel.
5. Preserve per-document selection, scroll, undo, and diagnostic state when
   tabs switch. Opening an already-open source activates its existing buffer.
6. Implement Save/Discard/Cancel for a dirty document and for closing an entire
   workspace. Save All reports partial failure and never silently drops text.
7. Add recovery journaling, conflict detection, external revision handling,
   case/path collision rules, traversal protection, and atomic desktop writes
   where the provider supports them.
8. Add a minimal template service for a solution, C# console/library projects,
   source files, and project references. Templates create standard SDK files and
   preserve unsupported user edits rather than introducing Broiler metadata.
9. Add thin Windows and Linux heads referencing Code Core. Register
   `Broiler.Windows.Code.slnx` and `Broiler.Linux.Code.slnx` in
   `eng/solutions.json` with foreign-platform exclusions when the heads land.
   Architecture tests reject copied shell/workspace logic in either head.
10. Give both desktop heads a real UI-thread dispatcher, explicit Broiler.Input
    routing, OS clipboard integration, and native text/IME services. Do not carry
    Writer's immediate dispatcher, in-memory clipboard fallback, or legacy input
    adapter into the Code support claim.
11. Register Code Core/workspace test roots in the generated
    `Broiler.Tests.slnx` definition when those projects land; Phase-2 exit tests
    are part of the normal test closure, not an end-of-roadmap addition.

**Status: all eleven actions delivered.** Covered by 221 tests across the
workspace, the controls, the shell, and the hosts.

Action 4 was delivered late and separately, after the rest of the phase. An
earlier revision of this section claimed all eleven had landed when action 4 had
not: a Solution Explorer data source, a document coordinator, a template
service, and a Problems model all existed and were tested, but **nothing
composed them onto the screen**. Both heads added a bare `StandardCodeEditor` as
their only root and neither opened a workspace, so the editor had no document
and every edit path refused. The running Windows application showed an empty
surface with no menu, no toolbar, and no working input — which is how the gap
was found, by running it rather than by reading the tests.

Every Phase 2 test passed throughout. They drove the coordinator, the tree, and
the tabs directly, so a shell that composed nothing satisfied all of them. The
tests added with the shell assert the assembly instead: that the root element
actually contains the menu, toolbar, explorer, tabs, editor, splitter, and
status line, and that attaching a workspace leaves the editor writable —
`IsReadOnly` true before, false after — which is the precise condition that was
false in the shipped head.

**Delivered:**

| Outcome | Where |
| --- | --- |
| `CodeWorkspace`, `CodeSolution`, `CodeProject`, `WorkspaceItem`, `SourceDocument`, stable IDs independent of path | [`src/Broiler.Code.Workspaces`](../src/Broiler.Code.Workspaces/) |
| Capability-based asynchronous storage, and a desktop provider with atomic writes | `Storage/IWorkspaceStorage.cs`, `Storage/FileSystemWorkspaceStorage.cs` |
| Traversal, alias, reserved-name, and case-collision handling | `Storage/WorkspacePath.cs` |
| Lossless `.csproj` and `.slnx`/`.sln` provider with the mutation matrix in executable form | `Projects/DeclaredProjectFile.cs`, `Projects/DeclaredSolutionFile.cs` |
| Declared-only solution loading — no evaluation, no restore, no analyzers | `WorkspaceLoader.cs` |
| Save/Discard/Cancel, Save All with per-document outcomes, external-change detection | `CodeWorkspace.cs` |
| Recovery journaling outside the workspace, atomic and per-entry tolerant | `Recovery/RecoveryJournal.cs` |
| Virtualized `UiTreeView`/Standard pair with lazy child counts and stable IDs | [`Broiler.UI.TreeView`](../Broiler.UI/src/Abstractions/ValueAndSelection/Broiler.UI.TreeView/), `.Standard` |
| TabView close requests, dirty and dynamic headers, removal, reorder, overflow, focus, semantics | `Broiler.UI.TabView` |
| Solution Explorer data source over the workspace | `src/Broiler.Code.Core/Shell/SolutionExplorerSource.cs` |
| Document coordinator: per-document view state, Save/Discard/Cancel, Save All, journalling | `src/Broiler.Code.Core/Shell/DocumentCoordinator.cs` |
| Template service for solutions, console and library projects, source files, and project references | `src/Broiler.Code.Core/Templates/CodeTemplateService.cs` |
| Composed shell: menu, toolbar, explorer, splitter, tabs, editor, Problems, Output, status | `src/Broiler.Code.Core/Shell/CodeShell.cs` |
| Named commands with three-state availability, so an absent service says so rather than looking broken | `src/Broiler.Code.Core/Shell/CodeCommands.cs` |
| Problems pane grouped by document over the shared tree control | `src/Broiler.Code.Core/Shell/ProblemsTreeSource.cs` |
| Untitled documents, per-document storage grants, and Save As that keeps identity | `src/Broiler.Code.Workspaces/CodeWorkspace.cs` |
| Host-neutral file dialogs, where the dialog result *is* the storage grant | `src/Broiler.Code.Core/Shell/FileDialogs.cs` |
| Startup bootstrap over `IWorkspaceStorage`, testable without a window | `src/Broiler.Code.Core/Shell/WorkspaceBootstrap.cs` |
| Win32 open/save dialogs granting storage rooted at the chosen directory | `src/Broiler.Code.Windows/WindowsFileDialogs.cs` |
| Populated toolbar over a host-supplied button factory, and mnemonics in `AccessKey` | `src/Broiler.Code.Core/Shell/CodeShell.cs`, `Shell/CodeCommands.cs` |
| New Project writing a plain solution and console project, then opening it | `CodeShell.NewProjectAsync` over `Templates/CodeTemplateService.cs` |
| Real UI-thread dispatcher, shared desktop input routing, and a machine-readable support claim | `src/Broiler.Code.Core/Hosting/` |
| Windows head: Direct2D window, Win32 clipboard, IMM32 composition, running the composed shell over an opened workspace | [`src/Broiler.Code.Windows`](../src/Broiler.Code.Windows/) |
| Linux head: X11/OpenGL window, evdev input, zenity/kdialog dialogs, running the composed shell | [`src/Broiler.Code.Linux`](../src/Broiler.Code.Linux/) |
| Shared evdev translation — pointer tracking, key names, characters — testable off Linux | `src/Broiler.Code.Core/Hosting/EvdevInputRouter.cs` |
| `Broiler.Windows.Code.slnx` and `Broiler.Linux.Code.slnx` with foreign-platform exclusions | [`eng/solutions.json`](../eng/solutions.json) |

**What the tests establish:** a no-op save of every project in the Phase 0
fixture is byte-identical; adding a project reference changes exactly one line;
a project holding a `<Choose>` or a custom `<Target>` refuses structural edits
wholesale rather than editing around the part it understood; classic `.sln`
project GUIDs survive a round trip; an item keeps its identity, buffer, and undo
history across a rename; Save All attempts every document and reports each
outcome without dropping text; an external change is detected before it is
overwritten; and unsaved text survives a process that never saved.

**What the control tests establish:** a thousand files across ten collapsed
projects produce ten rows and never enumerate the file lists; expanding one
project adds only its hundred children; a screen reader is handed the twenty
rows on screen out of 1,001, each announcing its level and position within that
level; expansion and selection survive a data-source refresh because they are
keyed by ID; a close is a request the control does not act on; removing a tab
moves focus deterministically and never onto a removed element; and a reorder
keeps the selection on the same tab rather than the same slot.

**What the shell tests establish:** caret and scroll survive a tab switch, so a
single reused editor control does not cost the user their place; opening an
already-open document activates its existing tab and buffer; Cancel on a dirty
close keeps both the tab and the text; Save writes then closes; Discard closes
without writing; Close All stops at the first cancellation rather than leaving a
half-closed workspace; and a templated solution contains no Broiler-specific
metadata anywhere and loads back as a declared workspace with its project
reference intact.

**The default document path, and three defects found by running it.** The
composed shell went out with its File menu still half-wired, and running it
showed three separate failures that all looked like one ("the editor is
read-only"):

| Symptom | Cause | Fix |
| --- | --- | --- |
| Typing did nothing; the window looked read-only | `UiSession` routes keys and characters to `FocusedElement`, falling back to hit-testing the event position. The head set `editor.HasFocus`, which draws a caret but tells the session nothing, so `FocusedElement` stayed null and every keystroke hit-tested to the origin — the menu bar. | The head sets session focus, at startup and on pointer down, via `CodeShell.ResolveFocusTarget` |
| New did nothing | Every document was a file. There was no way to have a buffer with no file behind it. | `CodeWorkspace.CreateUntitledDocument`; `SaveOutcomeKind.NeedsLocation` turns a Save with nowhere to go into a Save As |
| Open and Save As did nothing | Both raised an intent no host handled, and Save As did not exist as a command at all | `IFileDialogService` and `CodeCommandNames.SaveAs`, implemented over comdlg32 in the Windows head |
| The menu bar read "&File" | `UiMenu` draws `Text` verbatim; the mnemonic belongs in `AccessKey`, as `WriterApp` does it | `CodeCommand.AccessKey`, with a test walking the menu tree for stray ampersands |
| The toolbar was empty | It was docked and in the tree, but nothing ever added a child to it | A `CreateButton` factory on `CodeShellControls`, buttons built once and thereafter only updated |
| New Project did not exist | `CodeTemplateService` could scaffold one, but no command reached it | `CodeCommandNames.NewProject`, taking directory and name from one save dialog |

A fourth followed from running it again: the menu bar read **"&File"** and
**"&Build"**. `UiMenu` draws `Text` verbatim and carries the mnemonic in
`AccessKey` — which is how `WriterApp` has always built its menus. This shell
was the only caller putting ampersands in the text. Every command now declares
its mnemonic as `CodeCommand.AccessKey`, and a test walks the whole menu tree
asserting no `Text` contains one.

The toolbar is populated too. `CodeShellControls` gained a `CreateButton`
factory rather than a list of buttons: the shell decides which commands the
toolbar carries — a deliberate subset of the menu, since a toolbar mirroring
every command is a second menu that is harder to read — and the host decides
what a button is, the same split as every other control here. The buttons are
made once and afterwards only relabelled and enabled or disabled; rebuilding
them on each command refresh would replace the element under the pointer
mid-click.

**New Project** creates a solution with one console project and opens it. The
save dialog answers both halves of the question at once — which directory, and
from the file name what to call it — which avoids a text-input dialog this shell
does not have. All the files are written as a single plan, so the
already-exists check covers every one of them before any is created: a
half-written project is worse than none, because it looks like something the
user can open. What lands on disk is a plain `.slnx`, `.csproj`, and
`Program.cs` with no Broiler-specific metadata, asserted by test.

The grant model is what makes Open and Save As work outside the workspace root
without widening it: **the dialog is the grant.** A host returns storage already
scoped to the directory the user picked, the document carries that grant, and a
later save writes back through it. So `Broiler.Code.Core` still names no
filesystem type, a sandboxed provider substitutes cleanly, and two files called
`Program.cs` in two granted directories stay two documents rather than
collapsing into one — item identity is the path *plus* its grant.

Opening with no argument now grants an empty scratch directory under
`LocalApplicationData` and shows an untitled buffer, rather than writing a
sample file into the temp directory. The startup path itself moved out of the
Windows head into `WorkspaceBootstrap` in Core and goes through
`IWorkspaceStorage` rather than `System.IO`, so it is testable without a window
— the head's private copy was untested, which is how it shipped opening a
workspace whose editor had no document.

**What the shell composition tests establish:** the root element contains a
menu, a toolbar, a tree, a tab view, a code editor, a splitter, and a label,
rather than an editor alone; the File menu carries Save and Save All and the
menu bar carries a Build entry; attaching a workspace and opening a document
makes the editor writable and an inserted string appears in its snapshot;
Save All through the command path writes the edited text to disk; Problems
group by document with their severity counts; and Build reports
`Unavailable` with a reason naming the missing build service, so a command
that cannot work says why instead of doing nothing when clicked.

**What the default-document tests establish:** an empty grant still opens
something editable, and it is an untitled buffer named as such in its tab; a
grant holding sources opens one of them instead; `bin` and `obj` are not walked;
New never reuses a name that is still open; a Save on a never-saved document
asks where to put it and then writes there, keeping the same tab, ID, and undo
history; a file opened through a dialog grant saves back to where it came from
and not under the workspace root; two granted files of the same name stay two
documents; cancelling a dialog changes nothing and leaves the document dirty;
Save All asks about untitled documents rather than passing over the work most
likely to be lost; and a host with no dialogs reports Open and Save As as
`Unavailable` with a reason, with the menu entries visible and disabled.

**What the focus tests establish:** setting only `editor.HasFocus` — exactly
what the head did — leaves `FocusedElement` null and delivers not one character;
focusing through the session delivers typed text; a hit inside the editor or the
explorer resolves to that pane; and a hit on the toolbar or menu resolves to
nothing, so pressing a toolbar button does not take the caret out of the
document it is meant to act on.

**What the evdev tests establish:** the pointer starts centred and accumulates
relative motion, and is clamped to the viewport rather than walking off the
window where every hit test would miss; a move carries the tracked position, not
the delta, and a button press carries it too, because evdev button events have
no position at all; shrinking the window clamps the pointer instead of
recentring it, which would move the cursor out from under the user's hand; an
absolute position from X11 wins over accumulated motion and ignores sub-pixel
noise; `KeyA` and `Digit7` normalise to the names the controls switch on; a key
produces the character a US layout would, shifted or not; and Ctrl+S produces a
key with no text, a key release produces no text, and an arrow key produces no
text — otherwise a shortcut would type a letter, or every character would be
typed twice.

**What the chrome tests establish:** no menu text anywhere in the tree carries
an ampersand, while File, Build, and Save keep their access keys; the toolbar
carries buttons bound to commands, each labelled and wide enough to read; a
toolbar button runs the same command the menu does and follows the same
availability, so Build is disabled with no build service and Save turns on when
a document opens; and New Project writes a solution, project, and `Program.cs`
containing no Broiler-specific metadata and then opens them — while refusing a
name that is not an identifier, and refusing to overwrite an existing solution
without leaving a partial tree behind.

**The desktop heads, and what the Linux one does not have.** Each head declares
its four services as a value — `HostServiceReport` — and a test asserts the
claim rather than trusting a comment. `--services` prints it and exits non-zero
if any service is a *substitute*, which is the specific thing the roadmap
forbids: Writer's immediate dispatcher, its in-memory clipboard fallback, and
the legacy input adapter are all substitutes that behave plausibly without doing
the real thing.

| Service | Windows | Linux |
| --- | --- | --- |
| UI-thread dispatcher | Native — queued, drained on the message-loop thread | Native — drained on the event-loop thread |
| Input routing | Native — explicit Broiler.Input with device identity and a monotonic sequence | Native — same, over evdev |
| Clipboard | Native — Win32, no in-memory fallback | **Unavailable** — X11 selection ownership is not implemented |
| Text input and IME | Native — IMM32 composition, candidates placed at the caret | **Unavailable** — no XIM, ibus, or Wayland backend exists; US-layout typing only |
| File dialogs | Native — the common dialogs, through comdlg32 | Native where zenity or kdialog is installed; **Unavailable** otherwise |

The two Linux gaps are structural rather than unfinished wiring. Its graphics
stack is an X11 surface driven by a polling loop and its input arrives through
evdev — raw device events, not X11 key events — and an input method speaks XIM,
ibus, or the Wayland text-input protocol. None of those are reachable from
evdev, and the only `ITextInputProvider` in the repository is Android's. The
head therefore reports both as unavailable and disables the affected commands,
rather than substituting an in-process buffer that would make copy and paste
appear to work while interoperating with nothing.

Closing them means adding a Linux text-input backend and X11 selection
ownership, which is Broiler.Input and Broiler.Graphics work rather than the
head's.

**The Linux head now runs the shell.** It opens an X11/OpenGL window through
`Broiler.Graphics.Linux.OpenGL`, composes the same shell from the same
abstractions, and drives it from a loop that pumps X11 events, drains input,
drains the dispatcher, and renders — in that order. There is no message loop to
be called back from on this platform, so that loop *is* the UI thread, and
everything the analysis layers post is executed by it rather than on the thread
that produced it.

Input is evdev, which is not a windowing input stack: it reports what a device
did, with no notion of a window, a cursor position, or a character. Three things
therefore have to happen that Windows gets from the OS — relative motion is
accumulated into a viewport-clamped absolute position, key names are normalised
to what the controls switch on, and characters are derived from keys. That is
`EvdevInputRouter`, in Core rather than the head, so it is testable on any
operating system; the head keeps only the device opening. Because evdev is a
global device stream with no window scoping, devices are read only while the X11
window has focus — otherwise typing into another application would also type
here.

Deriving characters from keys is a US-layout mapping, not an input method. It
covers unmodified and shifted Latin typing and refuses anything held with
Control or Alt, which are shortcuts. Everything past that needs a real IME,
which is why the head still reports text input as **unavailable** rather than
claiming this is one.

**File dialogs** come from the desktop's own helper — zenity, or kdialog on KDE
— run as a subprocess. There is no toolkit-neutral file chooser on Linux, and
this head deliberately takes no GTK or Qt dependency to get one; running the
helper the desktop already ships means the user sees their real chooser with
their bookmarks, not an imitation. Where neither is installed there is no
dialog, and Open, Save As, and New Project report themselves unavailable with a
reason — which is the machinery the shell already had, now exercised by a head
that genuinely lacks the service. `file dialogs` joined `HostServiceReport` for
exactly that reason: a head without it cannot run those commands, so it belongs
in the claim rather than in an implementation detail.

Clipboard and IME remain unavailable, unchanged and for the reasons above.

**What could not be verified here.** This work was done on Windows. The Linux
head builds clean and its `--services` claim runs and prints correctly, because
that path is pure managed code — but the window, the X11 surface, and the evdev
devices have not been exercised on a Linux display from this machine. The
translation logic they depend on is tested; the platform wiring around it is
not. It needs a run on a Linux desktop before the exit gate can be called met
there.

**Exit gate:** met on Windows. A two-project fixture can be opened, edited,
renamed, saved, closed, recovered, and reopened without losing stable identity,
undo history, or unknown project data; a no-op project save is byte-identical
and supported structural edits produce minimal diffs; dirty-close, partial-save,
external-change, traversal, and case-collision tests pass; a 1,000-item tree
renders by visible range and is keyboard accessible; both generated desktop
solution closures verify and build. Dispatcher and input-routing tests pass for
both heads and no head carries any of Writer's three substitutes.

Three clauses remain open. The Linux clipboard and IME integration tests cannot
pass until the backends above exist. The gate's requirement that a newly
templated solution build its declared graph is covered here by a load-back test
rather than by a reference CLI build — the templated output is asserted to
contain only standard SDK files and to reload as a declared workspace with its
project reference intact, which is not the same as having compiled it. And the
Linux head's window has not been run on a Linux display: it builds clean and its
`--services` claim executes, but the X11 surface, the render loop, and the evdev
devices have only been reasoned about, not exercised.

## Phase 3 - Add C# language services and error highlighting

**Owner:** `Broiler.Code.Language.CSharp.Syntax` and
`Broiler.Code.Language.CSharp.Roslyn`, with project evaluation owned by the
worker and CodeEditor integration owned by Broiler Code Core.

**Current evidence:** Roslyn is present only in specialized JavaScript scripting
and source-generator projects; there is no reusable C# workspace, classifier,
or source-located diagnostic service.

**Next actions:**

1. Pin one reviewed Roslyn toolset compatible with the chosen .NET SDK and keep
   all Roslyn types behind the optional language-service boundary. Report the
   language-service compiler identity alongside the selected SDK so mismatches
   with authoritative build diagnostics are visible.
2. Add trusted, out-of-process design-time project evaluation that honors the
   workspace's `global.json`, target framework, imports, ancestor configuration,
   and granted external roots and returns a serializable evaluated graph. It
   runs no analyzer/generator in the IDE process and leaves untrusted workspaces
   in declared, non-evaluating mode.
3. Incrementally parse changed documents and publish C# classifications,
   brace/bracket pairs, syntax diagnostics, and cross-file semantic diagnostics
   for the exact workspace snapshot.
4. Resolve project references, framework references, compiler options, defines,
   nullable context, generated inputs, and target framework through the project
   model. Surface an explicit unsupported-project diagnostic instead of silently
   guessing when evaluation data is unavailable.
5. Merge live and build diagnostics by origin/build ID without letting an older
   build replace newer live results.
6. Add Problems filtering/grouping, severity counts, gutter markers, squiggles,
   accessible tooltips/descriptions, and click/keyboard navigation to source and
   related locations.
7. Compose the portable C# classifier when a host cannot load the full semantic
   service; expose classification-only-between-builds mode in the status UI and
   keep Roslyn out of that host's transitive closure.
8. Keep third-party analyzer and source-generator execution out of the client
   language service. Consume worker-produced generated sources/diagnostics only
   when their build snapshot and trust policy match the current workspace.
9. Register portable-classifier, Roslyn-service, evaluated-graph, and diagnostic
   integration tests in the appropriate component/root test solutions when each
   project lands.

**Status: delivered.** Covered by 91 tests across the two language components
and the diagnostics model.

**Delivered:**

| Outcome | Where |
| --- | --- |
| Portable Roslyn-free classifier, promoted from the Phase 0 prototype | [`src/Broiler.Code.Language.CSharp.Syntax`](../src/Broiler.Code.Language.CSharp.Syntax/) |
| Optional semantic service behind its own boundary, reporting compiler identity | [`src/Broiler.Code.Language.CSharp.Roslyn`](../src/Broiler.Code.Language.CSharp.Roslyn/) |
| Out-of-process design-time evaluation, trust-gated, returning a serializable graph | `DesignTimeEvaluator.cs`, `EvaluatedProjectGraph.cs` |
| Live/build diagnostic merge by origin and snapshot version | `src/Broiler.Code.Core/Diagnostics/DiagnosticMerge.cs` |
| Problems filtering, grouping, severity counts, and accessible navigation | `src/Broiler.Code.Core/Diagnostics/ProblemsModel.cs` |

**What the tests establish:** editing `ReportFormatter.cs` as an unsaved overlay
removes the CS7036 in `ReportRunner.Broken.cs` without that file being touched;
the same source with and without `LEGACY_TARGET` produces different errors; an
untrusted workspace evaluates nothing and reports why; a document conditioned
out of the compile set says so rather than reporting no errors; a build that
started three edits ago cannot resurrect diagnostics the user has fixed; and a
project-level diagnostic with no span survives a stale build, so "the SDK is
missing" does not vanish because the user typed.

**Two things worth stating plainly.** Roslyn is pinned at 5.3.0 while the SDK
compiles with 5.6.0, so the service and the authoritative build do not share a
compiler build; `CSharpLanguageService.CompilerIdentity` is reported next to the
resolved SDK so a live diagnostic the build does not produce has a visible cause.
And build diagnostics are never rebased onto a newer snapshot — a stale build
contributes only its span-less entries, because span translation is deferred
until a separately tested change-tracking algorithm exists.

**Exit gate:** met, with one measurement carried forward. Edits in one file
update cross-file diagnostics in another; error, warning, information, and
project-only diagnostics render and navigate; target-specific defines produce
target-specific results; cancellation is observed before any work and a
superseded result cannot overwrite a newer snapshot. The clause about the
committed analysis trace meeting the Phase-0 budgets is **not** re-measured
here: the traces run against the portable classifier, whose incrementality is
re-proved on the real buffer, but no committed trace yet exercises the Roslyn
service under the latency budget. That belongs with the input-to-present
measurement Phase 1 also owes, and both need a host presenting real frames.

## Phase 4 - Build ordinary .NET projects from the IDE

**Owner:** `Broiler.Code.Build` and `Broiler.Code.Build.Worker`.

**Current evidence:** the repository invokes the CLI directly and has no build
request protocol, toolchain discovery, structured logger, cancellation broker,
or artifact manifest.

**Next actions:**

1. Implement serializable build request, capability, progress, log, diagnostic,
   and artifact contracts, including workspace/build IDs and target identity.
2. Add desktop worker discovery and preflight for the selected SDK, workloads,
   reference packs, NuGet configuration, and target capabilities. Honor the
   workspace `global.json`, roll-forward policy, workload versions, and compiler
   identity; do not silently substitute the IDE's SDK.
3. Compute and materialize one immutable granted workspace snapshot per job in
   a neutral root outside the Broiler checkout so unrelated staging ancestors
   cannot influence evaluation. Include dirty project/configuration files and
   non-code assets. During trusted evaluation/restore, record every resolved SDK,
   import, project/package reference, analyzer/generator, and generated input in
   a final hash manifest; reject ungranted workspace-local discoveries and path,
   link, or reparse-point escapes.
4. Require explicit workspace trust before evaluation, restore, analyzers,
   generators, targets, or build; retain syntax-only editing when trust is
   absent or revoked.
5. Evaluate and build SDK projects in a trusted out-of-process worker. Use a
   small structured MSBuild logger or equivalent event adapter; console text
   remains display-only. The process boundary protects IDE reliability, not the
   user's account from a trusted project's targets.
6. Add explicit restore modes, deterministic environment capture, progress,
   cancellation/kill, time/resource/output limits, cleanup, and retry rules.
   Cancellation terminates the entire process tree within the frozen bound; a
   job is never automatically retried after project code may have executed.
7. Wire Build/Rebuild/Cancel and Problems/Output UI state. The UI names the
   exact snapshot and target being built while editing continues.
8. Verify artifact existence, kind, size, SHA-256, containment, link/archive
   safety, and signing state before the worker reports success. Never run a newly
   built output automatically in this phase.
9. Register build-contract, worker, cancellation, containment, and structured
   diagnostic tests in `Broiler.Tests.slnx` when the worker lands.

**Exit gate:** the deliberately broken multi-file .NET fixture produces
structured, navigable diagnostics against its unsaved snapshot; the corrected
fixture produces verified library/application artifacts; cancellation leaves no
process descendant or staging directory behind within the frozen bound; a
target that spawns descendants is terminated and not retried, while hostile
input paths cannot escape the materialization/artifact roots; dirty project
files and non-code assets affect the output; missing pinned SDK/restore failures
appear as toolchain/project diagnostics; and the UI remains responsive during
all jobs.

## Phase 5 - Add Android and WebAssembly build targets

**Owner:** build worker, Android, WebAssembly, and release owners.

**Current evidence:** the repository already publishes Android APK/AAB outputs
and a trimmed browser-WebAssembly bundle, but those paths are product/CI scripts,
not target profiles available through an IDE build contract.

**Next actions:**

1. Add target profiles that translate the selected project/configuration into
   separate `dotnet build`/`dotnet publish` jobs without leaking worker-global
   RIDs, output paths, target frameworks, or publish properties into referenced
   projects.
2. For Android, detect the .NET Android workload, JDK, Android SDK/API/build
   tools, target/minimum API, and evaluated ABI settings. Respect the project or
   publish profile for arbitrary projects. Broiler-provided templates may use a
   project-local ABI property like the existing Broiler heads, but the worker
   never injects `RuntimeIdentifiers` globally across the graph.
3. Produce and verify a directly installable Debug APK with
   `EmbedAssembliesIntoApk=true` in the fixture plus an unsigned Release AAB.
   Inspect archive contents and classify signing state before claiming success.
   Keep store credentials and production signing outside the initial IDE
   milestone; a later signing provider must use an external secret boundary and
   verify the signing certificate.
4. For the standalone `Microsoft.NET.Sdk.WebAssembly` profile, detect
   `wasm-tools` and Python where the worker platform requires it. Respect
   project/publish-profile trimming, AOT, and output settings instead of forcing
   global CLI values. The fixture defines explicit trimmed interpreted and AOT
   profiles; report payload size and publish mode in the artifact manifest.
5. Serve WebAssembly preview artifacts from a fresh random loopback origin,
   contained artifact root, validated Host header, and deny-by-default CORS/CSP.
   The preview has no cookies, ambient/build credentials, or access to worker
   APIs; sandbox embedding and service-worker cleanup prevent a prior build from
   retaining authority. Test DNS rebinding, path/link escape, and hostile active
   content before enabling broader preview permissions.
6. Add target-scoped diagnostics and prerequisite remediation for missing
   workloads/SDKs rather than presenting every failure as a source error.
7. Extend the template service with one standard .NET-for-Android project and
   one standalone `Microsoft.NET.Sdk.WebAssembly` browser project whose target
   settings live in their project or publish profiles.
8. Add Android archive inspection and emulator install/launch smoke plus
   Chromium and Firefox WebAssembly boot smoke for both fixture profiles in CI.

**Exit gate:** the same multi-file fixture builds ordinary .NET output, a
verified APK/AAB set, and a verified WebAssembly site from explicit target
profiles. The APK installs/launches on the supported emulator, the WebAssembly
site boots in interpreted and AOT modes in the supported browser matrix, the
preview-origin hardening tests pass, intentional source errors navigate to the
same snapshot on all three targets, and missing-toolchain cases (including
Python where required) fail before expensive work with actionable diagnostics.

## Phase 6 - Ship Android and browser IDE hosts

**Owner:** Broiler Code Core plus Android, WebAssembly, storage, and build-service
owners.

**Current evidence:** both hosts can render and edit Writer content, but Android
persists one SAF URI and WebAssembly opens/downloads one file. Neither can
execute an installed desktop SDK toolchain.

**Next actions:**

1. Define an authenticated, versioned remote Build Worker API with TLS, worker
   identity verification, explicit disclosure/consent before source upload,
   incremental content-addressed transfer, job progress/cancellation, artifact
   download, and capability discovery. Local and remote workers implement the
   same build contracts.
2. Run every remote job in an ephemeral sandbox with tenant isolation, no
   inherited secrets, quotas, restore/network policy, retention limits, audit
   records, redacted logs, and cleanup verification. NuGet credentials use a
   dedicated secret broker; plaintext credentials from workspace configuration
   or signing properties are never uploaded.
3. Cache only content whose identity and tenancy are proven. Never reuse an
   untrusted build directory or return another workspace's diagnostics/artifacts.
4. Add Android and WebAssembly heads that reference `Broiler.Code.Core`; forbid
   duplicated shell/workspace/build logic in platform heads with architecture
   tests. Register `Broiler.Android.Code.slnx` and
   `Broiler.WebAssembly.Code.slnx` in `eng/solutions.json` with foreign-platform
   exclusions when each head lands.
5. Reuse `Broiler.App.Android` and add persisted document-tree access,
   capability-aware enumeration/create/rename/delete, an app-private recovery
   mirror, compact layout, lifecycle recovery, and no broad storage permission.
6. Extract `Broiler.App.WebAssembly` from the existing Writer/browser Canvas
   host patterns, then add browser directory handles where supported, an
   origin-private recovery store, and whole-workspace import/export fallback.
   Use chunked/binary transfer; do not base64-copy every source file through the
   current single-file bridge.
7. Make connection, reduced language-service mode, build location, offline
   limitation, upload consent, retry, cancellation, retention, and artifact
   download state explicit in the IDE.
8. Add the Code WebAssembly client to a static host only after preserving the
   build-worker process boundary; do not add arbitrary build execution to BOSS.
9. Validate the Android IDE on physical hardware: SAF tree permission and
   revocation, process death/recovery, rotation, hardware keyboard, touch/stylus,
   and real IME candidate placement.

**Exit gate:** a user can import/open the same compatible two-project fixture,
edit multiple files, recover unsaved state, request each target build, navigate
diagnostics, and retrieve artifacts from Windows, Linux, Android, and supported
browsers. TLS/worker identity, source-consent, credential-redaction, permission
revocation, offline mode, disconnect/reconnect, cancellation, quota, malformed
archive, retention, and cross-tenant isolation tests pass; the physical Android
device checks pass; every platform head uses the same Core application; and all
four generated solution closures pass verification.

## Phase 7 - Stabilize a preview

**Owner:** Broiler Code, component owners, release, accessibility, and security.

**Current evidence:** component-level input/rendering and platform packaging
tests exist, but none cover an IDE-scale workspace, untrusted build graph, or
cross-platform Code workflow.

**Next actions:**

1. Add buffer/property fuzzing, workspace interruption/recovery tests, compiler
   cancellation races, malformed project/archive tests, worker crash recovery,
   and long-running edit/build soak tests.
2. Record cold start, payload, memory, large-file/large-solution analysis,
   keystroke latency, build startup, incremental build, artifact transfer, and
   recovery measurements on every supported host.
3. Implement and validate the missing platform accessibility bridges (Windows
   UIA, Linux accessibility, Android, and browser semantics) before making a
   screen-reader claim. Then validate keyboard-only operation, named screen
   readers, IME candidate placement/composition, bidi/RTL, high contrast, text
   scaling, reduced motion, touch, stylus, and Android insets/rotation on the
   declared matrix.
4. Threat-model project evaluation, MSBuild tasks, analyzers/generators, NuGet
   restore, symlinks/traversal, archive extraction, preview origins, worker API,
   logs, artifacts, caches, and signing boundaries. Complete penetration and
   dependency/license review for the shipped graph.
5. Audit that every earlier-phase test root and platform smoke suite is present
   in the appropriate generated solution and CI workflow; verify the platform
   solutions registered when their heads landed and their forbidden
   foreign-platform patterns.
6. Publish templates and a tutorial for one ordinary .NET app, one Android app,
   and one browser-WebAssembly app, including exact prerequisites and support
   limitations.

**Exit gate:** the declared host/target matrix passes functional, artifact,
performance, recovery, accessibility, isolation, security, dependency/license,
and soak gates from clean environments. The preview documentation accurately
distinguishes local versus remote diagnostics/builds and debug versus
production-signable artifacts.

## Milestones

| Milestone | Included phases | User-visible outcome |
| --- | --- | --- |
| M0 - Editor prototype | 0-1 | Shared, classification-capable CodeEditor driven by a deterministic fixture classifier |
| M1 - Project editor | 2-3 | Multi-project C# editing, dirty/recovery behavior, and live cross-file errors |
| M2 - Desktop compiler | 4 | Responsive Windows/Linux IDE with authoritative local .NET builds |
| M3 - Cross-target compiler | 5 | Verified Android APK/AAB and WebAssembly publish artifacts from the same workspace |
| M4 - Host parity | 6 | Android and browser IDE clients using the same Core and verified remote build-worker boundary |
| Preview | 7 | Evidence-backed support statement and reproducible release artifacts |

## Explicitly deferred

The first preview does not promise debugging, test discovery/running, Git UI,
NuGet package management UI, refactoring, completion, go-to-definition, rename,
designer surfaces, collaboration, extensions, additional programming languages,
store submission, production signing-key custody, or a full Android/.NET SDK
installed inside the Android/browser client. Completion, navigation, refactoring,
debugging, and test running are natural follow-on phases after the editor,
workspace, diagnostic, and build contracts have stabilized.

After Phase 0, `docs/architecture/broiler-code.md` is the source of truth for
durable capability and ownership decisions. Reconcile this roadmap to those
decisions and keep it focused on unfinished outcomes and objective exit gates as
phases complete.

## Repository references

- [Broiler Code architecture](architecture/broiler-code.md) — source of truth after Phase 0
- [Broiler Code budgets and support matrix](architecture/broiler-code-budgets.md)
- [SDK-project mutation matrix](architecture/broiler-code-project-mutations.md)
- [Phase 0 harnesses, fixture, traces, and baselines](../tests/broiler-code-phase0/)
- [Root roadmap](ROADMAP.md)
- [Documentation rules](README.md#documentation-rules)
- [Android application architecture](architecture/android.md)
- [Broiler.UI roadmap](../Broiler.UI/docs/roadmap.md)
- [Broiler.UI control ownership ADR](../Broiler.UI/docs/adr/0001-ui-root-and-per-type-assembly-rule.md)
- [Writer application core](../src/Broiler.Writer/WriterApp.cs)
- [Writer Android host](../src/Broiler.Writer.Android/MainActivity.cs)
- [Writer WebAssembly host](../src/Broiler.Writer.WebAssembly/README.md)
- [Generated solution manifest](../eng/solutions.json)
