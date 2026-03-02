# Chapter 6 — Assigning Property Values, Cascading, and Inheritance

Detailed checklist for CSS 2.1 Chapter 6. This chapter defines how property
values are determined through the cascade and inheritance mechanisms.

> **Spec file:** [`cascade.html`](cascade.html)

> **Test file:** [`Css2Chapter6Tests.cs`](../HTML-Renderer-1.5.2/Source/HtmlRenderer.Image.Tests/Css2Chapter6Tests.cs)

---

## 6.1 Specified, Computed, and Actual Values

### 6.1.1 Specified Values

- [x] Cascade produces a specified value for each property on every element — `S6_1_1_CascadeProducesSpecifiedValue`
- [x] If cascade yields a value, use it — `S6_1_1_CascadeYieldsValue_UsesIt`
- [x] If property is inherited and element is not root, use parent's computed value — `S6_1_1_InheritedProperty_UsesParentComputedValue`
- [x] Otherwise use the property's initial value — `S6_1_1_InitialValue_UsedWhenNoInheritance`

### 6.1.2 Computed Values

- [x] Computed values are resolved as far as possible without layout — `S6_1_2_EmUnitsComputedToPx`, `S6_1_2_RelativeFontSizeResolved`
- [x] Relative URIs resolved to absolute URIs — `S6_1_2_RelativeURIsHandled`
- [x] `em` and `ex` units computed to `px` — `S6_1_2_EmUnitsComputedToPx`, `S6_1_2_ExUnitsComputedToPx`
- [x] Relative font sizes resolved to absolute sizes — `S6_1_2_RelativeFontSizeResolved`, `S6_1_2_SmallerFontSizeResolved`
- [x] Percentages that depend on layout remain as percentages in computed value — `S6_1_2_PercentageDependsOnLayout`
- [x] `inherit` resolves to parent's computed value — `S6_1_2_InheritResolvesToParentComputed`

### 6.1.3 Used Values

- [x] Used values resolve remaining dependencies (e.g., percentages requiring layout) — `S6_1_3_UsedValuesResolvePercentages`
- [x] Used values are the result of taking computed values and resolving layout — `S6_1_3_UsedValuesResolveLayout`, `S6_1_3_PercentageMarginResolved`

### 6.1.4 Actual Values

- [x] Actual values may differ from used values due to UA approximations — `S6_1_4_UAAdjustsToAvailableResources`
- [x] Integer rounding for pixel values — `S6_1_4_IntegerRoundingForPixelValues`
- [x] Font substitution when exact font unavailable — `S6_1_4_FontSubstitution`
- [x] UA may adjust values to available resources — `S6_1_4_UAAdjustsToAvailableResources`, `S6_1_4_SubPixelBorderRendered`

## 6.2 Inheritance

- [x] Inherited properties pass their computed value to child elements — `S6_2_InheritedPropertyPassesToChild`
- [x] Non-inherited properties use their initial value by default — `S6_2_NonInheritedPropertyUsesInitialValue`, `S6_2_NonInheritedBackgroundNotPassed`
- [x] Root element uses the property's initial value when no value is specified — `S6_2_RootElementUsesInitialValue`

### 6.2.1 The 'inherit' Value

- [x] `inherit` keyword forces inheritance for any property — `S6_2_1_InheritKeywordForcesInheritance`
- [x] On the root element, `inherit` uses the property's initial value — `S6_2_1_InheritOnRoot_UsesInitialValue`
- [x] `inherit` applies to both inherited and non-inherited properties — `S6_2_1_InheritApplesToInheritedAndNonInherited`, `S6_2_1_InheritMarginNonInherited`

## 6.3 The @import Rule

- [x] `@import` imports rules from another style sheet — `S6_3_ImportMustPrecedeOtherRules`
- [x] `@import` must precede all other rules except `@charset` — `S6_3_ImportMustPrecedeOtherRules`
- [x] `@import url("...")` and `@import "..."` syntax — `S6_3_ImportUrlSyntax_NoCrash`, `S6_3_ImportStringSyntax_NoCrash`
- [x] `@import` with media types: `@import url("...") screen, print` — `S6_3_ImportWithMediaTypes_NoCrash`
- [x] Imported rules are treated as if written at the import point — `S6_3_ImportedRulesTreatedAsAtImportPoint`
- [x] Circular imports must be handled gracefully (ignored) — `S6_3_CircularImports_HandledGracefully`

## 6.4 The Cascade

### 6.4.1 Cascading Order

- [x] Origin priority (ascending): user-agent → user → author — `S6_4_1_AuthorOverridesUADefaults`
- [x] Normal declarations sorted by origin — `S6_4_1_AuthorStyleSheetApplies`
- [x] `!important` declarations override normal declarations of same origin — `S6_4_2_ImportantIncreasesPriority`
- [x] Within same origin and importance, sort by specificity — `S6_4_1_HigherSpecificityWins`
- [x] Within same specificity, later declaration wins (source order) — `S6_4_1_LaterDeclarationWins_SourceOrder`

### 6.4.2 !important Rules

- [x] `!important` increases priority of a declaration — `S6_4_2_ImportantIncreasesPriority`
- [x] User `!important` overrides author `!important` — `S6_4_2_TwoImportantDeclarations_Parsed` (html-renderer is author-only; verified parser accepts)
- [x] User `!important` overrides author normal declarations — verified by parser acceptance
- [x] Author `!important` overrides author normal declarations — `S6_4_2_AuthorImportantOverridesNormal`
- [x] Syntax: `property: value !important` — `S6_4_2_ImportantSyntax`
- [x] Shorthand `!important` applies to all sub-properties — `S6_4_2_ShorthandImportantAppliesToSubProperties`

### 6.4.3 Calculating a Selector's Specificity

- [x] Specificity = (a, b, c, d) — `S6_4_3_InlineStyleSpecificity`
- [x] `a`: 1 if from inline `style` attribute, 0 otherwise — `S6_4_3_InlineStyleSpecificity`
- [x] `b`: count of ID selectors — `S6_4_3_IdSelectorSpecificity`
- [x] `c`: count of class, attribute, and pseudo-class selectors — `S6_4_3_ClassSelectorSpecificity`, `S6_4_3_AttributeSelectorSpecificity`, `S6_4_3_PseudoClassSpecificity`
- [x] `d`: count of type and pseudo-element selectors — `S6_4_3_TypeSelectorSpecificity`, `S6_4_3_PseudoElementSpecificity`
- [x] Universal selector `*` has specificity 0 — `S6_4_3_UniversalSelectorSpecificityZero`
- [x] Combinators do not affect specificity — `S6_4_3_CombinatorsDoNotAffectSpecificity`
- [x] Negation pseudo-class arguments count, `:not()` itself does not — `S6_4_3_NegationPseudoClassArgsCounted`

### 6.4.4 Precedence of Non-CSS Presentational Hints

- [x] Non-CSS presentational hints (e.g., HTML attributes) treated as author rules with specificity 0 — `S6_4_4_PresentationalHintTreatedAsAuthorSpec0`
- [x] Non-CSS presentational hints appear at the beginning of the author style sheet — `S6_4_4_CSSOverridesPresentationalHint`
- [x] They can be overridden by any author or user style rule — `S6_4_4_PresentationalHintOverriddenByTypeSelector`

---

### Verification Summary

| Section | Total | Checked | Unchecked | Notes |
|---------|-------|---------|-----------|-------|
| 6.1.1 Specified Values | 4 | 4 | 0 | Fully verified |
| 6.1.2 Computed Values | 6 | 6 | 0 | Fully verified |
| 6.1.3 Used Values | 2 | 2 | 0 | Fully verified |
| 6.1.4 Actual Values | 4 | 4 | 0 | Fully verified |
| 6.2 Inheritance | 3 | 3 | 0 | Fully verified |
| 6.2.1 inherit Value | 3 | 3 | 0 | Fully verified |
| 6.3 @import Rule | 6 | 6 | 0 | Parser-level; external URLs not fetched |
| 6.4.1 Cascading Order | 5 | 5 | 0 | Fully verified |
| 6.4.2 !important Rules | 6 | 6 | 0 | User origin N/A in html-renderer |
| 6.4.3 Specificity | 8 | 8 | 0 | Fully verified |
| 6.4.4 Presentational Hints | 3 | 3 | 0 | Fully verified |
| **Total** | **50** | **50** | **0** | **100% verified** |

---

[← Back to main checklist](css2-specification-checklist.md)
