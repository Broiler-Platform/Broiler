# Broiler.UI Directory Structure Refactor Proposal

**Status:** Proposed  
**Date:** 2026-07-09  
**Scope:** Physical repository layout, solution organization, and navigation
rules for `Broiler.UI`. This proposal does not change public APIs, namespaces,
assembly names, package IDs, or the approved per-type assembly model.

## 1. Executive decision

Refactor the `Broiler.UI` filesystem layout around the architecture that already
exists:

1. keep `Broiler.UI.<Type>` abstraction assemblies and
   `Broiler.UI.<Type>.Standard` implementation assemblies;
2. separate abstractions from implementations in the directory tree;
3. group projects by product role under each side of that split; and
4. move demos, tests, bundles, and adapters out of the root project list.

The recommended shape is:

```text
Broiler.UI/
  src/
    Foundation/
    Abstractions/
    Implementations/
      Standard/
    Integrations/
    Bundles/
  tests/
  samples/
  docs/
  eng/
```

This keeps the current package topology intact while making the tree readable at
a glance. A developer should be able to answer three questions from the path
alone:

- Is this a public abstraction or a concrete implementation?
- Which UI family does it belong to?
- Is this runtime code, a test, a sample, an adapter, or a convenience bundle?

## 2. Why this refactor is worth doing

The current root of `Broiler.UI/` contains every project side by side:

```text
Broiler.UI.Button
Broiler.UI.Button.Standard
Broiler.UI.CheckBox
Broiler.UI.CheckBox.Standard
...
Broiler.UI.RichEdit.Standard.Tests
Broiler.UI.RichEdit.Win32.Demo
Broiler.UI.Standard.Tests
Broiler.UI.Win32.Demo
```

That layout was useful while the component was being grown phase by phase, but
it now hides important boundaries:

- abstractions and implementations look equally close to consumers;
- demos and tests sit beside runtime packages;
- core infrastructure, controls, composite widgets, dialogs, and rich text are
  all flattened into one directory;
- the `.Standard` suffix has to carry too much organizational meaning by itself;
  and
- new contributors can miss the approved architecture rules from ADR 0001 and
  ADR 0007 because the filesystem does not reinforce them.

The refactor should not fight the existing design. The component already made
the important architectural choice: one abstract root, one abstraction assembly
per independently instantiable type, and one standard implementation assembly
per type. The directory tree should simply make that choice obvious.

## 3. Naming and terminology

Use these terms consistently:

| Term | Meaning in the tree |
|---|---|
| `Foundation` | Root UI contracts and shared standard infrastructure, not individual controls |
| `Abstractions` | Public `Ui*` contracts and control-specific value/event types |
| `Implementations/Standard` | Broiler-drawn standard concrete controls and their factories |
| `Integrations` | Optional adapters to non-UI components or formats, such as RTF/DOM bridges |
| `Bundles` | Convenience package projects that aggregate references but add no behavior |
| `samples` | Host/demo applications, including platform-specific demos |
| `tests` | Test projects only |

Avoid `View` as a top-level directory name for now. In Broiler.UI, "view" could
mean a host viewport, a logical window, a document view, a control, or an
application screen. It is too ambiguous to be the primary sorting concept.

Avoid `Widgets` as a peer of `Controls` unless the project adopts a formal
distinction. Today the public model is "UI elements" and "control types"; adding
another noun without a separate contract would create vocabulary drift. If the
word is useful, use it informally for the standard concrete control set, not as
a new architectural boundary.

## 4. Recommended target tree

### 4.1 Runtime foundation

```text
Broiler.UI/src/Foundation/
  Broiler.UI/
  Broiler.UI.Standard/
```

`Broiler.UI` remains the platform-neutral root and host/session contract
assembly. `Broiler.UI.Standard` remains shared standard-control infrastructure
only. It should not contain public concrete type-specific controls.

As a follow-up inside `Broiler.UI.Standard`, split files into internal folders:

```text
Broiler.UI.Standard/
  Animation/
  Commands/
  Focus/
  Input/
  Rendering/
  Semantics/
  Theme/
  Tree/
```

That internal folder cleanup can happen independently from moving project
folders.

### 4.2 Abstraction projects

```text
Broiler.UI/src/Abstractions/
  Shell/
    Broiler.UI.Window/
    Broiler.UI.Dialog/
    Broiler.UI.Tooltip/
    Broiler.UI.FileDialog/
    Broiler.UI.FontDialog/
  Layout/
    Broiler.UI.Panel/
    Broiler.UI.ScrollView/
    Broiler.UI.TabView/
  Content/
    Broiler.UI.Label/
    Broiler.UI.ImageView/
    Broiler.UI.ProgressBar/
  Commands/
    Broiler.UI.Button/
    Broiler.UI.ToggleButton/
    Broiler.UI.Toolbar/
    Broiler.UI.Menu/
  ValueAndSelection/
    Broiler.UI.CheckBox/
    Broiler.UI.RadioButton/
    Broiler.UI.Slider/
    Broiler.UI.ListView/
    Broiler.UI.ComboBox/
  Text/
    Broiler.UI.Edit/
    Broiler.UI.RichEdit/
```

The buckets are deliberately practical rather than academic:

- `Shell` owns logical windows, transient UI, and host-facing dialog surfaces.
- `Layout` owns containment, scrolling, and visible region switching.
- `Content` owns passive display and status presentation.
- `Commands` owns invokable UI and command surfaces.
- `ValueAndSelection` owns user choice, range, and item selection.
- `Text` owns editable text and document editing abstractions.

### 4.3 Standard implementation projects

Mirror the abstraction buckets under `Implementations/Standard`:

```text
Broiler.UI/src/Implementations/Standard/
  Shell/
    Broiler.UI.Window.Standard/
    Broiler.UI.Dialog.Standard/
    Broiler.UI.Tooltip.Standard/
    Broiler.UI.FileDialog.Standard/
    Broiler.UI.FontDialog.Standard/
  Layout/
    Broiler.UI.Panel.Standard/
    Broiler.UI.ScrollView.Standard/
    Broiler.UI.TabView.Standard/
  Content/
    Broiler.UI.Label.Standard/
    Broiler.UI.ImageView.Standard/
    Broiler.UI.ProgressBar.Standard/
  Commands/
    Broiler.UI.Button.Standard/
    Broiler.UI.ToggleButton.Standard/
    Broiler.UI.Toolbar.Standard/
    Broiler.UI.Menu.Standard/
  ValueAndSelection/
    Broiler.UI.CheckBox.Standard/
    Broiler.UI.RadioButton.Standard/
    Broiler.UI.Slider.Standard/
    Broiler.UI.ListView.Standard/
    Broiler.UI.ComboBox.Standard/
  Text/
    Broiler.UI.Edit.Standard/
    Broiler.UI.RichEdit.Standard/
```

This is the important split. Implementations can be found by category, but they
cannot be mistaken for public contracts. It also makes forbidden dependencies
easier to spot during review: abstraction projects should not reference sibling
implementation projects, and standard implementation projects should continue
to flow through the approved abstraction and `Broiler.UI.Standard` edges.

### 4.4 Integrations, bundles, samples, and tests

```text
Broiler.UI/src/Integrations/
  RichEdit/
    Broiler.UI.RichEdit.Rtf/
    Broiler.UI.RichEdit.Dom/              # future, if approved/implemented

Broiler.UI/src/Bundles/
  Broiler.UI.All/

Broiler.UI/samples/
  Win32/
    Broiler.UI.Win32.Demo/
  Linux/
    Broiler.UI.Linux.Demo/
  RichEdit.Win32/
    Broiler.UI.RichEdit.Win32.Demo/

Broiler.UI/tests/
  Foundation/
    Broiler.UI.Tests/
    Broiler.UI.Standard.Tests/
  Commands/
    Broiler.UI.Toolbar.Tests/
  Text/
    Broiler.UI.RichEdit.Tests/
    Broiler.UI.RichEdit.Standard.Tests/
    Broiler.UI.RichEdit.Rtf.Tests/
```

Future test projects should live next to their category in `tests/`, not beside
runtime projects. Tests may still reference runtime projects across categories
where that is the behavior under test.

## 5. Mapping from current layout to target layout

| Current project family | Target location |
|---|---|
| `Broiler.UI` | `src/Foundation/Broiler.UI` |
| `Broiler.UI.Standard` | `src/Foundation/Broiler.UI.Standard` |
| `*.Window`, `*.Dialog`, `*.Tooltip`, `*.FileDialog`, `*.FontDialog` | `src/Abstractions/Shell` |
| `*.Window.Standard`, `*.Dialog.Standard`, `*.Tooltip.Standard`, `*.FileDialog.Standard`, `*.FontDialog.Standard` | `src/Implementations/Standard/Shell` |
| `*.Panel`, `*.ScrollView`, `*.TabView` | `src/Abstractions/Layout` |
| `*.Panel.Standard`, `*.ScrollView.Standard`, `*.TabView.Standard` | `src/Implementations/Standard/Layout` |
| `*.Label`, `*.ImageView`, `*.ProgressBar` | `src/Abstractions/Content` |
| `*.Label.Standard`, `*.ImageView.Standard`, `*.ProgressBar.Standard` | `src/Implementations/Standard/Content` |
| `*.Button`, `*.ToggleButton`, `*.Toolbar`, `*.Menu` | `src/Abstractions/Commands` |
| `*.Button.Standard`, `*.ToggleButton.Standard`, `*.Toolbar.Standard`, `*.Menu.Standard` | `src/Implementations/Standard/Commands` |
| `*.CheckBox`, `*.RadioButton`, `*.Slider`, `*.ListView`, `*.ComboBox` | `src/Abstractions/ValueAndSelection` |
| `*.CheckBox.Standard`, `*.RadioButton.Standard`, `*.Slider.Standard`, `*.ListView.Standard`, `*.ComboBox.Standard` | `src/Implementations/Standard/ValueAndSelection` |
| `*.Edit`, `*.RichEdit` | `src/Abstractions/Text` |
| `*.Edit.Standard`, `*.RichEdit.Standard` | `src/Implementations/Standard/Text` |
| `Broiler.UI.RichEdit.Rtf` | `src/Integrations/RichEdit` |
| `Broiler.UI.All` | `src/Bundles` |
| `*.Tests` | `tests/<matching category>` |
| `*.Demo` | `samples/<platform-or-scenario>` |

## 6. Rules to preserve during the move

1. Do not rename assemblies, namespaces, packages, public types, or files during
   the first filesystem move.
2. Do not merge abstraction assemblies into a `Controls` assembly.
3. Do not merge standard implementations into a `Controls.Standard` assembly.
4. Do not introduce `Broiler.UI.Windows` as a runtime implementation bucket.
   Platform-specific code stays in samples/hosts or outside Broiler.UI runtime
   assemblies.
5. Keep `Broiler.UI.Standard` as infrastructure only.
6. Keep `Broiler.UI.All` as a bundle with no behavior.
7. Preserve all architecture tests and extend them with path/category checks
   only after the move is stable.
8. Keep `docs/` and `eng/` at the component root so standalone component builds
   and packaging metadata remain easy to find.

## 7. Roadmap

### Phase 0 - Approve taxonomy and guardrails

**Status:** Complete (2026-07-09). The taxonomy is approved in
`Broiler.UI/docs/adr/0019-directory-structure-topology.md`; path-sensitive move
surfaces are inventoried in
`Broiler.UI/docs/phase0/directory-structure-path-inventory.md`.

Objective: decide the target buckets before moving files.

Tasks:

- Approve the `Foundation`, `Abstractions`, `Implementations/Standard`,
  `Integrations`, `Bundles`, `samples`, and `tests` layout.
- Confirm the category mapping in section 5.
- Add an ADR if repository topology policy needs a formal update to ADR 0012.
- Identify every file that embeds project paths: `Broiler.UI.slnx`,
  aggregate `Broiler.slnx`, project references, docs, scripts, CI files, and
  phase boundary JSON records.

Exit gate:

- the taxonomy is approved;
- no assembly/package/API rename is part of the first move; and
- every path-updating surface is listed.

### Phase 1 - Mirror the target layout in solution folders

**Status:** Complete (2026-07-09). `Broiler.UI.slnx` now mirrors the target
component buckets with `/src`, `/samples`, and `/tests` solution folders.
The aggregate `Broiler.slnx` mirrors the same grouping under `/Dependencies/UI`.
`Broiler.UI/README.md` now lists projects by the approved categories. No project
directories were moved.

Objective: preview the shape without moving project files.

Tasks:

- Reorganize `Broiler.UI.slnx` folders to match the target tree:
  `/src/foundation`, `/src/abstractions/<category>`,
  `/src/implementations/standard/<category>`, `/src/integrations`,
  `/src/bundles`, `/tests`, and `/samples`.
- Do the same in the aggregate solution if it has explicit UI solution folders.
- Update `Broiler.UI/README.md` so the project list is grouped by category.

Exit gate:

- `dotnet build Broiler.UI/Broiler.UI.slnx` succeeds;
- IDE navigation reflects the target grouping; and
- no filesystem paths have changed yet.

### Phase 2 - Move runtime project directories

**Status:** Complete (2026-07-09). Runtime UI projects now live under
`Broiler.UI/src`: foundation projects in `src/Foundation`, public control
contracts in `src/Abstractions/<category>`, standard controls in
`src/Implementations/Standard/<category>`, RichEdit RTF in
`src/Integrations/RichEdit`, and `Broiler.UI.All` in `src/Bundles`.
Project references, solution paths, architecture tests, and the pack script were
updated. Test and sample projects intentionally remain at the component root for
Phase 3.

Objective: move source projects with the smallest possible behavioral diff.

Tasks:

- Move `Broiler.UI` and `Broiler.UI.Standard` into `src/Foundation`.
- Move abstraction projects into `src/Abstractions/<category>`.
- Move standard implementation projects into
  `src/Implementations/Standard/<category>`.
- Move `Broiler.UI.RichEdit.Rtf` into `src/Integrations/RichEdit`.
- Move `Broiler.UI.All` into `src/Bundles`.
- Update all `ProjectReference` paths.
- Update `Broiler.UI.slnx`, aggregate `Broiler.slnx`, docs, scripts, and CI.
- Keep project file names and assembly names unchanged.

Exit gate:

- `dotnet build Broiler.UI/Broiler.UI.slnx` succeeds;
- `dotnet test Broiler.UI/Broiler.UI.slnx` succeeds or any unrelated failures
  are documented;
- package IDs and assembly names are unchanged; and
- architecture tests still prove abstraction/implementation boundaries.

### Phase 3 - Move tests and samples

**Status:** Complete (2026-07-09). Test projects now live under
`Broiler.UI/tests` by category: foundation tests in `tests/Foundation`, toolbar
tests in `tests/Commands`, and RichEdit tests in `tests/Text`. Demo projects now
live under `Broiler.UI/samples` by scenario: `samples/Win32`,
`samples/Linux`, and `samples/RichEdit.Win32`. Project references, solution
paths, README commands, and Linux demo CI publishing were updated.

Objective: remove non-runtime noise from the component root.

Tasks:

- Move all test projects under `tests/<category>`.
- Move demo projects under `samples/<platform-or-scenario>`.
- Update references, solution paths, docs, and validation commands.
- Keep demo namespaces and binaries unchanged unless a later cleanup approves
  renames.

Exit gate:

- the root of `Broiler.UI/` contains only `src`, `tests`, `samples`, `docs`,
  `eng`, component metadata, and solution/build files;
- all UI tests build and run from the new paths; and
- README validation commands use the new paths.

### Phase 4 - Internal folder hygiene

**Status:** Complete (2026-07-09). `src/Foundation/Broiler.UI` now groups root
contracts by responsibility (`Elements`, `Host`, `Input`, `Layout`,
`Rendering`, `Routing`, `Semantics`, `Session`, and `System`).
`src/Foundation/Broiler.UI.Standard` now groups shared standard infrastructure
by responsibility (`Animation`, `Commands`, `Focus`, `Input`, `Rendering`,
`Semantics`, `Session`, `Theme`, and `Tree`). Namespaces, assembly names,
package IDs, and project files were left unchanged.

Objective: improve navigation inside the larger shared projects.

Tasks:

- Split `Broiler.UI` files into internal folders such as `Host`, `Input`,
  `Layout`, `Rendering`, `Routing`, `Semantics`, `Session`, and `System`.
- Split `Broiler.UI.Standard` files into the folders listed in section 4.1.
- Split large standard controls only where the file count justifies it. Do not
  create nested folder ceremony for two-file assemblies.
- Add a short `README.md` to `src/Abstractions` and `src/Implementations` if
  needed to explain the dependency direction.

Exit gate:

- no namespace or public API churn is introduced solely for folder movement;
- small control assemblies remain simple; and
- shared infrastructure is easier to scan by responsibility.

### Phase 5 - Add topology validation

**Status:** Complete (2026-07-09). `Broiler.UI.Tests` now includes repository
topology validation in `UiTopologyTests`: project roles must live under the
approved `src`, `tests`, or `samples` folders; abstraction and standard
implementation projects must use approved categories; standard implementations
must have matching abstractions; abstraction projects cannot reference
`src/Implementations/Standard`; and runtime `src` projects cannot reference
Windows-specific projects.

Objective: make the new structure executable instead of tribal knowledge.

Tasks:

- Extend architecture tests or add a small repository topology test that checks:
  - abstraction projects live under `src/Abstractions`;
  - standard implementation projects live under `src/Implementations/Standard`;
  - demo projects live under `samples`;
  - test projects live under `tests`;
  - no runtime abstraction references a sibling standard implementation; and
  - no runtime UI project references Windows-specific assemblies.
- Keep the test data generated or easy to update when a new control pair is
  intentionally added.

Exit gate:

- misplaced new projects fail fast in CI; and
- adding a new control type has an obvious checklist.

## 8. Dependency policy after the refactor

The physical tree should reinforce this dependency direction:

```text
src/Foundation/Broiler.UI
  -> Broiler.Graphics, Broiler.Input abstractions

src/Abstractions/<category>/Broiler.UI.<Type>
  -> src/Foundation/Broiler.UI
  -> approved sibling abstractions only when required by public API

src/Foundation/Broiler.UI.Standard
  -> src/Foundation/Broiler.UI

src/Implementations/Standard/<category>/Broiler.UI.<Type>.Standard
  -> matching abstraction
  -> src/Foundation/Broiler.UI.Standard
  -> platform-neutral Broiler.Graphics/Input abstractions

src/Integrations/<area>
  -> relevant UI abstraction
  -> explicitly approved external component, such as Broiler.Documents

samples/<platform>
  -> selected UI implementations
  -> platform-specific Graphics/Input hosts
```

The important review smell after this move is an abstraction project reaching
"down" into `src/Implementations`.

## 9. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Moving many projects creates path churn | Large diff and merge pain | Use a dedicated PR with no code edits beyond path/reference updates |
| Category names become a debating trap | Delays | Approve the section 5 mapping once; allow later one-project moves only with a small rationale |
| Abstraction and implementation pairs become harder to compare | Slower local edits | Mirror category names exactly and keep project names unchanged |
| CI or packaging scripts embed old paths | Broken builds | Phase 0 path inventory, then run full UI and aggregate builds before merging |
| Docs and phase records point to old paths | Confusing history | Update live docs; leave historical phase records alone if they are intentionally archival |
| `Broiler.UI.Standard` remains a mixed bag internally | Navigation still hard | Phase 4 splits shared infrastructure by responsibility |
| The move accidentally becomes a namespace/API cleanup | Consumer breakage | Hard rule: path-only first, cleanup later |

## 10. Recommended first PR sequence

1. Add this proposal and, if approved, a short ADR amending the component
   topology.
2. Reorganize only `Broiler.UI.slnx` folders and README project grouping.
3. Move runtime projects under `src/` and update references.
4. Move tests and samples.
5. Add topology validation.
6. Split internal folders in `Broiler.UI` and `Broiler.UI.Standard`.

This sequence gives an early preview, then does the noisy filesystem move in
clean, reviewable chunks.

## 11. Definition of done

The directory refactor is complete when:

- the component root is no longer a flat list of runtime, test, sample, bundle,
  and adapter projects;
- abstractions and standard implementations are physically separated;
- the abstraction and implementation buckets use the same category names;
- tests and samples live outside runtime source folders;
- all project references, solutions, docs, scripts, and CI paths are updated;
- package IDs, assembly names, namespaces, and public APIs are unchanged unless a
  later non-layout proposal approves them;
- topology tests prevent future drift; and
- a new developer can locate "the public Button contract" and "the standard
  Button implementation" from the path alone.
