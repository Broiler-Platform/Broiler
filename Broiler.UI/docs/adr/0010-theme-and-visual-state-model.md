# ADR 0010 - Theme And Visual State Model

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

Standard controls need consistent visuals without importing a platform theme API
or a CSS engine.

## Decision

Broiler.UI.Standard owns platform-neutral theme tokens, visual-state data, high
contrast inputs, reduced-motion inputs, focus visuals, and animation timing.
Controls resolve tokens during render traversal.

## Consequences

- Themes are data, not platform probes inside controls.
- Standard controls can be tested with deterministic clocks and recording
  renderers.
- A later platform can supply different tokens without changing public control
  contracts.

