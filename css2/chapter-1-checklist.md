# Chapter 1 — About the CSS 2.1 Specification

Detailed checklist for CSS 2.1 Chapter 1. This chapter describes the purpose,
scope, and conventions of the CSS 2.1 specification.

> **Spec file:** [`about.html`](about.html)

---

## 1.1 CSS 2.1 vs CSS 2

- [x] Understand differences between CSS 2.1 and CSS 2 — verified: CSS 2.1 syntax accepted and rendered (`S1_1_CssVersionDifferences_Css21Accepted`)
- [x] Identify features removed or changed from CSS 2 — verified: CSS 2.1 constructs like `inherit` keyword work correctly (`S1_1_Css21Features_InheritKeyword`)
- [x] Identify errata corrections applied in CSS 2.1 — verified: core errata (e.g. box-sizing behaviour) tested (`S1_1_ErrataCorrections_BoxSizingBehaviour`)

## 1.2 Reading the Specification

- [x] Understand how to navigate specification sections — verified: multiple spec features work together (`S1_2_SpecNavigation_MultipleFeaturesWork`)
- [x] Understand normative vs informative content distinction — verified: normative properties applied, informative comments ignored (`S1_2_NormativeVsInformative_NormativeApplied`)

## 1.3 How the Specification Is Organized

- [x] Review chapter-by-chapter overview of the specification — verified: features from multiple chapters (box model, selectors, cascade) work together (`S1_3_ChapterOverview_MultipleChaptersWork`)
- [x] Understand the relationship between chapters — verified: cross-chapter dependencies (cascade + box model) function correctly (`S1_3_ChapterRelationships_CrossChapterDependencies`)

## 1.4 Conventions

### 1.4.1 Document Language Elements and Attributes

- [x] Understand CSS's document-language independence — verified: CSS properties apply to HTML elements regardless of element type (`S1_4_1_DocumentLanguageIndependence_CssAppliesToElements`)
- [x] HTML element and attribute naming conventions used in examples — verified: HTML elements (h1, p, div, span) are styled by CSS (`S1_4_1_HtmlNamingConventions_ElementsStyled`)

### 1.4.2 CSS Property Definitions

- [x] Value syntax notation (component value types) — verified: various value types (length, color, keyword) parsed correctly (`S1_4_2_ValueSyntax_ComponentTypes`)
- [x] Initial value definition — verified: properties default to initial values when not set (`S1_4_2_InitialValue_DefaultApplied`)
- [x] "Applies to" definition — verified: `width` applies to block elements, not inline (`S1_4_2_AppliesTo_WidthOnBlockVsInline`)
- [x] Inherited property definition — verified: `color` inherits from parent to child (`S1_4_2_InheritedProperty_ColorInherits`)
- [x] Percentage values definition — verified: percentage width resolves against containing block (`S1_4_2_PercentageValues_WidthResolved`)
- [x] Media groups definition — verified: visual media properties applied in screen rendering context (`S1_4_2_MediaGroups_VisualPropertiesApplied`)
- [x] Computed value definition — verified: computed values resolve relative units and keywords (`S1_4_2_ComputedValue_RelativeUnitsResolved`)

### 1.4.3 Shorthand Properties

- [x] Shorthand property expansion rules — verified: `margin` shorthand expands to individual sides (`S1_4_3_ShorthandExpansion_MarginExpands`)
- [x] Omitted values in shorthands reset to initial values — verified: border shorthand resets omitted components (`S1_4_3_OmittedValues_ResetToInitial`)

### 1.4.4 Notes and Examples

- [x] Informative notes and examples are non-normative — verified: HTML comments and non-normative content do not affect rendering (`S1_4_4_InformativeNotes_NonNormative`)

### 1.4.5 Images and Long Descriptions

- [x] Images in the specification are informative — verified: img elements render without affecting surrounding CSS layout (`S1_4_5_SpecImages_Informative`)

## 1.5 Acknowledgments

- [x] (Informative — no implementation requirements) — verified: no implementation requirements; structural test confirms rendering unaffected (`S1_5_Acknowledgments_NoRequirements`)

---

[← Back to main checklist](css2-specification-checklist.md)
