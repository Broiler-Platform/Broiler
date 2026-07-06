# Broiler.UI Phase 0 Package, Repository, and Compatibility Decisions

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Repository Placement

Broiler.UI starts as a root component directory in the aggregate repository:

```text
Broiler.UI/
```

Phase 0 contains records and characterization tests only. Phase 1 adds runtime
projects under this component root. A later repository split or submodule
conversion requires a separate ADR after the first Windows migration proves the
API shape.

## Package Shape

Each public abstraction and standard implementation assembly is versioned as an
independent package:

```text
Broiler.UI
Broiler.UI.Standard
Broiler.UI.Window
Broiler.UI.Window.Standard
Broiler.UI.Panel
Broiler.UI.Panel.Standard
...
```

An optional package-only convenience bundle may be added after stabilization,
but no runtime assembly may become a monolithic controls bundle.

## Versioning

- Abstraction packages use semantic versioning independently.
- Standard implementation packages declare their supported abstraction version
  range.
- Implementation packages may depend on shared `Broiler.UI.Standard` support,
  but sibling standard implementation references are forbidden unless a later
  ADR approves a type-specific exception.
- Package tests must reject ambiguous duplicate factory selection.

## Graphics Compatibility Window

Existing Graphics-owned controls are retained until the Broiler.UI replacement
path passes production gates.

| Current API | Policy |
|---|---|
| `BControl`, `BButtonControl`, `BEditControl`, `BLabelControl` | Keep during UI proof and browser migration. Mark obsolete only after the new path passes the Phase 4 and Phase 8 gates. |
| `BControlOptions` | Keep as native-control compatibility options until no application chrome uses it. |
| `BWindow.Create*Control` | Keep until BrowserWindow no longer creates native child controls. |
| Graphics input callbacks | Keep until Broiler.Input providers and UI route adapters are in place. |
| `BWindow.NativeHandle` | Keep for Graphics/app host compatibility, but never expose it through Broiler.UI. |

After obsoletion, keep one documented compatibility window and remove only at a
future approved breaking release. The exact release numbers must be recorded
when the first preview package train exists.

## Cross-Repository Order

Provider-first order is mandatory:

1. Add or extend platform-neutral provider contracts.
2. Consume those contracts in Broiler.UI and application migration paths.
3. Remove old consumer usage.
4. Deprecate or remove old provider APIs.
5. Update aggregate submodule pointers last.

