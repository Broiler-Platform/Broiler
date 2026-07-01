# Human review summary: Broiler preview

> **Status: SUMMARY ONLY - not a root-level human approval.**

This file aggregates the human-review records found in subdirectories for the first
Broiler preview. It is intentionally a summary and does not replace the individual
component review files, reviewer attestations, conditions, or pending-review warnings.

Source files included:

- `Broiler.CSS/HUMAN_REVIEW.md`
- `Broiler.CSS/Broiler.DOM/HUMAN_REVIEW.md`
- `Broiler.DOM/HUMAN_REVIEW.md`
- `Broiler.Graphics/HUMAN_REVIEW.md`
- `Broiler.HTML/HUMAN_REVIEW.md`
- `Broiler.HTML/Broiler.Graphics/HUMAN_REVIEW.md`
- `Broiler.JS/HUMAN_REVIEW.md`
- `Broiler.JS/Broiler.DateTime/HUMAN_REVIEW.md`
- `Broiler.JS/Broiler.Regex/HUMAN_REVIEW.md`
- `Broiler.JS/Broiler.Regex/Broiler.Unicode/HUMAN_REVIEW.md`
- `Broiler.JS/Broiler.Unicode/HUMAN_REVIEW.md`
- `Broiler.Layout/HUMAN_REVIEW.md`

## Overall preview position

The repository is suitable only for first-preview, controlled development, testing, and
evaluation scenarios. It must not be presented as production-ready, security-audited, or
free of defects or vulnerabilities.

Several components are approved for first-preview use, usually with conditions. Several
nested review records remain pending or explicitly state that they are not approvals.
Because the preview application combines these components, the safe public position is:

- first-preview only;
- controlled or non-hostile inputs only unless the host provides isolation;
- no production or security-sensitive use without further review;
- re-review required after source changes to reviewed commits;
- public preview notes must preserve the safety warnings from the component reviews.

## Component status summary

| Source | Status summary |
| --- | --- |
| `Broiler.CSS/HUMAN_REVIEW.md` | Approved for first preview. Dead/transitional code and preview instability are accepted limitations. |
| `Broiler.CSS/Broiler.DOM/HUMAN_REVIEW.md` | Approved with conditions for first-preview use. Parser/string-handling risks, dead code, and cleanup follow-ups remain. |
| `Broiler.DOM/HUMAN_REVIEW.md` | Pending; not approved for preview use in that record. |
| `Broiler.Graphics/HUMAN_REVIEW.md` | Approved with conditions for first-preview use. Image codecs, untrusted binary input, native Windows API usage, and Direct2D/DirectWrite/DXGI/D3D interop are security-sensitive. |
| `Broiler.HTML/HUMAN_REVIEW.md` | Approved with conditions for first preview only. Renderer paths are not hardened for hostile HTML/CSS, and obsolete/transitional APIs remain. |
| `Broiler.HTML/Broiler.Graphics/HUMAN_REVIEW.md` | Pending; not approved for preview use in that nested record. |
| `Broiler.JS/HUMAN_REVIEW.md` | Pending final human review. Available only for first preview with safety warnings. JavaScript execution and host integration are security-sensitive and are not a sandbox. |
| `Broiler.JS/Broiler.DateTime/HUMAN_REVIEW.md` | Approved for preview. Residual risks are normal parser/date-time semantics and caller misuse in critical domains. |
| `Broiler.JS/Broiler.Regex/HUMAN_REVIEW.md` | Approved for preview. Resource-exhaustion risk from large or adversarial regex input remains despite a step budget. |
| `Broiler.JS/Broiler.Regex/Broiler.Unicode/HUMAN_REVIEW.md` | Pending; not approved for preview use in that nested record. |
| `Broiler.JS/Broiler.Unicode/HUMAN_REVIEW.md` | Approved with conditions for first-preview use. Runtime libraries use committed data, while developer-operated data tools download Unicode/CLDR data and require review for refreshes. |
| `Broiler.Layout/HUMAN_REVIEW.md` | Status line records first-preview approval, with decision conditions recorded. Layout CPU/memory consumption, dead code, cleanup, and dependency/license follow-ups remain. |

## Shared safety risks

- **Untrusted active content:** Broiler.JS can parse, compile, and execute JavaScript and
  can interact with .NET host capabilities. It is not a security sandbox.
- **Untrusted documents:** Broiler.HTML and related DOM/CSS/layout paths parse and render
  HTML/CSS. Hostile documents may trigger parser, resource-loading, rendering,
  performance, or compatibility problems.
- **Untrusted images:** Broiler.Graphics includes managed image codecs for PNG/APNG,
  JPEG, and BMP. Complex binary image parsing is security-sensitive.
- **Native Windows interop:** The graphics backend uses Windows APIs and
  Direct2D/DirectWrite/DXGI/D3D interop. Correct security-relevant usage is not guaranteed
  by the first-preview reviews.
- **Resource exhaustion:** Layout, rendering, regex matching, JavaScript execution, image
  decoding, malformed inputs, deep nesting, large documents, or adversarial data may cause
  high CPU use, high memory use, hangs, crashes, or denial-of-service behavior.
- **Network, file, and host access:** Preview code paths can load pages, resources,
  scripts, downloads, files, or host integration depending on the embedding scenario.
- **Incomplete review:** Some component records remain pending, and several approved
  records explicitly exclude production or security-sensitive use.

## Preview recommendations

- Use Broiler only as first-preview software in controlled development or test scenarios.
- Prefer local, known, non-hostile pages and assets for evaluation.
- Run unknown HTML/CSS/JS/images in a disposable VM, sandbox, or otherwise isolated
  environment.
- Do not execute untrusted JavaScript unless the embedding host enforces capability
  restrictions, isolation, and operational limits.
- Restrict resource loading, file access, network access, host integration, module
  loading, storage, debugging, downloads, and CLR interop when evaluating unknown content.
- Apply resource limits and timeouts around parsing, layout, rendering, image decoding,
  regex matching, and script execution.
- Keep all preview communication clear that this is not a production security audit.
- Re-run human review, tests, dependency/license checks, static analysis, vulnerability
  scanning, fuzzing, or threat modeling before broader release or security-sensitive use.

## Human attestation

No root-level human approval is recorded in this summary file. The authoritative human
review decisions and attestations remain in the source files listed above.

