# Chapter 2 — Introduction to CSS 2.1

Detailed checklist for CSS 2.1 Chapter 2. This chapter provides a tutorial
introduction and describes the CSS 2.1 processing model.

> **Spec file:** [`intro.html`](intro.html)

---

## 2.1 A Brief CSS 2.1 Tutorial for HTML

- [x] Understand how CSS rules are applied to HTML documents — verified: CSS rules style HTML elements (`S2_1_CssRules_AppliedToHtml`)
- [x] Linking style sheets via `<link>` element — verified: `<link>` element recognised in markup (`S2_1_LinkElement_StyleSheetLinked`)
- [x] Embedding styles via `<style>` element — verified: `<style>` block applies rules to elements (`S2_1_StyleElement_EmbeddedStyles`)
- [x] Inline styles via `style` attribute — verified: inline `style` attribute overrides other sources (`S2_1_InlineStyle_AttributeApplied`)
- [x] Grouping and inheritance examples — verified: grouped selectors apply same styles; inheritance propagates (`S2_1_Grouping_GroupedSelectorsApply`, `S2_1_Inheritance_ColorPropagates`)

## 2.2 A Brief CSS 2.1 Tutorial for XML

- [x] CSS applied to arbitrary XML documents — verified: custom/unknown elements can be styled with `display` and CSS properties (`S2_2_XmlDocuments_CustomElementsStyled`)
- [x] Styling elements without default presentation semantics — verified: elements without UA defaults are styled via explicit CSS (`S2_2_NoDefaultSemantics_ExplicitStyleApplied`)

## 2.3 The CSS 2.1 Processing Model

### 2.3.1 The Canvas

- [x] Definition of the canvas (rendering surface) — verified: rendering produces a bitmap surface (`S2_3_1_Canvas_RenderingSurfaceProduced`)
- [x] Canvas dimensions and infinite extent — verified: content can extend beyond initial viewport; layout is still computed (`S2_3_1_Canvas_DimensionsAndExtent`)
- [x] Canvas background propagation from root element — verified: body background propagates to canvas (`S2_3_1_Canvas_BackgroundPropagation`)

### 2.3.2 CSS 2.1 Addressing Model

- [x] Source document parsed into a document tree — verified: HTML is parsed into a document/fragment tree (`S2_3_2_DocumentTree_HtmlParsed`)
- [x] CSS selectors address elements in the document tree — verified: class, ID, and type selectors address elements correctly (`S2_3_2_Selectors_AddressElements`)
- [x] Processing model: parse → apply styles → layout → render — verified: full pipeline from HTML string to rendered bitmap (`S2_3_2_ProcessingModel_FullPipeline`)

## 2.4 CSS Design Principles

- [x] Forward and backward compatibility — verified: unknown properties are ignored; valid properties still apply (`S2_4_ForwardCompatibility_UnknownPropertiesIgnored`)
- [x] Complementary to structured documents — verified: CSS enhances HTML structure without altering it (`S2_4_ComplementaryToStructure_HtmlPreserved`)
- [x] Vendor, platform, and device independence — verified: standard CSS properties render consistently (`S2_4_DeviceIndependence_StandardPropertiesWork`)
- [x] Maintainability — verified: centralised style blocks can be updated independently (`S2_4_Maintainability_CentralisedStyles`)
- [x] Simplicity — verified: a single property declaration styles an element (`S2_4_Simplicity_SinglePropertyWorks`)
- [x] Network performance — verified: compact CSS notation produces correct output (`S2_4_NetworkPerformance_CompactNotation`)
- [x] Flexibility — verified: multiple styling methods (inline, block, class, id) all work (`S2_4_Flexibility_MultipleMethods`)
- [x] Richness — verified: diverse properties (color, border, margin, padding, font) all apply (`S2_4_Richness_DiverseProperties`)
- [x] Alternative language bindings — verified: CSS not tied to HTML; can style generic elements (`S2_4_AlternativeBindings_GenericElements`)
- [x] Accessibility — verified: semantic HTML elements receive default styling; visibility can be controlled (`S2_4_Accessibility_SemanticElements`)

---

[← Back to main checklist](css2-specification-checklist.md)
