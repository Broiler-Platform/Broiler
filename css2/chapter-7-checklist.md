# Chapter 7 — Media Types

Detailed checklist for CSS 2.1 Chapter 7. This chapter defines media types
that allow style sheets to be tailored for different output devices.

> **Spec file:** [`media.html`](media.html)

---

## 7.1 Introduction to Media Types

- [x] Style sheets can target specific media (screen, print, etc.) — verified: `@media screen` rules apply in screen context (`S7_1_MediaTargeting_ScreenStylesApply`)
- [x] Media-dependent style sheets allow different presentations for different devices — verified: screen vs print produce different styling (`S7_1_MediaDependent_DifferentPresentations`)
- [x] `@media` rule and `@import` with media types — verified: `@media` rule parsed and applied correctly (`S7_1_AtMediaRule_ParsedAndApplied`)

## 7.2 Specifying Media-Dependent Style Sheets

- [x] `@media` rule syntax: `@media type { rules }` — verified: basic `@media` syntax with rule sets inside (`S7_2_AtMediaSyntax_RuleSetsApply`)
- [x] `@import` with media list: `@import url("...") type1, type2` — verified: `@import` with media type parsed without error (`S7_2_AtImport_WithMediaType`)
- [x] `<link>` element `media` attribute — verified: `<link media="screen">` recognised in parsing (`S7_2_LinkMedia_AttributeRecognised`)
- [x] `<style>` element `media` attribute — verified: `<style media="screen">` applies rules (`S7_2_StyleMedia_AttributeApplied`)
- [x] `<?xml-stylesheet?>` PI `media` attribute — verified: not applicable to HTML-only renderer; no-op confirmed (`S7_2_XmlStylesheet_NotApplicable`)

### 7.2.1 The @media Rule

- [x] `@media` rule contains rule sets conditional on media type — verified: rules inside `@media screen` block apply (`S7_2_1_ConditionalRuleSets_ScreenApplies`)
- [x] Comma-separated media type list — verified: `@media screen, print` applies in screen context (`S7_2_1_CommaSeparated_MultipleTypes`)
- [x] Case-insensitive media type names — verified: `@media SCREEN`, `@media Screen` both apply (`S7_2_1_CaseInsensitive_MediaNames`)
- [x] `@media` rules may not be nested — verified: nested `@media` blocks handled gracefully; inner rules still functional (`S7_2_1_NoNesting_GracefulHandling`)

## 7.3 Recognized Media Types

- [x] `all` — suitable for all devices — verified: `@media all` rules apply (`S7_3_All_AppliesToAllDevices`)
- [x] `aural` — speech synthesizers (deprecated in favor of `speech`) — verified: `@media aural` does not apply in visual context (`S7_3_Aural_NotAppliedInVisual`)
- [x] `braille` — braille tactile feedback devices — verified: `@media braille` does not apply in screen context (`S7_3_Braille_NotAppliedInScreen`)
- [x] `embossed` — paged braille printers — verified: `@media embossed` does not apply in screen context (`S7_3_Embossed_NotAppliedInScreen`)
- [x] `handheld` — handheld devices (small screen, limited bandwidth) — verified: `@media handheld` does not apply in screen context (`S7_3_Handheld_NotAppliedInScreen`)
- [x] `print` — paged opaque material and print preview — verified: `@media print` does not apply in screen context (`S7_3_Print_NotAppliedInScreen`)
- [x] `projection` — projected presentations — verified: `@media projection` does not apply in screen context (`S7_3_Projection_NotAppliedInScreen`)
- [x] `screen` — color computer screens — verified: `@media screen` rules apply to rendered output (`S7_3_Screen_AppliesInScreenContext`)
- [x] `speech` — speech synthesizers — verified: `@media speech` does not apply in screen context (`S7_3_Speech_NotAppliedInScreen`)
- [x] `tty` — media using fixed-pitch character grid — verified: `@media tty` does not apply in screen context (`S7_3_Tty_NotAppliedInScreen`)
- [x] `tv` — television-type devices — verified: `@media tv` does not apply in screen context (`S7_3_Tv_NotAppliedInScreen`)
- [x] Unknown media types must be treated as not matching — verified: `@media nonexistent` rules are ignored (`S7_3_UnknownType_NotMatching`)

### 7.3.1 Media Groups

- [x] Media group: continuous vs paged — verified: continuous-media properties (overflow, scroll) apply; paged-media properties (page-break) are available (`S7_3_1_ContinuousVsPaged_ContinuousApplies`)
- [x] Media group: visual vs aural vs tactile — verified: visual properties (color, background, border) apply in screen context (`S7_3_1_VisualVsAural_VisualApplies`)
- [x] Media group: grid vs bitmap — verified: bitmap rendering produces pixel output (not character grid) (`S7_3_1_GridVsBitmap_BitmapRendering`)
- [x] Media group: interactive vs static — verified: interactive features (cursor, hover) available in rendered context (`S7_3_1_InteractiveVsStatic_InteractiveAvailable`)
- [x] Properties applicable per media group (property definitions table) — verified: visual-group properties (width, height, color, border) all apply (`S7_3_1_PropertyDefinitions_VisualGroupApplies`)

---

[← Back to main checklist](css2-specification-checklist.md)
