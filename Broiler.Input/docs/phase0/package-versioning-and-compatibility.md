# Broiler.Input Phase 0 Package, Repository, And Compatibility Decisions

**Status:** Approved for Phase 1  
**Date:** 2026-07-02

## Package Names

Each runtime assembly ships as its own package with the same name:

```text
Broiler.Input
Broiler.Input.Windows
Broiler.Input.Keyboard
Broiler.Input.Keyboard.Windows
Broiler.Input.Mouse
Broiler.Input.Mouse.Windows
```

There is no package-only convenience bundle in Phase 0.

## Version Alignment

- All packages in the initial preview train use the same preview version.
- Abstraction packages may stabilize independently after the first API review.
- Windows implementation packages declare compatible major versions of their
  abstraction and `Broiler.Input.Windows`.
- A Windows implementation package must not make a sibling implementation
  package transitively visible.

## Project Reference Policy

Project references are allowed inside the aggregate workspace only in the same
direction as package references:

```text
Implementation -> typed abstraction -> core
Implementation -> Windows support -> core
```

Abstractions do not reference implementations. No Input assembly references
Graphics, HTML, DOM, JavaScript, or application projects.

## Compatibility Window

Current Graphics input callbacks remain in place during this Phase 0 slice.
Future migration work may introduce a compatibility adapter, but removal of
`BInputEvents`, `BVirtualKey`, or native input parsing from Graphics requires a
separate approved removal milestone.
