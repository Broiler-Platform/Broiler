# ADR 0023 - TreeView Control

**Status:** Approved for Broiler Code Phase 0  
**Date:** 2026-08-05

## Context

Broiler.UI's `ListView` is flat. A Solution Explorer needs solution, project,
folder, source, reference, and diagnostic nodes, and the Phase 2 exit gate names
a 1,000-file tree that must stay keyboard and pointer accessible while rendering
by visible range.

A tree is a general control. Building it inside Broiler Code would put shared
behaviour — keyboard navigation, focus, expansion, selection, theming,
semantics — in a product assembly where no other consumer can reach it, and
where Broiler.UI's own tests do not cover it.

## Decision

### Control family

- `UiTreeView` is a new abstraction in its own `Broiler.UI.TreeView` assembly.
- `StandardTreeView` and its factory live in the mirrored
  `Broiler.UI.TreeView.Standard` assembly.

ADR 0001 and ADR 0019 apply unchanged. This is a general Broiler.UI control that
Broiler Code consumes, not a Broiler Code control that happens to live in
Broiler.UI.

### Virtualized by row, not by node

The control materializes visual rows for the visible range only. Expanding a
node with ten thousand children creates rows for the visible ones.

Consequently the control does **not** own the node tree. It consumes a
hierarchical data source that answers:

- how many children a node has, without enumerating them;
- the child at an index; and
- whether a node can be expanded, without expanding it.

A Solution Explorer over a large directory must be able to answer "is this
folder expandable" without listing it, and a control that requires a fully
materialized tree makes that impossible.

### Identity is stable and external

Nodes are identified by a caller-supplied stable ID, not by index or by
reference. Expansion state, selection, and scroll position are keyed by that ID
and survive a refresh that replaces node instances.

This matters for the product reason recorded in the architecture: an IDE rename
retains a file's identity, and the explorer must not collapse the tree because
the underlying objects were rebuilt.

### Keyboard, focus, and semantics

- Arrow keys navigate and expand or collapse; Home and End go to the first and
  last visible rows; type-ahead selects within the expanded set.
- Exactly one node is the focus target for tab order. Focus does not move to a
  node that scrolls out of view.
- Semantics expose role, level, expanded state, position within its level, and
  the size of its level, so a screen reader can describe the structure without a
  materialized tree.
- Selection modes: single and extended. Multi-select is a Phase 2 need for the
  explorer.

### Presentation

Nodes carry an icon slot, a primary label, an optional secondary label, and an
optional state decoration such as a dirty marker or a diagnostic severity.
State is never conveyed by color alone, and every state stays distinguishable in
high contrast.

## Consequences

- The Solution Explorer is a data source plus a presentation policy, not a
  control.
- Broiler.UI gains a control other consumers can use, and Broiler.UI's own
  keyboard, focus, theme, and semantics tests cover it.
- The 1,000-file exit gate is a property of the control, testable in Broiler.UI
  before the workspace exists.
- Lazy child counting constrains the data source's design. That is intentional:
  the alternative pushes the cost onto every consumer with a large tree.

## Follow-up

Phase 2 delivers the control and the Solution Explorer data source. Drag and
drop, in-place rename, and filtering are not part of this record and are
separately gated.
