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

## The backlog is currently empty

No submodule fix is waiting to be applied. The last entry — *Stop for-await deadlocking on a step
result that is not already settled*, against `Broiler.JS` — is upstream: it is commit `ab5f797a`,
which is the pointer this repository pins, so CI sees it. Its patch file was deleted per the rule
above, and the main-repo hold it gated was released with it —
`ReadableStream.prototype.values` and its `@@asyncIterator` are now installed in
`src/Broiler.HtmlBridge.Dom/Polyfills/streams-and-file-reader.js`.

When the next fix has to ship this way, add it back as `0001-…` with the heading shape the deleted
entry used: **Targets**, **Subject**, **Based on**, then what the change does, what it is verified
against, and — if a main-repo fallback exists — what that fallback is and when it should be removed.
