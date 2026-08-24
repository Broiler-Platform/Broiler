# Broiler.UI Roadmap

**Status:** Active preview. The retained-mode foundation, standard control
families, RichEdit, Formatting Codes view, component directory topology, and
preview packages exist. This file replaces completed phase records with the work
that is still open.

## Remove temporary host and Graphics integration

- Migrate remaining Writer/demo users of `StandardLegacyGraphicsInputAdapter` to
  explicit Broiler.Input providers.
- Remove the application dependency on Graphics-owned `BControl`,
  `BButtonControl`, `BEditControl`, `BLabelControl`, and `BControlOptions` after
  all consumers have equivalent managed-control behavior.
- Narrow `BWindow`/`Direct2DWindow` to graphics hosting and presentation after
  input and control migration gates pass.
- Preserve browser-content input routing separately from application chrome
  routing.

## Host parity and review

- Produce evidence for Windows IME candidate placement, clipboard, cursor,
  drag/drop, accessibility bridge, screen-reader, keyboard-only, high-contrast,
  text-scale, reduced-motion, and RTL behavior.
- ~~Decide and document whether secondary logical windows may map to native
  top-level windows.~~ Decided: they may *break out* into their own native
  top-level window via the `IUiWindowHost` host capability (one-way, modality
  preserved), without exposing a native handle. See ADR
  [0025](adr/0025-host-window-breakout.md).
- Replace the pending Phase-0-era human review with a review of a named current
  revision before expanding the preview claim.

## Touch input and gestures

Required by the planned Android applications, but none of it is Android-specific:
Windows and Linux touch providers would need the same neutral behavior. The
sequencing and exit gates are in
[the root roadmap](../../docs/ROADMAP.md#a3--touch-first-interaction-in-broilerui).

- Carry contact identity and phase through `UiInputEvent`.
  `FromTouchContact` currently keeps only the position and discards `ContactId`,
  `TouchContactState`, and `Pressure`; `FromPenContact` discards the same, so no
  control can distinguish a press from a release or see a second contact.
- Add one shared gesture recognizer over neutral contact streams — tap,
  double-tap, long-press, drag, fling with momentum, and pinch — consumed by
  every control instead of being reimplemented per control or per platform
  backend.
- Give `StandardScrollView` content-drag scrolling, fling with deceleration,
  overscroll, and scroll-chaining. Its pointer path currently requires
  `MouseButton.Left` and only drags the scrollbar thumb or track, so a
  touch-derived event scrolls nothing.
- Add touch-target minimum sizes and hit slop to the token work below, plus
  long-press context activation.
- Add selection and caret handles and a text-selection model that does not
  depend on a hover state, for `Edit` and `RichEdit`.
- Consume host-published window insets so content reflows around the soft
  keyboard, system bars, and display cutouts, and keep the focused caret visible
  when the keyboard opens.

## Editor-side text input contract

- Extend the text-input host seam beyond `PublishCaret`/`ClearCaret` so a real
  IME can be satisfied: text around the cursor, the current selection,
  composing-region set and clear, and commit or replace. Android's
  `InputConnection` is the immediate driver, but Windows TSF and browser
  composition need the same two-way protocol, so it belongs here rather than in
  any one platform backend.
- Drive soft-keyboard visibility, keyboard type, and the IME action from editor
  focus through the host, without the editor knowing the platform.

## Design-system and UX conformance

- Finish token enforcement: CI contrast coverage, raw-color/size linting,
  explicit override behavior, and text-scale application.
- Implement consistent visual states, focus-visible policy, tab traversal,
  modal focus trapping, composite navigation, and minimum target sizes.
- Add typography, spacing, density, and motion tokens with deterministic
  reduced-motion behavior.
- Complete semantic relationships and live regions, automated accessibility
  checks, screen-reader scripts, pseudo-localization, bidi/RTL, and fractional
  DPI/reflow tests.
- Publish the design-system, interaction, content, accessibility, and
  per-control maturity references after the behavior is enforceable.

## RichEdit and Formatting Codes

- Render paragraph alignment, lists, and indentation consistently with the
  document model and Formatting Codes projection.
- Complete optional rich HTML/RTF host integration without adding DOM/codecs to
  the core RichEdit assemblies.
- Add formatting-aware accessibility evidence, bidi/RTL and IME host tests,
  incremental/visible-range layout where measurements require it, large-document
  benchmarks, and operation fuzzing.
- Make an explicit go/no-go decision for advanced textual Formatting Codes
  source editing; keep the shipped structured editor canonical and safe by
  default.

## Stabilization and release

- Freeze public names and XML documentation after application consumer review.
- Run performance, leak, fuzz, accessibility, localization, DPI, IME, and
  long-duration soak gates.
- Validate independent package consumption and non-Windows builds.
- Complete dependency, license, API, and attributable human review before a
  stable release.

## Pointer events carry modifier state

`Broiler.Input`'s `MouseButtonEvent`, `MouseMoveEvent` and `MouseWheelEvent` carry an
`InputModifiers` value, and `UiInputEvent.FromMouse*` passes it through, so a control
can tell a Ctrl-click or Shift-click from a plain one.

`InputModifiers` lives in the root `Broiler.Input` assembly rather than on the
keyboard package: every device abstraction depends on the root and none depend on
each other (Broiler.Input ADR 0001), and modifier state belongs to a chord rather than
to a keyboard. It mirrors `KeyboardModifierState` member for member so the UI layer
can cast between them in one place; `Broiler.Input.Contract.Tests` pins the two
layouts against each other.

What populates it varies by platform, and consumers should not assume it is complete:

- **Windows** fills Shift and Ctrl straight from the mouse message's `wParam`. Alt is
  *not* there — Windows delivers it as `WM_SYSCOMMAND` — so an Alt-click reports no
  modifier.
- **Linux/evdev** cannot fill it in the mouse backend at all, because the mouse and
  the keyboard are separate devices. The coordinator that merges both streams stamps
  the last-seen keyboard modifiers instead (see `LinuxInputCoordinator`).
- **Touch** contacts arrive as synthesized pointer presses and never carry modifiers,
  which is why `StandardListView` toggles on an unmodified click rather than requiring
  Ctrl to accumulate — the convention would put multi-selection out of touch's reach.

Pen and touch events do not carry modifiers yet; adding them is the same shape of
change as this one.
