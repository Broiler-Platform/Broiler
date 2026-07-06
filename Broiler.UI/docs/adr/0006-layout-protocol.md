# ADR 0006 - Layout Protocol

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

Broiler.UI needs widget layout but must not absorb webpage layout or depend on
`Broiler.Layout`.

## Decision

Broiler.UI defines a small widget measure/arrange protocol in logical units.
Visibility, invalidation, layout rounding, constraints, transforms, and
right-to-left data are UI concepts. CSS/web layout remains outside UI.

## Consequences

- `Broiler.UI` does not reference `Broiler.Layout`.
- Panels and controls implement widget layout policies, not CSS formatting
  contexts.
- Layout cycles are rejected before a tree mutation becomes visible.

