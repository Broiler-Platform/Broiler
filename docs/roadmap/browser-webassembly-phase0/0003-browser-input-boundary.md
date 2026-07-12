# Decision 0003 - Browser Input and Host-Capability Boundary

**Status:** Approved for Phase 1  
**Date:** 2026-07-11

## Context

Browser Pointer Events, keyboard events, text/composition events, clipboard
events, and file pickers do not have identical lifecycle or permission semantics
to desktop event loops. The current neutral Input records cover the first
pointer/keyboard/text slice, while current UI routing loses some metadata and has
no explicit pointer-cancel/capture-lost event.

## Decision

The Phase 1 sample observes browser events and emits neutral Broiler Input/UI
events. Keyboard transitions stay separate from committed text and composition.
The sample normalizes logical key names and treats browser-owned shortcuts
explicitly.

For T2 pointer cancel, lost DOM capture, blur, page hide, and disposal, the host
tests this compatibility cleanup while logical capture is still active:

1. move outside the viewport to clear hover;
2. release the pressed button outside the viewport to reset private press/drag
   state without activation; and
3. release remaining `UiSession` capture.

If any selected control cannot reset safely, a neutral routed cancel event is
permitted as a checked-in, cross-platform behavior fix.

The hidden editable is application-aware for T2 and provides caret positioning
plus basic committed text. T3 robust IME may add a neutral focus/text-context
lifecycle and composition selection projection.

Browser clipboard shortcut sequencing is owned by trusted `copy`/`cut`/`paste`
events so managed controls never paste stale data and then the real payload.
Path-based `StandardFileDialog` is not used; Writer open/save uses browser
streams/opaque resources.

T2 passive semantic inspection is host-driven after layout/render and makes no
accessibility support claim. T3 requires actionable semantics and focus for the
selected Writer workflow; generalized identity/actions/virtual children are a
T4 gate.

## Consequences

- No neutral Input/UI contract changes are required to start Phase 1.
- Repeat/location metadata loss, Meta/Command, pointer cancellation, IME
  selection, async clipboard, focus traversal, inbound drag/drop, and touch/pen
  detail each retain an explicit failing-scenario gate.
- Browser permissions and user activation remain application-host policy.
