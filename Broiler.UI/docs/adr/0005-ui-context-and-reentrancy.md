# ADR 0005 - UI Context And Reentrancy

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

Retained-mode UI state must avoid tree corruption when input, layout, render,
and event handlers interact.

## Decision

Each `UiSession` owns a UI context that serializes tree mutation, event
delivery, focus/capture transitions, layout, invalidation, and rendering.
Mutations that occur during sensitive traversals are queued to safe points.

## Consequences

- Public mutable state is accessed on the owning UI context unless documented as
  immutable snapshot data.
- Rendering does not execute public event handlers while holding traversal
  locks.
- Reentrancy and disposal order become architecture and fuzz-test targets in
  Phase 1 and Phase 2.

