# ADR 0024 - TabView Document Behavior

**Status:** Approved for Broiler Code Phase 0  
**Date:** 2026-08-05

## Context

`UiTabView` today presents a fixed set of tabs. A document area needs tabs that
can be closed, reordered, marked dirty, overflow when they do not fit, and
report which document they carry — and a close that a host can **refuse**,
because closing a dirty document has to be able to raise Save/Discard/Cancel.

Broiler Code is the first consumer with these needs, but they are ordinary tab
behaviour rather than IDE behaviour, and Writer would benefit from the same
capabilities if it grew a multi-document mode.

## Decision

These capabilities extend the existing `UiTabView` and `StandardTabView`. A
separate DocumentTabView control is not introduced: the difference is a set of
optional behaviours, not a different control, and splitting it would leave two
tab controls to keep consistent.

### Close is a request

A close affordance raises a **close request** carrying the tab's identity. The
host decides. A host that does nothing leaves the tab open.

The control never removes a tab on its own in response to a user gesture. This
is the whole point: Save/Discard/Cancel cannot be expressed by a control that
has already closed the document.

Programmatic removal remains available for a host that has decided.

### Dirty state and dynamic headers

A tab carries a dirty flag and a header that can change without the tab being
recreated — a renamed file must not lose its position, its scroll state, or its
identity.

The dirty marker is a distinct visual element, never color alone, and is exposed
in the tab's accessible description. High-contrast and reduced-motion
preferences apply.

### Identity

Tabs are identified by a caller-supplied stable ID. Reorder, overflow, and
activation are expressed in terms of that ID, not of an index, so a host holding
a reference to "the tab for document X" stays correct across every operation.

Activating a tab whose document is already open activates the existing tab
rather than adding a second one. The control provides the lookup; the host
decides what counts as the same document.

### Reorder and overflow

- Tabs can be reordered by pointer drag and by keyboard command. Reorder raises
  an event before it is applied so a host can persist the order.
- When tabs do not fit, the control overflows rather than shrinking them below
  legibility. The overflow affordance is keyboard reachable and lists the hidden
  tabs by header.
- The active tab is always reachable: activating a tab in the overflow scrolls
  it into the visible strip.

### Focus and semantics

- The tab strip is one tab stop. Arrow keys move between tabs; the selected tab
  is the tab stop.
- Closing a tab moves focus deterministically: to the next tab, or to the
  previous one when the closed tab was last, or to the empty-state content when
  none remain. Focus never moves to a removed element and never leaves the
  document area unexpectedly.
- Semantics expose the tab list, each tab's selected state, its dirty state, and
  its position, plus the relationship between a tab and its content panel.

### Compatibility

Every capability here is additive and optional. A host that sets none of them
gets today's behaviour. Existing Writer and Broiler.UI tab tests are the gate on
that claim.

## Consequences

- The Phase 2 document area is composition rather than a new control.
- Save/Discard/Cancel on close is expressible, which it is not today.
- Reorder, overflow, and dirty state get Broiler.UI's keyboard, focus, theme,
  and semantics tests rather than product-local ones.
- `UiTabView` grows a wider surface. That is accepted in preference to a second
  tab control, and the additive rule keeps the growth from reaching existing
  consumers.

## Follow-up

Phase 2 delivers these capabilities alongside `UiTreeView` (ADR 0023) and
composes both into the Code shell. Split views and tab groups are not part of
this record.
