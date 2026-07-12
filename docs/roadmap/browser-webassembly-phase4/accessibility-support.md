# Phase 4 Accessibility Support Statement

## Supported baseline

- The selected canvas workflow has nine stable DOM semantic controls.
- Button, Edit, password Edit, RTL Edit, Slider, ScrollView, ListView, Menu, and
  ImageView have application-owned accessible labels and focus mapping.
- The selected workflow is keyboard-operable from either the canvas or mirrored
  DOM controls.
- Managed focus and DOM focus are synchronized after frame presentation.
- Password values are redacted from semantics and diagnostics.
- Reduced-motion preference is captured and exposed to application policy.

## Not yet certified

- This is not a general `UiElement` tab-order/action framework.
- Canvas geometry is not exposed as a complete platform accessibility tree.
- Live-region announcements, high-contrast visual certification, touch screen
  reader gestures, braille displays, and switch control are not tested.
- Actual native IME candidate-window placement is not automated.
- NVDA, JAWS, Narrator, VoiceOver, TalkBack, and browser-specific combinations
  require manual evidence before being claimed.

## Evidence matrix

| Combination | Automated semantic/keyboard gate | Manual screen-reader gate |
|---|---|---|
| Chromium / Windows | Implemented; committed CI pending | NVDA run pending |
| Firefox / Windows or Linux CI | Implemented; committed CI pending | Manual run pending |
| Safari / macOS | Not in current CI | VoiceOver run pending |

The application is therefore described as having an actionable accessibility
baseline for the selected workflow, not generalized WCAG or screen-reader
conformance.
