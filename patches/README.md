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
| `0001-js-method-repository-site-lifetime.patch` | `Broiler.JS` | Address a compiled site by its index, not by a GCHandle never freed | `MethodRepository` used the address of a never-freed `GCHandle` as a compiled site's id, so every inner-lambda site a compilation registered stayed rooted for the life of the process — a `DynamicMethod` for an eagerly generated site, and the whole un-emitted expression tree for a deferred one that is never called. ~86 KB retained per compiled function, linear. Addressing sites by index inside the repository gives them the lifetime of the code that can reach them: `Broiler.Cli.Tests`' `ScriptCompileAhead` collection drops from a peak of 8.7 GB to 1.5 GB, bounded, with the same 49 tests passing in the same time. |

Until `0001` is applied and the pointer bumped, `docs/xunit-suite-status.md`
describes the retention as fixed — which it is in this tree only once the patch
is applied. Nothing in the main repo depends on it to build or pass.
