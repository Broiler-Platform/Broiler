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
| `0001-js-private-name-key-classification.patch` | `Broiler.JS` | Classify a private-name key by its marker and `#`, not the marker alone | A private name's property key is the U+0001 marker followed by the name, and the name always carries its leading `#` (`JSObject.MintPrivateName`, `FastCompiler.KeyOfPrivateName`). `KeyStrings.Classify` tested only the marker, so **every ordinary string key beginning with U+0001** was taken for a private name: writing one threw the brand-check `TypeError`, and reflection and enumeration hid it. WPT's `testharness.js` does exactly that while building its escape map — `formatEscapeMap[String.fromCharCode(p)]` with `p = 1` — so the harness threw *while loading* and **every testharness-based test in the suite reported no results at all**. With the patch applied the harness loads and the usual pass/fail table renders. |

The main repo builds and passes without it, and there is no main-repo seam that
can stand in: the classification lives entirely in
`Broiler.JavaScript.Storage`, so there is no equivalent fallback fix to carry
here in the meantime.

It is listed in `scripts/apply-pending-wpt-patches.sh`, which the privacy
test-page and real-world render workflows run before their builds — so it
reaches those on top of the pinned pointer. The **WPT** workflows
(`wpt-tests.yml`, `wpt-reftests.yml`) do not run that script today, so the WPT
suite only picks this up once a maintainer lands it upstream and bumps the
pointer.
