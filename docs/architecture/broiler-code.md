# Broiler Code architecture

- **Status:** Approved for Phase 0
- **Owner:** Broiler Code, with Broiler.UI, Android, WebAssembly, release, and
  security owners consulted
- **Recorded:** 2026-08-05

After Phase 0 this document is the source of truth for Broiler Code's durable
capability and ownership decisions. The
[Broiler Code roadmap](../broiler-code-roadmap.md) keeps the unfinished outcomes
and their exit gates and is reconciled to the decisions recorded here.

Companion records:

- [Performance budgets and support matrix](broiler-code-budgets.md)
- [SDK-project mutation matrix](broiler-code-project-mutations.md)
- Broiler.UI ADRs [0021](../../Broiler.UI/docs/adr/0021-code-editor-control-family.md),
  [0022](../../Broiler.UI/docs/adr/0022-virtualized-text-semantics-and-ime.md),
  [0023](../../Broiler.UI/docs/adr/0023-tree-view-control.md), and
  [0024](../../Broiler.UI/docs/adr/0024-tab-view-document-behavior.md)
- Phase 0 harnesses and recorded baselines:
  [`tests/broiler-code-phase0`](../../tests/broiler-code-phase0/)

## Scope

Broiler Code is the development counterpart of Broiler Writer: one
platform-neutral application rendered with Broiler.UI, with thin Windows, Linux,
Android, and WebAssembly hosts.

The first release's language scope is **C# on .NET 10 using SDK-style
projects**. The editor, workspace, diagnostic, and build contracts stay
language-neutral so a second language does not require rewriting them, but a
plug-in language marketplace and a general multi-language IDE are not in scope.

Standard `.slnx`, `.sln`, and `.csproj` files are canonical. Broiler Code does
not invent a second project format, and does not write Broiler-specific metadata
into a user's project.

### Web target

The web target is frozen as the repository's **standalone
`Microsoft.NET.Sdk.WebAssembly` profile**, not an implied Blazor project system.
This applies in both directions: the browser IDE host is built that way, and the
browser applications Broiler Code builds for a user are that kind of project.
The fixture's `SampleReports.Web` is the reference shape, with trimming and AOT
declared by publish profiles rather than forced by the worker.

## Capability boundary

"Runs Broiler Code" and "builds a target" are separate capabilities and are
presented separately in the product. The matrix is in the
[budgets document](broiler-code-budgets.md#minimum-supported-host-matrix); the
decisions behind it are here.

A host reports **classification-only mode** whenever it has the portable
classifier but not the complete semantic stack. This is a visible state in the
status UI, not an absence of one. Between builds, such a host shows syntax
colouring and the diagnostics from the last authoritative build, and says so.

Offline behaviour is stated honestly: desktop builds work offline once the SDK,
workloads, and restored packages are present; Android and browser clients keep
editing and portable colouring, but compiler diagnostics and authoritative
builds are unavailable when the selected worker cannot be reached.

### The Android and browser fork, decided

Phase 0's instruction was to decide local Roslyn plus evaluated-graph delivery
versus portable-classification-only, without leaving an open architecture fork.

**Decision: portable classification only on Android and browser hosts, for the
first release.** Not because the payload is impossible — it is smaller than
expected — but because two of the three gating measurements have not been taken,
and a semantic claim rests on all three.

What was measured:

| Finding | Value |
| --- | --- |
| Composing the Roslyn syntax service into a trimmed browser payload | +7.90 MiB raw, +1.79 MiB compressed |
| The same, excluding localized message resources | +1.76 MiB raw, +0.46 MiB compressed |
| Share of the Roslyn payload that is localized message text | 57% raw, 50% compressed, in 26 satellite assemblies |
| Evaluated graph for a two-project solution | 168 metadata references, 5.8 MiB |
| Local semantic service, fed that graph, versus the authoritative build | Identical diagnostic identity — same rule, document, and span |

What was not measured: cold start and peak memory on a real Android device or
in a browser, and the reflection-safety review that a trimmed Roslyn needs. Both
are recorded as owed in the budgets document. Until they exist, composing local
semantic diagnostics into those hosts would be a claim about a library rather
than about diagnostics a user can trust.

The satellite-assembly finding is worth stating plainly because it changes the
shape of the eventual decision: over half the cost of putting Roslyn in a
browser is message text that the diagnostic contract already treats as
display-only. A host that accepts English-only messages pays roughly a quarter
of the payload. That option is left open deliberately.

## Ownership

| Concern | Owner | Boundary |
| --- | --- | --- |
| Code-editor abstraction and neutral visual contracts | `Broiler.UI.CodeEditor` | Its own control-facing document, snapshot, and span interfaces; no Roslyn, workspace, filesystem, or platform dependency |
| Standard code-editor rendering and interaction | `Broiler.UI.CodeEditor.Standard` | Consumes immutable control-facing snapshots; owns no compilation |
| Tree and enhanced tab controls | Broiler.UI abstraction/`.Standard` pairs | General controls with keyboard, focus, theme, and semantic behaviour |
| Text buffers and declared workspace state | `Broiler.Code.Workspaces` | Stable IDs, versions, dirty overlays, lossless project data, recovery, storage capabilities; no Broiler.UI dependency |
| Portable C# classification | `Broiler.Code.Language.CSharp.Syntax` | Roslyn-free, composed into every host |
| C# parsing and semantic services | `Broiler.Code.Language.CSharp.Roslyn` | Optional composition root; Roslyn is never a transitive dependency of Code Core |
| Build request, progress, diagnostic, artifact contracts | `Broiler.Code.Build` | Platform-neutral, serializable, versioned |
| Trusted evaluation and SDK/MSBuild execution | `Broiler.Code.Build.Worker` | Separate process or verified remote sandbox; never loaded into the UI process |
| Shared IDE shell and commands | `Broiler.Code.Core` | Explorer, tabs, Problems and Output panes, command state, build coordination |
| Reusable browser application host | `Broiler.App.WebAssembly` | Canvas host, queued dispatcher, clipboard, text, cursor, browser resources |
| Platform capabilities | `Broiler.Code.Windows`, `.Linux`, `.Android`, `.WebAssembly` | Rendering and input host, resource access, dispatcher, worker transport only |

The dependency direction is fixed: platform heads and language/build adapters
depend inward on neutral contracts, never the reverse.

`Broiler.Code.Core` adapts workspace snapshots to the control-facing CodeEditor
interfaces. This keeps the reusable control independent of a product-owned
workspace while keeping the workspace independent of UI packages. Workspace
buffers are the sole authority for edit transactions, dirty versions, and
undo/redo history. CodeEditor owns caret, selection, scroll, and composition
state; it emits versioned edit intents and renders the accepted snapshot the
adapter returns.

## Versioned source and workspace

```text
CodeWorkspace -> CodeSolution -> CodeProject -> WorkspaceItem
WorkspaceItem -> SourceDocument | ProjectFile | Resource | Content | Configuration
SourceDocument -> TextSnapshot
```

Every item has a stable runtime ID independent of its current path. Text items
also carry an immutable monotonic text version, a saved version, encoding/BOM
and line-ending policy, and an external revision token. Project and solution
state are snapshots too. Rename, move, and save act on capabilities the storage
provider exposes, never on assumed local paths.

### Edit transactions

A transaction names the version it was composed against. The buffer either
produces exactly one successor snapshot or **rejects it as stale**. It never
rebases silently, because a silent rebase turns a race into a wrong edit at a
plausible-looking position.

Undo and redo produce ordinary successor snapshots with new versions rather than
restoring an old snapshot object. An analysis result computed before an undo can
therefore never reattach after it — snapshot identity, not text equality, is
what decides.

### Buffer representation

Phase 0 measured a whole-string snapshot: 17.63 ms and 21.4 MB allocated per
keystroke on a 10.5 MB document, against 0.16 ms to analyse it. **Phase 1 must
deliver a non-copying representation** — a piece table or rope over shared
immutable chunks. This is recorded as an architectural requirement rather than a
performance note because the measured gap is two orders of magnitude and does
not close with tuning.

### Session state

Open tabs, active document, selection, scroll positions, expanded tree nodes,
pane sizes, and recent workspaces are per-user state, separate from the
canonical solution and project files. Stable IDs are persisted there: an IDE
rename retains identity; an external rename retains identity only when the
provider exposes a durable file ID or the user confirms the match.

Unknown project XML and unsupported settings survive a byte-identical no-op
round trip; supported structural edits produce minimal diffs. The
[mutation matrix](broiler-code-project-mutations.md) is the authority on which
constructs are which.

## Classification

Syntax highlighting is a typed collection of classification spans over one text
snapshot. The CodeEditor maps classification kinds to theme tokens; it does not
know C# token kinds and does not call a language service.

The vocabulary is language-neutral — comment, documentation comment, keyword,
control keyword, preprocessor keyword and text, string, escape, character,
numeric, operator, punctuation. Identifiers get no span and are painted in the
default foreground, because distinguishing a type from a local requires binding.

Classifications are stored per line with **line-relative offsets**. An edit on
one line leaves every other line's spans valid without rewriting them, which is
what makes reuse a reference copy rather than a shift over hundreds of thousands
of spans. Each line also records the lexer state it started in, so incremental
work stops as soon as that state reconverges.

Measured on a 100,000-line file: a single-character edit relexes **1 line**;
opening a block comment relexes until the next `*/` closes it, 35 lines in the
fixture; an unterminated raw string that can never reconverge relexes the whole
document, and even then costs 6.9 ms.

The portable classifier's approximations are deliberate and documented in the
implementation: a conservative contextual-keyword set, interpolation holes
classified as part of their string, and `#if` regions not dimmed because which
branch is live depends on the evaluated graph's defines. Where the semantic
service disagrees, it is authoritative.

### Concurrency

The classifier holds no cross-call state. Cancellation is cooperative, so a
superseded run keeps executing until it next checks its token, and two runs
overlap routinely; a scratch buffer on the instance is shared between them and
corrupts both. Phase 0 hit exactly this.

## Diagnostics

One normalized diagnostic model serves the live language service and the build
worker. It carries a stable ID, severity, code, origin, and optional help URI;
solution, project, and document identity; a zero-based source span with
normalized line and column; the target framework, RID, configuration, and build
ID where applicable; and the workspace and text-snapshot versions.

**Identity is rule ID, document, and span. It is never the message.** Messages
are localized and exist for display only. This is not a stylistic preference:
the machine that recorded the Phase 0 baselines emits German build output, and
the worker's locale-independence proof shows the same build under English and
Japanese producing byte-identical structured diagnostics and different message
strings.

Diagnostics without a source span stay visible as project or toolchain errors.
Editor squiggles, gutter marks, tooltips, Problems rows, and navigation all
consume the typed diagnostic, and **no UI parses localized console text**.

A result may be applied only while its exact snapshot is current. Span
translation for older live-analysis results is deferred until a separately
tested change-tracking algorithm exists; authoritative build diagnostics are
never rebased.

### The structured channel

The worker obtains compiler diagnostics from the compiler's **SARIF 2.1 error
log**, which carries rule ID, severity, file URI, and one-based region as
structured fields with only the message translated. Two implementation facts
were established in Phase 0 and are recorded because both fail silently:

- The comma in the `ErrorLog` property value must be MSBuild-escaped as `%2C`.
  Passed literally, the version selector is dropped and the compiler writes
  SARIF 1.0.0, whose results carry `resultFile` instead of `physicalLocation` —
  a schema change with no warning and no build failure.
- `ErrorLog` must be injected through a props file imported via
  `CustomAfterMicrosoftCommonTargets`, not through a command-line `-p:`.
  Command-line properties are literal strings, so `$(MSBuildProjectName)` lands
  on disk unexpanded and every inner build of a multi-targeting project fights
  over one file.

SARIF also surfaces diagnostics the console never prints: the fixture's
`SampleReports.Core` produces two information-severity analyzer diagnostics that
`dotnet build` does not show at any default verbosity. A Problems pane fed from
console text would be missing a whole severity class.

MSBuild-level failures — a missing workload, an unresolvable SDK, a failing
target — are not in SARIF. Phase 4 adds a small structured MSBuild logger for
them. Until then the worker reports a typed "the cause is outside the structured
channel" diagnostic and attaches the console log **verbatim and unparsed**.

## Authoritative builds

An `IBuildService`-style contract accepts an immutable granted workspace
snapshot, a pinned toolchain selection, a target, a configuration, a restore
policy, and a cancellation token. It streams typed progress, log, diagnostic,
and artifact events. Dirty overlays are included, so a build cannot silently
compile different content from the editor.

### Granted snapshot and the input manifest

The pre-evaluation snapshot contains every known granted workspace input:
solution and project files, sources, resources and content, manifests,
`global.json`, applicable ancestor `Directory.Build.*` and
`Directory.Packages.props`, NuGet configuration, linked items, known imports and
references, and dirty non-code files. `bin`, `obj`, VCS metadata, and worker
caches are excluded by policy.

Dynamic imports, SDK inputs, packages, analyzers, generators, and generated
files that can only be found during trusted evaluation and restore are
discovered **inside** the worker and added, with origin and hashes, to the final
build-input manifest. Phase 0 measured the gap concretely: 31 granted inputs,
then 21 more discovered during restore and 4 generated during the build.

Workspace-local discoveries outside a granted root fail with an actionable
capability diagnostic. Traversal, symlink, hardlink, and reparse-point escapes
are rejected at materialization **and again at artifact collection** — a target
is free to write wherever it likes, so a link planted during the build would
otherwise carry a file out of the job root disguised as an artifact.

### Ancestor isolation

The worker materializes each job into a neutral per-job root **outside the
Broiler checkout**, so unrelated staging ancestors cannot influence evaluation.
The Phase 0 fixture carries its own `Directory.Build.props` and
`Directory.Build.targets` for the same reason, so that it evaluates identically
in place and after materialization.

### Toolchain

The worker honours the workspace's `global.json` by resolving the SDK with the
job root as the working directory — the same way the CLI does — rather than
reading the pin and then building with the IDE's own SDK. A pin it cannot
satisfy is a toolchain diagnostic raised in preflight, before expensive work,
and never a source error or a silent substitution.

The worker reports the resolved SDK version **and the compiler identity that
produced the diagnostics**, read from the SARIF tool driver. On the Phase 0
baseline these differ — SDK compiler 5.6.0, language-service Roslyn 5.3.0 — and
Phase 3 requires that gap to be visible rather than mysterious.

### Process boundary and trust

MSBuild project evaluation and targets execute code. Even the local worker runs
out of process; the boundary protects the IDE's reliability, not the user's
account from their own project's targets. A remote worker additionally runs each
job in a disposable sandbox with no host secrets, bounded CPU, memory, time and
storage, an explicit network and restore policy, output limits, cancellation,
and verified cleanup.

Broiler Office Server may serve the WebAssembly client, but its current
static-file process must not also execute user builds.

Opening a workspace does not imply trust. An untrusted workspace opens in a
non-evaluating edit mode that reads project structure without executing MSBuild,
restore, targets, analyzers, or source generators. Evaluation and build require
an explicit, scoped trust decision. Third-party analyzers and generators never
load into the IDE process; when enabled, they execute only inside the worker's
trust boundary.

Cancellation terminates the entire process tree. MSBuild starts worker nodes,
and killing only the launcher leaves them holding the job directory open, which
defeats cleanup.

## Evaluated graph delivery

Roslyn alone is not a language service. A compilation with no metadata
references reports every framework type as missing, so the question for a
constrained host is not "can Roslyn run there" but "can the evaluated graph get
there".

The graph is obtained from MSBuild's own structured `-getItem`/`-getProperty`
JSON output — not from a build log, and identical under any UI language. It
supplies the metadata references, the preprocessor symbols, the language
version, the nullable context, **and the compile item list**.

The compile item list is part of the graph, not something to derive by scanning
directories. Evaluating the fixture's `SampleReports.App` yields 3 compile
items, not the 4 `.cs` files on disk, because one is conditioned out. Phase 0's
Roslyn harness initially scanned directories and produced 17 errors instead of
1 — it compiled two files declaring the same type and picked up `obj` assembly
info from both projects. The correct source is the evaluated graph.

## Phase 0 evidence

Everything asserted above is reproducible from
[`tests/broiler-code-phase0`](../../tests/broiler-code-phase0/):

| Claim | Evidence |
| --- | --- |
| Incremental classification is indistinguishable from full | 17 correctness checks, including insert/delete at every position of 11 multi-line-state sources |
| Stale work cannot paint | Every trace ends agreeing with a full classification and none painted a superseded snapshot; `completed-stale-run-rejected` covers the completed-then-discarded race deterministically |
| A cross-file error maps back to an exact snapshot | The worker reports CS7036 at a workspace-relative path, zero-based span, and the SHA-256 of the exact bytes compiled |
| A local semantic service agrees with the authoritative build | Both land on `ReportRunner.Broken.cs` line 17, column 31 |
| Diagnostics are locale-independent | The same build under English and Japanese produces byte-identical structure and different messages |
| Unsaved content is what gets compiled | The dirty overlay reaches the build and is recorded as such in the input manifest |
| Traversal out of the grant is refused | Refused in preflight with a capability diagnostic |
| An unsatisfiable toolchain pin fails early | `ToolchainUnavailable` before any build work, with the pin reported |
