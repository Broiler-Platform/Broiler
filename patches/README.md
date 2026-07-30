# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their remote from
the session that wrote them: the git proxy only authorises repos in the session's
GitHub scope, so a push to a submodule remote outside it returns **403**. Rather
than bump a submodule pointer at a commit CI cannot clone, the change is captured
here as a `git format-patch` file for a maintainer to apply.

## Applying

```sh
cd <Submodule>
git am ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

Delete the patch file and its row below once the pointer is bumped.

## Index

| Patch | Submodule | Summary |
| --- | --- | --- |
| `0035-dom-create-element-local-name-colon.patch` | `Broiler.DOM` | Stop `DomDocument.CreateElement` routing through the namespace-aware `DomName` constructor. Per DOM, `createElement` takes a *local* name: it does no prefix splitting and no qualified-name validation. Because that is also the HTML parser's element-creation path, a tag name the tokeniser legitimately produced — anything with a leading, trailing or doubled colon, e.g. `<x::y>` — threw `'…' is not a valid qualified name` and took down the whole page render (WPT issue #1491 problem 1, `navigation-timing/dom-interactive-media-document.html`, whose name came from WebM bytes). |

A main-repo fallback ships for 0035, so the WPT test that reported the crash is
green on CI without it: `FragmentTreeBuilder.BuildEmbeddedDocumentMarkup` no
longer feeds a frame's non-HTML resource to the HTML parser, which is what minted
that tag name. The patch is the fix at its own layer — it covers every other way
such a tag name reaches the parser, ordinary markup included — so it is still
worth applying.
