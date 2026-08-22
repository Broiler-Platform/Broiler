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
| `0002-html-outermost-svg-author-display.patch` | `Broiler.HTML` | Let an outermost `<svg>` keep the display the author cascaded | An outermost `<svg>` is a replaced element, so its `display` is *initially* `inline`; the box tree substitutes `inline-block` for that (how an inline replaced box is laid out here) but applied the substitution to the **cascaded** value too, discarding whatever the author wrote. `svg { display: block }` therefore laid out inline — siblings side by side instead of stacked, and `margin: 0 auto` computing to zero rather than centring (`css/compositing/line-with-svg-background` is ten block `<svg>`s and came out two to a row) — and `svg { display: none }` was overridden into a visible box, so the hidden `<svg>` a page uses to carry nothing but a `<filter>` or `<defs>` painted (`css/filter-effects/tainting-css-dropshadow-currentcolor`, 97.4 % → 100 %). The cascaded value is now read on the outermost `<svg>` only: a nested one is SVG content rather than a CSS box and arrives already carrying its ancestor's `display: none`. |
| `0003-css-link-matches-xlink-href.patch` | `Broiler.CSS` | Match `:link` on an SVG `<a>` that links through `xlink:href` | SVG 1.1 §17.1 gives `<a>` its link through `xlink:href`, and SVG 2 §14.1 adds plain `href` without retiring it, so either attribute alone makes the element a link. `CssSelectorMatcher` tested `href` alone, so an `<a xlink:href="…">` matched no `a:link` rule and the whole rule was dropped — `a:link rect { fill: lime }` left the red rectangle beneath it showing. Pairs with the main-repo fix that fires `load` at an outermost inline `<svg>`: WPT `svg/linking/reftests/href-a-element-attr-change` removes `href` from its own load handler and asserts the element keeps its link status, so until that handler ran the test passed without reaching its assertion at all. |
| `0004-html-paint-transform-origin.patch` | `Broiler.HTML` | paint: apply a CSS transform about its transform-origin | CSS Transforms 1 §8 applies an element's `transform` about its `transform-origin`, and `PaintWalker` used the box centre for every element without reading the property. So a declared origin did nothing, and paint disagreed with the element's own script: `transform-origin: 0 0; transform: scale(0.5)` on a 100×100 box reported a rect at (0, 0) from `getBoundingClientRect` and painted it at (25, 25). Scaling *up* is worse than a displacement — a centre origin throws a top-left child clean off the canvas — so `css/filter-effects/filter-scaling-001` (#1774 entry 28, 50.1 %) rendered a blank page where half the viewport should be green. The grammar is main-repo (`Broiler.Layout.IR.CssTransformOrigin`, shared with the bridge's transform chain and the SVG renderer, unit-tested in `CssTransformOriginTests`); this patch is the two lines that reach it. |
| `0005-html-row-flex-replaced-item.patch` | `Broiler.HTML` | parse: leave a row flex container's image unwrapped | `CorrectImgBoxes` wraps a block-level replaced element in an anonymous block, which is how such a box is laid out here — and blockification has already made a flex item's `<img>` block-level by the time it runs. For an item that wrapper is fatal: it *becomes* the flex item, so every size the flex algorithm resolves lands on it while the image inside keeps the width it was declared with. An `<img style="width:999px">` in a zero-width flex row could not shrink at all — not even with `min-width: 0`, the one spelling that always lets an item collapse — and simply overflowed its container. Column containers are deliberately excluded: they place items by ordinary block flow, and block flow is what cannot position a block-level replaced box, which is why the wrapper exists at all. |

None is bumped as a pointer, and the main repo builds and passes without any of
them.

For `0001` there is no main-repo seam that can stand in: the classification
lives entirely in `Broiler.JavaScript.Storage`, so there is no equivalent
fallback fix to carry here in the meantime. `0002` is the same — the
substitution is made in `Broiler.HTML`'s own box-tree cascade — but its
behaviour is pinned in the main repo by
`src/Broiler.Cli.Tests/SvgAuthorDisplayTests.cs`, which feature-probes the
pinned pointer and self-skips the cases the patch enables rather than going red.
`0003` is the SVG-linking half of the scripted-`<svg>` work whose other three
fixes are main-repo; those are pinned unconditionally by
`src/Broiler.Wpt.Tests/SvgScriptedOnloadTests.cs`, and this patch is what takes
`svg/linking/reftests/href-a-element-attr-change` the rest of the way once they
let its load handler run.

`0004` is shaped the way CLAUDE.md asks a two-repo fix to be shaped: the whole of
the `transform-origin` grammar is main-repo
(`Broiler.Layout/IR/CssTransformOrigin.cs`, shared with the script bridge's
transform chain and with the SVG renderer's `transform-box` handling, and
unit-tested unconditionally by `Broiler.Layout.Tests.CssTransformOriginTests`), so
the patch itself is the two lines in `PaintWalker` that call it.
`src/Broiler.Wpt.Tests/TransformOriginPaintTests.cs` pins the painted result the
way `0002`'s test does — it probes the pinned pointer and self-skips rather than
going red. There is no main-repo fallback that can stand in for the paint half:
the transform is emitted in `Broiler.HTML`'s own stacking walk.

`0005` is shaped the same way. The whole of the CSS Flexbox §4.5 sizing it unblocks
is main-repo (`Broiler.Layout/Engine/CssBox.Flex.cs`: a replaced item's *content size
suggestion* is its natural inline size rather than the width it was declared with, and
its *transferred size suggestion* — its definite cross size through the aspect ratio —
bounds that; plus the block-axis twins of both), and it is unit-tested unconditionally
by `Broiler.Layout.Tests.FlexReplacedItemSizingTests`, which builds the unwrapped tree
by hand and so passes with or without the patch. The patch is the one condition that
produces that tree from a real document. Measured against Chromium goldens with it
applied, `css/css-flexbox` goes 899 → 906 of 1439 and `css/css-sizing` 390 → 391 of 748,
with `css/css-grid` unchanged at 1220 of 2343.

All five are listed in `scripts/apply-pending-wpt-patches.sh`, which the privacy
test-page and real-world render workflows run before their builds — so they
reach those on top of the pinned pointer. The **WPT** workflows
(`wpt-tests.yml`, `wpt-reftests.yml`) do not run that script today, so the WPT
suite only picks these up once a maintainer lands them upstream and bumps the
pointers.
