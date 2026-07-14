# Broiler.Input Phase 2 Deferred External Integration

**Status:** Deferred by user constraint  
**Date:** 2026-07-02

The roadmap includes integration work outside the new component:

- move native message parsing out of `Direct2DWindow`;
- migrate `Broiler.Browser.Windows` to consume Input abstractions directly;
- prove existing browser interaction tests are behaviorally equivalent; and
- remove Graphics native input parsing from the final path.

Those items are not implemented in this slice because the user explicitly
requested that other components not be modified.

The implemented `Broiler.Input` pieces are ready for that later integration:

- `IWindowsInputHost` and `WindowsInputMessageDispatcher` provide the borrowed
  HWND/message-source boundary;
- `WindowsKeyboardInputDevice` and `WindowsMouseInputDevice` own cooked message
  translation;
- `WindowsKeyboardProvider` and `WindowsMouseProvider` own typed discovery,
  opening, Raw Input registration lease entry points, and hot-plug observation;
  and
- `LegacyWindowInputAdapter` exposes existing callback categories without a
  reverse dependency into Graphics.
