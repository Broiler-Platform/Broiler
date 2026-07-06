# ADR 0001 - UI Root And Per-Type Assembly Rule

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

Broiler.UI needs a shallow public hierarchy that can grow one control at a time
without creating a monolithic controls assembly.

## Decision

`UiElement` is the only abstract root class in `Broiler.UI`. Every independently
instantiable UI type has exactly one public abstract base in its own abstraction
assembly. Every standard concrete implementation has its own `.Standard`
implementation assembly.

## Consequences

- `Broiler.UI.Standard` contains shared infrastructure only.
- A new public control type requires an abstraction assembly and a standard
  implementation assembly.
- Public inheritance deeper than the roadmap-approved exceptions requires a
  later ADR.

