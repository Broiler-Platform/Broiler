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

## Index

| Patch | Submodule | Commit subject | Why |
| --- | --- | --- | --- |
| `0001-dom-static-parsedocument-in-tests.patch` | `Broiler.DOM` | Call HtmlDocumentParser.ParseDocument on the type, not an instance | `ParseDocument`/`ParseFragment` are static; two files in `Broiler.Dom.Html.Tests` still construct a parser to call them on. That is CS0176, so the test project does not compile and its 41 tests do not run. With the patch applied they compile and all 41 pass. |

`Broiler.DOM` is checked out twice — at `Broiler.DOM` and, at the same commit, at
`Broiler.CSS/Broiler.DOM` (`scripts/check-submodule-sha-drift.sh` enforces that
they match). Patch `0001` is applied once and both gitlinks move together.
