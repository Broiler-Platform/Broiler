# Chapter 15 — Fonts

Detailed checklist for CSS 2.1 Chapter 15. This chapter defines properties
for font selection and font matching.

> **Spec file:** [`fonts.html`](fonts.html)

---

## 15.1 Introduction

- [x] Fonts are resources containing glyph representations
- [x] CSS font properties select font families, styles, sizes, and variants
- [x] Font matching is defined algorithmically

## 15.2 Font Matching Algorithm

- [x] Step 1: UA computes each font property's computed value
- [x] Step 2: For each character, UA assembles list of fonts that contain a glyph for that character
- [x] Step 3: Matching by `font-style` — italic/oblique preferred if specified
- [x] Step 4: Matching by `font-variant` — small-caps preferred if specified
- [x] Step 5: Matching by `font-weight` — closest available weight
- [x] Step 6: Matching by `font-size` — must be within UA-dependent tolerance
- [x] If no matching font found, use next font family in the list
- [x] If no match in any family, use UA-dependent default font
- [x] Per-character font fallback (different characters may use different fonts)
- [x] System font fallback for characters not covered by any listed family

## 15.3 Font Family: the 'font-family' Property

- [x] `font-family: [[<family-name> | <generic-family>],]* [<family-name> | <generic-family>]`
- [x] Comma-separated list of font families
- [x] First available font family is used
- [x] Family names with spaces must be quoted
- [x] Family names are case-insensitive

### 15.3.1 Generic Font Families

#### 15.3.1.1 serif

- [x] `serif` — fonts with serifs (e.g., Times New Roman)

#### 15.3.1.2 sans-serif

- [x] `sans-serif` — fonts without serifs (e.g., Arial, Helvetica)

#### 15.3.1.3 cursive

- [x] `cursive` — fonts with joining strokes (e.g., Comic Sans MS)

#### 15.3.1.4 fantasy

- [x] `fantasy` — decorative fonts (e.g., Impact)

#### 15.3.1.5 monospace

- [x] `monospace` — fonts with fixed-width glyphs (e.g., Courier New)
- [x] UAs should use same `em` value for monospace calculations

## 15.4 Font Styling: the 'font-style' Property

- [x] `font-style: normal` — normal upright face (default)
- [x] `font-style: italic` — italic face; if unavailable, oblique
- [x] `font-style: oblique` — oblique (slanted) face; if unavailable, italic
- [x] Inherited: yes

## 15.5 Small-caps: the 'font-variant' Property

- [x] `font-variant: normal` — normal glyphs (default)
- [x] `font-variant: small-caps` — lowercase letters rendered as smaller uppercase
- [x] If no small-caps font available, UA may simulate by scaling uppercase glyphs
- [x] Inherited: yes

## 15.6 Font Boldness: the 'font-weight' Property

- [x] `font-weight: normal` — equivalent to 400
- [x] `font-weight: bold` — equivalent to 700
- [x] `font-weight: bolder` — one step bolder relative to parent
- [x] `font-weight: lighter` — one step lighter relative to parent
- [x] `font-weight: 100` through `font-weight: 900` (nine numeric values)
- [x] Weight mapping: 100=Thin, 200=Extra Light, 300=Light, 400=Normal, 500=Medium, 600=Semi Bold, 700=Bold, 800=Extra Bold, 900=Black
- [x] If exact weight unavailable: for values ≤ 500, prefer lighter then darker; for values ≥ 600, prefer darker then lighter
- [x] `bolder`/`lighter` rounding rules to nearest available weight
- [x] Inherited: yes (computed weight value, not keyword)

## 15.7 Font Size: the 'font-size' Property

- [x] `font-size: <absolute-size>` — keyword sizes mapped to a UA-dependent table
  - [x] `xx-small`, `x-small`, `small`, `medium`, `large`, `x-large`, `xx-large`
  - [x] Scaling factor between adjacent sizes: approximately 1.2
  - [x] `medium` is the UA's default font size
- [x] `font-size: <relative-size>` — relative to parent's font-size
  - [x] `larger` — one step larger in the size table
  - [x] `smaller` — one step smaller in the size table
- [x] `font-size: <length>` — fixed font size (e.g., `16px`, `12pt`)
- [x] `font-size: <percentage>` — percentage of parent's font-size
- [x] Negative font-size values are illegal
- [x] Inherited: yes (computed value)
- [x] `em` and `ex` units on `font-size` refer to parent element's font-size

## 15.8 Shorthand Font Property: 'font'

- [x] `font: [<font-style> || <font-variant> || <font-weight>]? <font-size>[/<line-height>]? <font-family>`
- [x] Omitted values reset to initial values
- [x] `font-size` and `font-family` are required
- [x] `line-height` immediately follows `font-size` with `/` separator
- [x] System font keywords:
  - [x] `caption` — font used for captioned controls
  - [x] `icon` — font used for icon labels
  - [x] `menu` — font used in menus
  - [x] `message-box` — font used in dialog boxes
  - [x] `small-caption` — font used for small controls
  - [x] `status-bar` — font used in status bars
- [x] System font keywords set all font sub-properties at once
- [x] Individual font properties may be altered after setting a system font

---

[← Back to main checklist](css2-specification-checklist.md)
