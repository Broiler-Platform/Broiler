# Roadmaps

This directory contains development roadmaps for the Broiler project. Each
roadmap defines phased goals, tasks, and acceptance criteria for a specific
area of work.

## Index

| Roadmap | Status | Summary |
|---------|--------|---------|
| [Acid1 Error Resolution](acid1-error-resolution.md) | Complete | Fix all rendering discrepancies between Broiler and Chromium for the Acid1 CSS1 test |
| [CLI Website Capture](cli-website-capture.md) | Complete | Deliver a cross-platform CLI tool that captures website content using local engines |
| [CSS2 Differential Resolution](css2-differential-resolution.md) | Active | Fix rendering differences between html-renderer and headless Chromium |
| [CSS2 Verification Report Resolution](css2-verification-report-resolution.md) | Active | Address every issue identified in the CSS2 verification report |
| [Documentation & Testcase Refactor](documentation-testcase-refactor.md) | Active | Restructure documentation and refactor tests for clarity and rendering focus |
| [.NET Standard Type Replacement](dotnet-standard-type-replacement.md) | Complete | Replace custom types in HTML-Renderer with .NET standard library equivalents |
| [HTML & JS Engine](html-js-engine.md) | Active | Evolve HTML-Renderer and YantraJS into a production-grade HTML/JS engine |
| [W3C HTML Compliance](w3c-html-compliance.md) | Active | Advance HTML-Renderer to full W3C HTML compliance |

## Cross-References

Several roadmaps are closely related:

- **CSS2 cluster** — [CSS2 Differential Resolution](css2-differential-resolution.md)
  and [CSS2 Verification Report Resolution](css2-verification-report-resolution.md)
  both feed into the CSS2 compliance effort tracked in the
  [verification report](../css2-verification-report.md).
- **Testing & documentation** — [Documentation & Testcase Refactor](documentation-testcase-refactor.md)
  complements the [testing roadmap](../testing-roadmap.md) by addressing
  organisation and rendering focus rather than pipeline-layer coverage.
- **Architecture** — [.NET Standard Type Replacement](dotnet-standard-type-replacement.md)
  and the [architecture roadmap](../architecture-roadmap.md) both target
  codebase modernisation and engine separation.
- **Compliance** — [W3C HTML Compliance](w3c-html-compliance.md) and the
  [HTML & JS Engine](html-js-engine.md) roadmap share standards-compliance
  goals, with the former scoped to HTML-Renderer and the latter spanning both
  engines.
