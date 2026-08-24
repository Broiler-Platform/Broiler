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

### `Resolve property escapes, evaluate v-mode class sets, and fold from the UCD`

- **Target:** `Broiler.JS/Broiler.Regex` (`Broiler-Platform/Broiler.Regex`)
- **Apply onto:** `56c660701457a7e23f17f95ec82af7c75bee2723`
- **File:** `0001-broiler-regex-unicode-property-escapes-class-sets-and-case-folding.patch`

Track 2 of [the Broiler.JS gaps roadmap](../docs/broiler-js-gaps-roadmap.md#track-2--complete-ecmascript-regexp-behavior),
actions 1–3: `\p{…}` resolves against the pinned `Broiler.Unicode` tables, `v`-mode
class-set expressions and `\q{…}` are evaluated instead of skipped, and both branches of
`Canonicalize` become table-driven from the Unicode Character Database. Also six Annex B
grammar boundaries that differential testing against V8 turned up.

Verify with `dotnet test Broiler.Regex.slnx` inside the submodule — 247 tests, up from 57.

### `Route v-mode class sets and in-class property escapes to Broiler.Regex`

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
  now match through the routed engine (a shared `EnumerateMatches` over `RunMatch`), and
  `IJSRegExp.Value` is replaced by an engine-agnostic `IsMatch`. This part needs only the
  types already present in `a98619ab`, but it is bundled here because it shares
  `JSRegExp.Broiler.cs`.

Verify with `dotnet test Broiler.JS/Broiler.JavaScript.BuiltIns.Tests` (2180 pass, up from
2173 — the seven new `RegExpEngineConsistencyTests`) and
`dotnet test Broiler.JS/Broiler.JavaScript.Integration.Tests` (5024 of 5025 — the one
failure, `M8ValidationTests.M8_DocumentationFiles_Exist`, is pre-existing and unrelated:
it asserts a `docs/roadmap.md` that the component's own reorganisation replaced with
`docs/roadmap/`).

## No main-repo fallback is needed

Neither patch changes behaviour the main repository depends on: without them the pinned
submodules behave exactly as they do today, so CI is unaffected until they are applied.
The parent build does not name any of the new types, so reverting the submodule trees —
as this branch does — leaves everything compiling.
