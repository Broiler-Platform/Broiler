# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their `MaiRat/`
remote from the session that wrote them: the git proxy only authorises repos in
the session's GitHub scope, so a push to a submodule remote outside it returns
**403**. Rather than bump a submodule pointer at a commit CI cannot clone, the
change is captured here as a `git format-patch` file for a maintainer to apply.

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
| `0033-html-canvas-backdrop-translucent-root-background.patch` | `Broiler.HTML` | Composite a translucent propagated canvas background over the dark canvas backdrop instead of a hard-coded white one, so `:root { color-scheme: dark; background-color: rgba(…) }` renders over `rgb(18,18,18)` (WPT `css/css-color-adjust/rendering/dark-color-scheme/color-scheme-iframe-background-mismatch-alpha`). |

No main-repo fallback ships for 0033: the bug is in canvas layer compositing
inside the renderer, with no equivalent seam at a main-repo layer, so the WPT
test stays red on CI until the patch is applied.
