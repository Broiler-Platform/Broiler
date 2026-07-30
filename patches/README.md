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
| `0036-css-system-colors-and-logical-viewport-units.patch` | `Broiler.CSS` | Two whole-canvas failures from WPT issue #1491. **System colours** (problem 28): `CssSystemColors` carried only `Field`/`FieldText`, so `Canvas`, `CanvasText`, `ButtonFace` and the rest fell through to the named-colour lookup and resolved to black — `forced-colors-mode-20.html` rendered 98% black against Chromium's 98% white, and the whole family failed the same way. Fills in the CSS Color 4 §6 table from the light palette, aliases the §6.2 deprecated keywords, and adds a `CssColorScheme` overload for a dark used colour scheme. **Logical viewport units** (problem 30): `vb`/`vi` did not parse at all, so `page-box-008-print.html`'s `block-size: 100vb` box got no size. Adds them against the root element's writing mode plus the `sv*`/`lv*`/`dv*` variants, and fixes the number/unit split that canonicalisation broke (`"100svmin"` parsed its number as `"100s"`). |
| `0037-html-root-writing-mode-for-logical-viewport-units.patch` | `Broiler.HTML` | Hands the root element's `writing-mode` to `CssLengthParser.SetViewportSize` alongside the viewport dimensions, so `vi`/`vb` resolve on the axes CSS Values 4 §6.1.4 specifies. **Apply after 0036** — it calls the overload that patch adds. |
| `0038-dom-node-movebefore.patch` | `Broiler.DOM` | Adds the canonical `DomNode.MoveBefore` — the atomic form of `Node.moveBefore()`. Because the spec requires both parents to share a shadow-including root, a moved node's connectedness cannot change, so the document's id index is deliberately not torn down and rebuilt and the node is never disconnected (an `<iframe>` does not reload, a render-blocking element keeps blocking). Observers still get records for both parents; only the disconnection is skipped. WPT issue #1491 problem 27, `dom/nodes/moveBefore/preserve-render-blocking-style.html`, which rendered white because the missing method threw and the document was never styled. |

0036 and 0037 are a pair: 0037's call site needs the `SetViewportSize` overload
0036 introduces, so applying 0037 alone will not compile. There is no main-repo
fallback for either — until both are applied and the pointers bumped, CI still
resolves system colours to black and `vb`/`vi` to nothing.

A main-repo fallback ships for 0038 too: the bridge's `moveBefore` binding
(`DomBridge.MoveNodeBefore`) reproduces the observable behaviour on the primitives
available at the pinned submodule SHA, so the API exists and the WPT test is
green on CI without the patch. It is not fully atomic — the node is briefly
detached, so the id index churns. When 0038 lands, replace that method's body
with `parent.MoveBefore(node, reference)` and delete its local
`EnsurePreMoveValidity`, which duplicates the canonical check.

A main-repo fallback ships for 0035, so the WPT test that reported the crash is
green on CI without it: `FragmentTreeBuilder.BuildEmbeddedDocumentMarkup` no
longer feeds a frame's non-HTML resource to the HTML parser, which is what minted
that tag name. The patch is the fix at its own layer — it covers every other way
such a tag name reaches the parser, ordinary markup included — so it is still
worth applying.
