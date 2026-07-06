# ADR 0008 - Accessibility Semantic Bridge

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

Current native controls inherit Windows accessibility behavior. Broiler-drawn
controls need platform-neutral accessibility data.

## Decision

Broiler.UI owns semantic snapshots: roles, names, values, states, actions,
relationships, bounds, and virtualized children. Native accessibility bridges
are application-host responsibilities.

## Consequences

- UI runtime assemblies do not reference Windows UI Automation.
- Semantic snapshots must avoid exposing private Edit contents when privacy
  modes require redaction.
- Windows UIA mapping is tested in the host/integration layer, not inside UI
  runtime assemblies.

