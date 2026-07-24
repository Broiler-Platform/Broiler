# Broiler.Input Roadmap

**Status:** Active preview. Keyboard/mouse providers exist for Windows and Linux;
Windows microphone and camera capture are implemented; Touch, Pen, and Text
abstractions exist. This file tracks only work that is still open.

## Finish input ownership migration

- Move the remaining native message parsing and authoritative input route out of
  `Broiler.Graphics.Windows.Direct2DWindow`.
- Migrate remaining application and demo users of
  `StandardLegacyGraphicsInputAdapter` to explicit Broiler.Input providers.
- Remove Graphics-owned input callbacks and the legacy adapter only after every
  consumer has equivalent focus, capture, text, and pointer behavior.
- Keep browser DOM event construction and permission policy in the
  browser/application layer; Input owns devices, capture, timing, delivery, and
  faults.

## Complete provider coverage

- Add Windows Touch and Pen providers for the existing neutral contracts,
  including cancellation, capture loss, pressure/tilt capability reporting, DPI
  coordinates, and duplicate compatibility-mouse suppression.
- Decide whether Gamepad enters supported scope. If approved, define a neutral
  state contract and one Windows provider before creating packages.
- Treat Linux text/IME, touchpad policy, gestures, touch, and pen as separately
  approved follow-ups; do not infer support from the current evdev
  keyboard/mouse provider.

## Validate hardware and privacy

- Complete and retain evidence for the opt-in checks in
  [hardware-validation.md](hardware-validation.md).
- Add sustained start/stop, hot-plug, slow-consumer, handle-leak, and latency
  gates for camera, microphone, keyboard, and mouse.
- Supersede the initial buffer/delivery ADR wording where the shipped
  camera/microphone lease contracts are now more specific.

## Stabilize and release

- Review the public API baseline, names, XML documentation, trimming, and AOT
  behavior after real application migration.
- Validate packages from a feed without the aggregate repository and publish
  explicit native/runtime requirements.
- Complete dependency, license, privacy, and human review before stable support
  claims.
