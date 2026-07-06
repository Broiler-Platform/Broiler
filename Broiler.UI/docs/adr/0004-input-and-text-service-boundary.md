# ADR 0004 - Input And Text-Service Boundary

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

Graphics currently translates Win32 messages into `BWindow` callbacks. The UI
roadmap requires normalized input from Broiler.Input.

## Decision

Broiler.UI consumes platform-neutral Broiler.Input contracts for pointer,
keyboard, text, and composition. A removable legacy adapter may translate the
current Graphics callbacks during migration.

## Consequences

- Public UI input events do not carry Windows message IDs, virtual-key
  constants, or HWNDs.
- Text composition, IME, clipboard, and caret geometry use host ports rather
  than platform probes.
- The legacy Graphics adapter is compatibility code and must have a removal
  gate after Broiler.Input cutover.

