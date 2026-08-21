# Submodule patches

Changes that belong in a submodule but could not be pushed to its remote: the
session's git proxy only injects a credential for repositories in the session's
GitHub scope, and `Broiler-Platform/Broiler.*` is outside it, so
`git push origin HEAD` from inside a submodule returns **403**. Per
`CLAUDE.md` → "Submodules: modify them; push if allowed, otherwise deliver as a
PATCH", the change is exported here instead and **no gitlink is bumped** — CI
clones each submodule by pointer, and a pointer moved to a commit that was never
pushed would break it.

Apply one with `git am` from inside the submodule it names, then bump the
pointer in the parent:

```sh
cd <Submodule>
git am ../patches/NNNN-<slug>.patch
git push origin HEAD          # from a session/CI with the broader scope
cd ..
git add <Submodule> && git commit -m "Update submodules"
```

This directory is a backlog, not an archive: a patch is deleted once its fix is
upstream and the numbering restarts from `0001` against whatever is left. A
`patches/NNNN` reference in an older commit message or document is therefore
almost always dangling — name the **commit subject** instead. To check whether a
fix is already live:

```sh
git -C <Submodule> log --oneline --grep '<subject>'
```

`scripts/apply-pending-wpt-patches.sh` holds a matching list — the subset whose
fix can move rendered pixels, so a WPT run exercises it rather than testing
against the un-fixed pointer. It is idempotent: a patch already contained in the
pinned pointer reverse-applies and is skipped rather than re-applied.

## Index

| Patch | Submodule | Commit subject | Why |
| --- | --- | --- | --- |
| `0001-js-method-repository-site-lifetime.patch` | `Broiler.JS` | Address a compiled site by its index, not by a GCHandle never freed | `MethodRepository` used the address of a never-freed `GCHandle` as a compiled site's id, so every inner-lambda site a compilation registered stayed rooted for the life of the process — a `DynamicMethod` for an eagerly generated site, and the whole un-emitted expression tree for a deferred one that is never called. ~86 KB retained per compiled function, linear. Addressing sites by index inside the repository gives them the lifetime of the code that can reach them: `Broiler.Cli.Tests`' `ScriptCompileAhead` collection drops from a peak of 8.7 GB to 1.5 GB, bounded, with the same 49 tests passing in the same time. |
| `0002-keep-a-css-escape-from-ending-a-rule.patch` | `Broiler.CSS` | Keep a CSS escape from ending a rule, and drop three invalid declarations | The scanners that find a rule's closing brace tracked strings and comments but not escapes, so the `\}` in `error: \};` closed the rule it sits in and every rule after it re-parsed one token out of phase and was dropped — a whole-stylesheet failure on any sheet containing one. In Acid2 that was every rule from `ul { display: table }` on, i.e. the last line of the face. Three narrower error-recovery fixes ride along (a stray top-level `;`, a leftover `!`, and two invalid values), plus border-shorthand reset semantics: `border: solid 1em black; border-top: 0` now erases the top border instead of keeping it, which is what drew a black bar across Acid2's face. Adds the thread-static `CssDocumentMode` that `0003` publishes into. |
| `0003-correct-the-box-tree-inside-a-float.patch` | `Broiler.HTML` | Correct the box tree inside a float, and publish the document mode to the cascade | `ContainsInlinesOnly` counts a float as inline-compatible (CSS2.1 §9.5), so a box whose children are all floats answers true — and both box-tree correction passes only recurse when it answers false, while `ContainsInlinesOnlyDeep` skips floats outright. A float's subtree was therefore never visited by either pass. Acid1's `<ul>` holds nothing but floated `<li>`s, and the `display: inline` `<form>` two levels down needed both passes; it got neither, so its whole subtree — both radio-button lines — laid out at zero size and painted nothing. Also mirrors the document's quirks-mode flag into `Broiler.CSS.CssDocumentMode` so the cascade can tell a quirks-mode unitless length from an invalid one. |

**Apply `0002` before `0003`, and do not land `0003` alone.** `0003` calls
`Broiler.CSS.CssDocumentMode`, which `0002` adds, so Broiler.HTML does not
compile with only the second of the pair. The apply script keeps them in order;
a maintainer landing them upstream should push the Broiler.CSS commit and bump
that pointer first.

Until `0001` is applied and the pointer bumped, `docs/xunit-suite-status.md`
describes the retention as fixed — which it is in this tree only once the patch
is applied. Nothing in the main repo depends on it to build or pass.

`0002` and `0003` are the same: the main repo builds and its tests pass against
the pinned pointers without them. What the pinned pointers do *not* do is render
Acid1 and Acid2 correctly — that needs both, which is why both are listed in the
apply script.
