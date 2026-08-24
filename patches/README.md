# Submodule patches awaiting upstream

Each file here is a `git format-patch` of a change that belongs in a submodule whose
remote is outside this session's GitHub scope, so the push returned 403 and the submodule
pointer was deliberately **not** bumped. Apply a patch inside its target submodule, push
there, then bump the gitlink in the parent — a pointer whose commit is not on the remote
would break CI, which clones submodules by pointer.

Delete a patch once its fix is upstream. This directory is a backlog, not an archive:
numbering restarts from `0001` against whatever is left, so the same number names
different changes at different times. Refer to a patch by its **commit subject**, never
by its number.

## Open patches

### `Resolve property escapes, evaluate v-mode class sets, fold from the UCD, iterate repeats, cut per-match allocation`

- **Target:** `Broiler.JS/Broiler.Regex` (`Broiler-Platform/Broiler.Regex`)
- **Apply onto:** `56c660701457a7e23f17f95ec82af7c75bee2723`
- **File:** `0001-broiler-regex-unicode-property-escapes-class-sets-and-case-folding.patch`

Track 2 of [the Broiler.JS gaps roadmap](../docs/broiler-js-gaps-roadmap.md#track-2--complete-ecmascript-regexp-behavior),
actions 1–3: `\p{…}` resolves against the pinned `Broiler.Unicode` tables, `v`-mode
class-set expressions and `\q{…}` are evaluated instead of skipped, and both branches of
`Canonicalize` become table-driven from the Unicode Character Database. Also six Annex B
grammar boundaries that differential testing against V8 turned up. Quantifier repetition is
iterative for every body shape — a single-code-point body (`.*`, `\d+`, `[^"]*`) through a
linear fast path, any other body (a capturing group, an alternation) through an
explicit-stack RepeatMatcher — so a repeat over a subject of any length matches natively
without recursing. Native recursion is bounded by the pattern's nesting depth (like the
parser), never by the subject, so a `RegexOverflowException` backstop remains only for a
pathologically nested pattern (the JavaScript layer catches it and falls back to .NET) and
input length no longer reaches it. A first allocation pass — `MatchState` as a `readonly
struct`, an allocation-free single-char fast path, one budget/capture array reused across a
run's start positions, and forward atom-run folding — takes simple patterns to ~2–3× the
compiled .NET engine (from up to 24×) ahead of the eventual non-Unicode routing; capture-heavy
patterns still need the compiled path (§4).

Verify with `dotnet test Broiler.Regex.slnx` inside the submodule — 253 tests, up from 57.

### `Route all Unicode-mode patterns to Broiler; unify match data; iterate all repeats`

- **Target:** `Broiler.JS` (`Broiler-Platform/Broiler.JS`)
- **Apply onto:** `a98619abbbcdcd70dbafddc96924b9e46ee60e85`, **after** the Broiler.Regex
  patch above has been pushed and this repository's `Broiler.Regex` gitlink bumped to it
- **File:** `0002-broiler-js-route-v-mode-class-sets-and-in-class-property-escapes.patch`

This patch carries both track-2 Broiler.JS changes:

- **Action 5 — routing.** `JSRegExp`'s gap scan routes `v`-mode class-set expressions and
  property escapes used as class members. It uses `CharSet.UsesPropertyEscape` and
  `CharSet.MatchesEmptyString`, which the Broiler.Regex patch introduces, so applying it
  before that patch will not compile.
- **Action 4 — one match-data abstraction.** `JSRegExp.Split`/`Replace` and `assert.match`
  now match through the routed engine (a shared `EnumerateMatches` over `RunMatch`),
  `IJSRegExp.Value` is replaced by an engine-agnostic `IsMatch`, and `CreateRegex` lets a
  routed pattern run with a null `value` when the translator cannot represent it — so a
  `v`-mode set operation over `\s`/`\S`/`\d` (valid ECMAScript the .NET UnicodeSets
  translator rejects) now constructs and matches through Broiler.
- **Action 6 — widen routing.** `TryBuildBroilerForGaps` becomes `TryRouteToBroiler`, which
  routes **every Unicode-mode (`u`/`v`) pattern Broiler can build** (not just the gap
  shapes), fixing real .NET-translation bugs for non-gap Unicode patterns — a standalone
  `\p{…}` under `i` threw, in-class case folding was missed. Non-Unicode patterns keep the
  faster .NET engine. With the Broiler patch's quantifier repetition now iterative for every
  body shape, no Unicode-mode pattern falls back on overflow; `RunMatch` still catches
  `RegexOverflowException` from the Broiler patch as a defensive backstop for a pathologically
  nested pattern, but input length no longer reaches it.

  All three parts need only the types already present in `a98619ab` plus `RegexOverflowException`
  and `CharSet.UsesPropertyEscape`/`MatchesEmptyString` from the Broiler.Regex patch, so
  applying this before that patch will not compile.

Verify with `dotnet test Broiler.JS/Broiler.JavaScript.BuiltIns.Tests` (2214 pass, up from
2173 — the new `RegExpEngineConsistencyTests`, `RegExpTranslatorFallbackTests` and
`RegExpUnicodeRoutingTests`) and
`dotnet test Broiler.JS/Broiler.JavaScript.Integration.Tests` (5024 of 5025 — the one
failure, `M8ValidationTests.M8_DocumentationFiles_Exist`, is pre-existing and unrelated:
it asserts a `docs/roadmap.md` that the component's own reorganisation replaced with
`docs/roadmap/`).

## No main-repo fallback is needed

Neither patch changes behaviour the main repository depends on: without them the pinned
submodules behave exactly as they do today, so CI is unaffected until they are applied.
The parent build does not name any of the new types, so reverting the submodule trees —
as this branch does — leaves everything compiling.
