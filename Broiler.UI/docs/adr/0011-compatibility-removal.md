# ADR 0011 - Compatibility Removal

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Context

Graphics currently owns controls and input callbacks used by the browser app.
Removing them too early would break the aggregate and standalone Graphics
checkouts.

## Decision

Graphics control and input compatibility remains until Broiler.UI proves the
browser toolbar path and the application migrates. Old APIs are marked obsolete
only after replacement gates pass and are removed only at a later approved
breaking release.

## Consequences

- Provider-first migration order is mandatory.
- `BControl`, `BButtonControl`, `BEditControl`, `BLabelControl`,
  `BControlOptions`, `BWindow.Create*Control`, and Graphics callbacks stay
  during early UI phases.
- The exact obsolete/removal release numbers are recorded when preview packages
  exist.

