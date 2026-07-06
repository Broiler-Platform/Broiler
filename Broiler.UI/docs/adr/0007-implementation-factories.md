# ADR 0007 - Implementation Factories

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

Applications must be able to choose only the control implementations they use
without hidden global registration.

## Decision

Broiler.UI uses explicit immutable factory sets supplied by the application or
host composition layer. It does not use reflection discovery, module
initializers, or mutable process-wide registries for standard control
selection.

## Consequences

- Adding a Button implementation does not pull unrelated controls into the
  dependency graph.
- Duplicate or ambiguous factories are rejected deterministically.
- Factory selection is testable without loading every implementation assembly.

