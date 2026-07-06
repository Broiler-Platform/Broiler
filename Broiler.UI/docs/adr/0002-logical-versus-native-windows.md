# ADR 0002 - Logical Versus Native Windows

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

Current `BWindow` exposes `IntPtr NativeHandle` and owns native window lifetime.
Broiler.UI must remain platform-neutral.

## Decision

`UiWindow` is a logical UI window with content, ownership, state, focus scope,
z-order, and close lifecycle. Native top-level windows, HWNDs, message loops,
and destruction stay in the application host.

## Consequences

- Broiler.UI never exposes native handles.
- Dialogs, menus, tooltips, and popups are logical managed subwindows by
  default.
- A host may map a logical window to a native window, but that mapping is not a
  Broiler.UI API.

