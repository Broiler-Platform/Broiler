# Submodule patches awaiting application

Fixes that belong in a submodule but could not be pushed to its remote: the push returns **403**
because the `Broiler-Platform/Broiler.*` repositories are outside this session's GitHub scope. Each
patch is generated with `git format-patch`, and the submodule pointer is deliberately **not** bumped
— CI clones the submodule by pointer, and bumping one to a commit that was never pushed would break
the clone.

This directory is a backlog, not an archive: a file is deleted once its fix is upstream, and the
numbering restarts from `0001` against whatever is left. So a `patches/NNNN` reference in an older
commit message or document is almost certainly dangling. Identify a patch by its **commit subject**
instead, and check whether it is live with:

```sh
git -C <Submodule> log --oneline --grep '<subject>'
git merge-base --is-ancestor <sha> HEAD
```

## Applying one

```sh
cd <Submodule>
git am ../patches/<file>.patch
git push origin HEAD          # from an environment whose scope includes the submodule remote
cd ..
git add <Submodule>           # bump the pointer only after the push succeeds
```

---

## `0001-css-is-alias-pseudos.patch`

- **Targets:** `Broiler.CSS` (`Broiler.CSS.Dom/CssSelectorMatcher.cs`)
- **Subject:** *Stop the `:is()` aliases matching every element*
- **Based on:** `3829101`, the currently pinned pointer

`:matches()`, `:any()`, `:-webkit-any()` and `:-moz-any()` are the historical spellings of `:is()`.
All four were listed as recognized-but-unmodelled, so they fell through the matcher's lenient default
arm and matched **every element**. The cascade uses the same matcher, so
`:-webkit-any(h1) { color: red }` painted the whole page rather than the headings — a rendering bug,
not only a `querySelector` one.

Measured against Chromium: only the `-webkit-` spelling is still accepted, and it behaves exactly
like `:is()`, so it routes to `MatchesAny`. `:matches()`, `:any()` and `:-moz-any()` were removed
from the platform, so they are invalid selectors whose rule is dropped and which match nothing.

**No main-repo fallback is possible for this one.** The damaging half is the cascade, which reaches
the matcher through the computed-style engine rather than through the bridge's `MatchesSelector`
wrapper, so there is no main-repo seam that could intercept it. The bug is live until this patch is
applied.

**When applying it, also update one main-repo test.**
`Broiler.Cli.Tests/DomApiSyntaxTests.An_Unknown_Functional_Pseudo_Class_Still_Over_Matches` pins the
current wrong answer on purpose — it asserts `document.querySelector(':matches(a)')` returns the
`<html>` element — precisely so that fixing the matcher trips it rather than passing unnoticed. After
the patch it should assert that `:matches(a)`, `:any(a)` and `:-moz-any(a)` all answer `null`, that
`:-webkit-any(div)` selects the `div`, and (still) that an unknown *vendor-prefixed* pseudo such as
`:-webkit-bogus` matches everything, which stays the matcher's deliberate policy.
