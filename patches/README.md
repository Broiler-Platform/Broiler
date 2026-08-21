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
| `0001-compositing-group-transform-content.patch` | `Broiler.HTML` | Keep a compositing group's contents when the group cannot use the raster canvas | A group opened for `opacity`, `mix-blend-mode` or `isolation` stays on the raster canvas only when every display item it encloses is one the raster canvas can draw, and `RGraphicsRasterBackend.IsRasterCompatibleItem` answers no for a `TransformItem`. One transformed descendant — however deep, and however ordinary the rest of the subtree — therefore sent the whole group to the compat seam, which on a host with no OS backend is an inert stub, and switching to it also turns `CanUseRaster` off for every draw the group encloses: the group and its entire subtree painted **nothing**. `SaveOpacityLayer`/`SaveBlendLayer` now skip the layer instead and let the contents draw straight onto the surface, which is what `SaveFilterLayer` already does and what `SaveTransformLayer`'s own comment describes about the same fall-through. `duckduckgo.com` is the page that showed it: its start page wraps all of its content in `#__next { isolation: isolate }` and has transforms beneath it, so it rendered as an empty white viewport. |

The same failure mode as the dashed-stroke patch that
`scripts/apply-pending-wpt-patches.sh` used to carry — a draw that misses the
raster path and falls through to the stub seam paints nothing at all — so this
one is listed there too, and reaches the WPT and real-world render runs on top
of the pinned pointer until a maintainer lands it.

The main repo builds and passes without it. `Broiler.Layout` carries the half of
the `duckduckgo.com` fix that is expressible there — an `isolation: isolate`
group is unobservable when nothing in the document blends, so it is no longer
emitted at paint time (`UnobservableIsolationGroupTests`) — which is what makes
that page render in this tree. That is a narrower fix than this patch by
construction: it cannot help a group opened for `opacity` or for a real
`mix-blend-mode`, and both still lose their subtree until this lands.
