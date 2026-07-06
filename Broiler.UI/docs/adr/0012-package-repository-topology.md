# ADR 0012 - Package/Repository Topology

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

The aggregate repository already contains several root components and submodule
checkouts. Broiler.UI needs a place to develop without forcing a premature
repository split.

## Decision

Broiler.UI starts as a root component directory in the aggregate repository.
Phase 1 adds projects under `Broiler.UI/`. Package publishing is per assembly
after the API has been proven by the Windows browser migration.

## Consequences

- A separate UI submodule or repository split requires a future ADR.
- Aggregate solution references may be added incrementally while each PR remains
  buildable.
- Submodule pointer updates happen after provider and consumer changes land.

