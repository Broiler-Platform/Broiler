# ADR 0003 - Graphics Submission Boundary

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

Standard controls must draw through Broiler.Graphics while avoiding backend and
native-window coupling.

## Decision

Broiler.UI standard controls submit platform-neutral drawing through
`Broiler.Graphics` render primitives, render lists, geometry, color, text
measurement, and resource handles. Backend surfaces and presentation remain host
owned.

## Consequences

- UI runtime assemblies may reference `Broiler.Graphics`.
- UI runtime assemblies must not reference `Broiler.Graphics.Windows`,
  Direct2D, Win32, WPF, WinForms, COM, or HWND.
- Device-loss handling is expressed through host/resource recreation contracts,
  not backend-specific code inside controls.

