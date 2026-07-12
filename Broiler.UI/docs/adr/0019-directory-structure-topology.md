# ADR 0019 - Directory Structure Topology

**Status:** Approved for directory-refactor Phase 1  
**Date:** 2026-07-09

## Context

The `Broiler.UI` component now contains the root runtime, shared Standard
infrastructure, many per-type abstraction assemblies, matching `.Standard`
implementation assemblies, optional adapters, demos, tests, and a convenience
bundle. Those projects currently live as a flat list directly under
`Broiler.UI/`.

ADR 0001 already requires one abstraction assembly per independently
instantiable UI type and one `.Standard` implementation assembly per standard
control. ADR 0007 requires explicit factory selection rather than hidden global
registration. ADR 0012 keeps `Broiler.UI` as a root component directory in the
aggregate repository.

The flat filesystem layout no longer reinforces those decisions. Abstractions,
implementations, tests, demos, adapters, and bundles look like peer runtime
packages even though they have different ownership and dependency rules.

## Decision

`Broiler.UI` will move to a role-oriented filesystem topology:

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

Runtime project placement is:

- `src/Foundation`: `Broiler.UI` and `Broiler.UI.Standard`;
- `src/Abstractions/<category>`: public `Ui*` control contracts;
- `src/Implementations/Standard/<category>`: standard concrete controls and
  factories;
- `src/Integrations/<area>`: optional adapters to other Broiler components or
  file formats;
- `src/Bundles`: meta-package/convenience projects with no behavior;
- `tests/<category>`: test projects; and
- `samples/<platform-or-scenario>`: demo and host applications.

The approved initial categories are:

| Category | Abstractions |
|---|---|
| `Shell` | Window, Dialog, Tooltip, FileDialog, FontDialog |
| `Layout` | Panel, ScrollView, TabView, Splitter |
| `Content` | Label, ImageView, ProgressBar |
| `Commands` | Button, ToggleButton, Toolbar, Menu |
| `ValueAndSelection` | CheckBox, RadioButton, Slider, ListView, ComboBox |
| `Text` | Edit, RichEdit, FormatCodeView |

The standard implementation tree mirrors those category names exactly.

This is a filesystem and solution-organization decision only. It does not rename
assemblies, namespaces, packages, public types, project files, or control
families.

## Consequences

- A project path will show whether the project is an abstraction,
  implementation, integration, bundle, test, or sample.
- New controls must choose both a public abstraction category and a mirrored
  standard implementation category.
- `Broiler.UI.Standard` remains shared infrastructure only; it does not become a
  control bundle.
- `Broiler.UI.All` remains a bundle project and does not gain behavior.
- No `Broiler.UI.Windows` runtime bucket is introduced. Platform-specific code
  remains in samples/hosts or outside UI runtime assemblies.
- Solution files, project references, scripts, docs, phase boundary records, and
  architecture tests that embed project paths must be inventoried before moving
  directories.
- Historical phase records may preserve old paths when they are archival
  evidence; live build, packaging, CI, and README commands must move to the new
  paths.

## Follow-up

Phase 1 may reorganize solution folders without moving projects. Later phases
may move runtime projects, tests, and samples in separate path-only changes.
After the filesystem move is stable, topology validation should make misplaced
new projects fail in CI.
