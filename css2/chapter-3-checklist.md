# Chapter 3 — Conformance: Requirements and Recommendations

Detailed checklist for CSS 2.1 Chapter 3. This chapter defines conformance
requirements for CSS 2.1 user agents.

> **Spec file:** [`conform.html`](conform.html)

---

## 3.1 Definitions

- [x] Definition of "style sheet" (set of statements)
- [x] Definition of "valid style sheet"
- [x] Definition of "source document"
- [x] Definition of "document language" (e.g., HTML, XML)
- [x] Definition of "user agent" (UA) — program that interprets documents
- [x] Definition of "author", "user", and "user agent" origins
- [x] Definition of "property" and "value"
- [x] Definition of "element" and "replaced element"
- [x] Definition of "intrinsic dimensions" for replaced elements
- [x] Definition of "attribute" and "content"
- [x] Definition of "rendered content" and "document tree"
- [x] Definition of "ignore" (parsing behavior for invalid/unsupported rules)

## 3.2 UA Conformance

- [x] Must parse style sheets as defined in the specification
- [x] Must assign to every element every property defined in the spec
- [x] Must support all required media types
- [x] Must correctly cascade and inherit values
- [x] Must recognize all valid CSS 2.1 selectors
- [x] Must implement all property value computations correctly
- [x] May use approximations for actual values (e.g., rounding)
- [x] Must allow user style sheets
- [x] May limit resource usage (e.g., memory)
- [x] Must not handle CSS as a programming language

## 3.3 Error Conditions

- [x] Must handle invalid style sheets gracefully
- [x] Must use forward-compatible parsing for unknown at-rules
- [x] Must ignore unknown properties
- [x] Must ignore illegal values for known properties
- [x] Must ignore malformed declarations

## 3.4 The text/css Content Type

- [x] Recognize the `text/css` MIME type
- [x] `@charset` rule for encoding declaration
- [x] Encoding resolution order: BOM → `@charset` → protocol → `<link>` charset → document encoding → UTF-8

---

**Verification notes:**
- All items verified with tests in `Css2Chapter3Tests.cs` (30 tests).
- Conformance items verified through rendering pipeline: style sheet parsing,
  cascading, selector recognition, property computation, error handling, and
  encoding support all tested against the html-renderer.

[← Back to main checklist](css2-specification-checklist.md)
