# Phase 5 Decision: Non-Axis-Aligned Transform Semantics

**Decision:** The direct-Canvas backend emulates the CPU renderer's axis-aligned
bounding-box transform semantics.
**Date:** 2026-07-11
**Roadmap:** section 9.2 ("decide transform semantics").

## Context

The roadmap requires choosing one policy for rotation, shear, negative scale, rounded
rectangles, images, and transformed clips before the CPU renderer can be treated as a
direct-Canvas oracle. It lists three options: (a) emulate the existing CPU
bounding-box semantics in the browser backend; (b) define native geometric transforms
as canonical and neutrally fix the CPU renderer; (c) limit initial conformance to
translation/axis-aligned scaling and document the rest.

The current CPU renderer (`BImageRenderer.TransformRect`) transforms the four corners
of a rectangle through `current * pixelScale` and rasterizes their **axis-aligned
bounding box**; corner radii and stroke thickness scale by `CurrentAverageScale`
(the mean of the transform's row lengths). Native Canvas, by contrast, transforms the
geometry itself.

## Decision

Adopt option (a): **emulate CPU bounding-box semantics.** `CanvasTransformPolicy`
reproduces `TransformRect` and `CurrentAverageScale` byte-for-byte, and the planner
bakes every drawing command into an axis-aligned device rectangle before emission.
The replay module therefore draws with an identity Canvas transform and never calls
`setTransform`.

## Rationale

- It keeps the CPU renderer a **pixel-exact oracle for the whole command set**, not
  just a translation/axis-scale subset. The headless conformance suite exploits this:
  a scene under translation and axis-aligned scale is byte-identical between
  `BImageRenderer` and the planned-and-replayed stream.
- It requires **no `Broiler.Graphics` core change**. Option (b) would change the CPU
  renderer's public rasterization behavior for every host (Windows/Linux/CPU),
  needing an ADR and broad re-baselining, for a browser benefit only.
- Broiler's current UI layer issues axis-aligned rectangles under translation and DPR
  scaling; rotation/shear are not on the T2/T3 control path, so exactness there is not
  yet required.

## Consequences and documented differences

- Rotation, shear, and negative scale render as the transformed **bounding box**, not
  true rotated/sheared geometry — identical to the CPU renderer, and **different from
  native Canvas geometric transforms**. This is a deliberate, documented divergence.
- Corner radii and stroke thickness scale by the average uniform scale; non-uniform
  scale is approximate, again matching the CPU renderer.
- Clips are axis-aligned device rectangles (bounding boxes of transformed clip rects),
  intersected across the stack — matching `BCanvas`'s all-clips-must-contain rule.
- If true geometric transforms become a product requirement, revisit as option (b)
  under its own ADR, updating the CPU renderer and this conformance suite together.
  A mixed per-command CPU/Canvas fallback remains out of scope; the whole-frame CPU
  fallback replaces the entire frame, it does not composite per command.
