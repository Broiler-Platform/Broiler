# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their remote from
the session that wrote them: the git proxy only authorises repos in the session's
GitHub scope, so a push to a submodule remote outside it returns **403**. Rather
than bump a submodule pointer at a commit CI cannot clone, the change is captured
here as a `git format-patch` file for a maintainer to apply.

The directory is a backlog, not an archive: it holds only what is *currently*
pending. A patch is deleted from here, along with its row below, once its fix is
upstream and the submodule pointer is bumped.

## Applying

```sh
cd <Submodule>
git am --keep-cr ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

**`--keep-cr` is not optional for a patch that touches a file with CRLF line endings**, which
several `Broiler.JS` sources are (mixed CRLF and LF, within one file). `git am` runs the patch
through `mailinfo`, which normalizes the line endings of the diff body unless told not to — so the
context lines stop matching the file and the apply fails with *"patch does not apply"* on a patch
that is perfectly good. `git apply` does not have the problem, which is exactly why it is not the
check: these instructions use `am`, so `am` is what a patch has to survive. Verified per patch by
applying it to a clean checkout of the pinned pointer and diffing the result against the branch it
was generated from.

## Exercised on CI before they land

`scripts/apply-pending-wpt-patches.sh` applies the patches listed in its
`PENDING_PATCHES` array to the checked-out submodule trees before the WPT run, so
a pending fix is reflected in CI's numbers rather than waiting on the pointer. It
is idempotent — a patch already contained in the pinned pointer reverse-applies
and is skipped — so an entry stops applying by itself once a maintainer lands it.

## Index

| Patch | Submodule | Note |
| --- | --- | --- |
| `0102-css-dir-pseudo-class` | `Broiler.CSS` | **`:dir()` matched every element, in both directions at once.** It was listed in `CssSelectorMatcher`'s `RecognizedPseudoClasses` but had no arm in the pseudo-class switch, so it fell through to the deliberately-lenient default for recognised-but-unmodelled names — which means `:dir(ltr)` *and* `:dir(rtl)` applied to the whole document. That is how a single shadow-tree rule painted an entire canvas in issue #1538 problems 16 and 20 (`css/css-shadow/shadow-directionality-001` and `-002`, both at ~1% match). It now resolves HTML's directionality concept: the nearest ancestor-or-self with a valid `dir` attribute, `ltr` at the root, with `dir=auto` — and `<bdi>`, whose default it is — resolved from the first strong directional character, skipping `<script>`/`<style>` and any descendant that declares its own direction. An argument other than `ltr`/`rtl` matches nothing (Selectors 4 §11.2). **Strictly a narrowing**, so it can only remove matches the lenient default invented. **The two tests it is named for do not depend on it**: they are carried over the threshold by the main-repo shadow-tree scoping that shipped with it (1.1–1.3% → 99.55%, measured), and this makes them pass for the right reason rather than because every `:dir()` matched. 7 tests in `CssSelectorMatcherDirTests` cover both directions, inheritance, an invalid attribute value, `auto` in three shapes, the `<bdi>` default and the non-`ltr`/`rtl` argument. Applies cleanly to the pinned `Broiler.CSS` pointer (`076ed5d`) and survives `git am --keep-cr`. Listed in `scripts/apply-pending-wpt-patches.sh`, so the WPT run exercises it on CI ahead of a maintainer landing it. |
