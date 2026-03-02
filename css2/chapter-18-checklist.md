# Chapter 18 — User Interface

Detailed checklist for CSS 2.1 Chapter 18. This chapter defines properties for
cursors, system colors, user font preferences, and outlines.

> **Spec file:** [`ui.html`](ui.html)

---

## 18.1 Cursors: the 'cursor' Property

- [x] `cursor: auto` — UA determines cursor (default)
- [x] `cursor: crosshair` — crosshair cursor
- [x] `cursor: default` — platform-dependent default cursor (usually an arrow)
- [x] `cursor: pointer` — pointer indicating a link
- [x] `cursor: move` — indicates something is to be moved
- [x] `cursor: e-resize` — east resize
- [x] `cursor: ne-resize` — northeast resize
- [x] `cursor: nw-resize` — northwest resize
- [x] `cursor: n-resize` — north resize
- [x] `cursor: se-resize` — southeast resize
- [x] `cursor: sw-resize` — southwest resize
- [x] `cursor: s-resize` — south resize
- [x] `cursor: w-resize` — west resize
- [x] `cursor: text` — text selection cursor (I-beam)
- [x] `cursor: wait` — program is busy
- [x] `cursor: help` — help is available
- [x] `cursor: progress` — program is busy but user can still interact
- [x] `cursor: <uri>` — custom cursor image
- [x] Comma-separated fallback list: `cursor: url(custom.cur), pointer`
- [x] Inherited: yes
- [x] Applies to all elements

## 18.2 System Colors

- [x] `ActiveBorder` — active window border
- [x] `ActiveCaption` — active window caption
- [x] `AppWorkspace` — MDI background color
- [x] `Background` — desktop background
- [x] `ButtonFace` — button face color
- [x] `ButtonHighlight` — button highlight
- [x] `ButtonShadow` — button shadow
- [x] `ButtonText` — button text color
- [x] `CaptionText` — caption text
- [x] `GrayText` — grayed-out text
- [x] `Highlight` — selected item background
- [x] `HighlightText` — selected item text
- [x] `InactiveBorder` — inactive window border
- [x] `InactiveCaption` — inactive window caption
- [x] `InactiveCaptionText` — inactive caption text
- [x] `InfoBackground` — tooltip background
- [x] `InfoText` — tooltip text
- [x] `Menu` — menu background
- [x] `MenuText` — menu text
- [x] `Scrollbar` — scrollbar track color
- [x] `ThreeDDarkShadow` — dark shadow for 3D elements
- [x] `ThreeDFace` — face color for 3D elements
- [x] `ThreeDHighlight` — highlight for 3D elements
- [x] `ThreeDLightShadow` — light shadow for 3D elements
- [x] `ThreeDShadow` — shadow for 3D elements
- [x] `Window` — window background
- [x] `WindowFrame` — window frame
- [x] `WindowText` — window text
- [x] System colors are deprecated in CSS3 but required in CSS 2.1
- [x] Case-insensitive system color keywords

## 18.3 User Preferences for Fonts

- [x] UAs should allow users to configure default fonts
- [x] Author styles may override user font preferences
- [x] System font keywords (`caption`, `icon`, etc.) use system font settings

## 18.4 Dynamic Outlines: the 'outline' Property

- [x] `outline-color: <color> | invert` — outline color
  - [x] `invert` — pixel inversion for visibility on any background
  - [x] UAs that do not support `invert` use initial value (typically `color` property value)
  - [x] Initial value: `invert`
- [x] `outline-style: <border-style> | auto` — outline style
  - [x] Same values as `border-style` (except no `hidden`)
  - [x] `auto` — UA-defined outline style
  - [x] Initial value: `none`
- [x] `outline-width: <border-width>` — outline width
  - [x] Same values as `border-width` (`thin`, `medium`, `thick`, or `<length>`)
  - [x] Initial value: `medium`
  - [x] Computed to 0 if `outline-style` is `none`
- [x] `outline` shorthand — `outline-color`, `outline-style`, `outline-width`
- [x] Outlines do not take up space (drawn over the box)
- [x] Outlines may be non-rectangular (follow element shape)
- [x] Outlines do not affect layout
- [x] Not inherited
- [x] Applies to all elements

### 18.4.1 Outlines and the Focus

- [x] UAs should draw outlines on focused elements (`:focus`)
- [x] Outlines provide visual indication of focus for accessibility
- [x] Authors should not remove outlines without providing alternative focus indicators

## 18.5 Magnification

- [x] UAs may provide magnification/zoom
- [x] Zoom should scale the pixel reference unit
- [x] Magnification is not a CSS property but a UA feature

---

[← Back to main checklist](css2-specification-checklist.md)
