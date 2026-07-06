# ADR 0009 - Edit Text Model

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

`BEditControl` currently uses a native Win32 edit control. A Broiler-drawn Edit
must reach strong text, IME, accessibility, and privacy gates before replacing
it.

## Decision

Standard Edit uses a UI-owned text model with explicit text storage,
selection/caret state, undo bounds, composition transactions, scrolling,
submission, and password/privacy behavior. Host ports provide clipboard,
composition, and caret-service integration.

## Consequences

- Native edit remains a compatibility path until replacement gates pass.
- Text indices, grapheme behavior, bidi handling, and composition rules require
  focused tests before API stabilization.
- Edit diagnostics must redact protected text by default.

