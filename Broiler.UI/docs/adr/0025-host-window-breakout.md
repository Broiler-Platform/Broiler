# ADR 0025 - Host-Window Break-Out for Secondary Windows

**Status:** Approved
**Date:** 2026-08-24

## Context

ADR [0002](0002-logical-versus-native-windows.md) made secondary windows —
owned windows, dialogs, menus, tooltips, popups — **logical** managed subwindows
rendered inside their owner's viewport, and left open (see the roadmap) *whether
secondary logical windows may map to native top-level windows*. Some subwindows
(a floating tool panel, a long-running dialog) are better as real OS windows the
user can move outside the main window, place on another monitor, and manage with
the window manager.

Broiler.UI must stay platform-neutral and never expose native handles (ADR 0002),
so it cannot create native windows itself.

## Decision

A logical subwindow **may break out into its own native top-level host window**,
one-way for the window's lifetime, through a host capability — never by exposing a
native handle.

- A primary `IUiHost` may also implement the optional capability `IUiWindowHost`
  (discovered with `Host is IUiWindowHost`, like the other optional host
  capabilities). It creates a native top-level window and returns an
  `IUiHostWindow`: itself an `IUiHost` that a new `UiSession` renders into and
  receives input from, plus a neutral lifecycle (`Bind`, `SetTitle`, `Activate`,
  `CloseRequested`, `Dispose`). No native handle crosses the boundary.
- `UiWindow.BreakOut` detaches the subwindow from its owner, asks the host to
  create a host window, and re-roots the subwindow in a fresh `UiSession` bound to
  it (reusing the origin session's dispatcher, clock, and factories). The platform
  owns the OS window and pumps its loop; the framework owns the reparenting and the
  new session.
- Break-out is **one-way**: closing the broken-out window disposes its host window
  and session. The reparent is written so a future dock-back could reverse it.
- **Modality is preserved across windows.** A modal dialog may break out and stay
  application-modal: its origin session is marked blocked
  (`UiSession.PushExternalModal`/`IsBlockedByExternalModal`) so the origin window
  swallows input while the dialog lives in another host window, independent of any
  native owner/modal support.

## Consequences

- Broiler.UI still exposes no native handles; the capability trades only in
  `IUiHost`, `UiSession`, `BRect`, `string`, and events. The
  `Runtime_Assemblies_Do_Not_Expose_Native_Handles_Or_Windows_Types` architecture
  test continues to hold for the new Foundation types.
- Hosts that do not implement `IUiWindowHost` are unaffected: `CanBreakOut` is
  false and `BreakOut` returns false, so subwindows remain logical.
- The Win32 host realizes break-out with a second `Direct2DWindow` that does not
  own the message loop (`BWindowOptions.OwnsMessageLoop = false`), so closing it
  does not quit the application.
