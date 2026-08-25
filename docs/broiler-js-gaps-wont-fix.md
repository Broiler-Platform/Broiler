# Broiler.JS gaps — won't fix

> Part of the [Broiler.JS gaps](broiler-js-gaps-roadmap.md) set:
> [closed](broiler-js-gaps-closed.md) · [open](broiler-js-gaps-open.md) · [in progress](broiler-js-gaps-in-progress.md) · **won't fix**.
> Statuses were last reconciled on **2026-08-25**.

Records that look like open gaps and are not. Some are already landed and only older Markdown
still calls them pending; some are deliberate product or profile boundaries; one is a deliberate
specification-conformance choice where a pinned suite disagrees with the engine and the engine is
right. None of these is Full-profile engine work.

## Stale records — landed, do not reopen

Do not reopen these solely because older Markdown calls them pending:

- the older direct-eval lexical-closure fixes are landed; the two direct-eval issues in track 1
  are different defects;
- the RegExp embedded-NUL and terminal-backslash fixes are landed;
- the prefix/postfix parser fix for forms such as `!c++ && 1` is landed;
- the Broiler.CSS evaluator used by `CSS.supports()` is landed;
- the main `document.currentScript`, `readyState`, `requestIdleCallback`, `sessionStorage`, and
  `structuredClone` surfaces have later fixed evidence, although narrower edge cases above remain;
- Broiler.HTML static-renderer exclusions do not prove that the aggregate Browser/HtmlBridge stack
  lacks every excluded API; and
- Minimal and deliberately narrow Portable profiles are not gaps in the Full profile.

## Deliberate deviations

Places where the engine answers as the current specification and the major engines do, and a
pinned test does not. They are documented next to the code rather than carried as failures.

- **`annexB/language/function-code/block-decl-func-skip-arguments`** — the test quotes the
  pre-2021 FunctionDeclarationInstantiation, which appended `"arguments"` to *parameterNames*;
  current 10.2.11 appends it to *paramBindings* instead, so the Annex B copy-out runs and the
  function value replaces the arguments object. V8 and SpiderMonkey both answer as Broiler does.
  See [the component's known gaps](../Broiler.JS/docs/compliance/known-gaps.md#deliberate-deviations).
- **Two `vi`-mode RegExp divergences**, both pinned by a test in `UnicodeSetsTests`: a lone binary
  property folds under §22.2.2.9 (so `\p{ASCII}` matches `ſ`), and a one-character `\q{…}`
  alternative folds like any other member. Broiler follows the specification in both; V8 does not.
  Whether to keep doing so is the open half — see
  [in progress](broiler-js-gaps-in-progress.md#track-2--complete-ecmascript-regexp-behavior).

## Out of scope

Removed or proprietary surfaces such as WebSQL and `chrome.loadTimes()` are not roadmap work.
Diagnostics that merely hide first-chance exceptions are useful tooling work but are not language
feature gaps and are not tracked here.

Execution speed, allocation, startup, tiering, caching, boxing, benchmark scores, and other
performance-only work are out of scope for this set. Web Performance API defects remain in scope
when the problem is missing or incorrect observable behavior rather than speed.
