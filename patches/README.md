# Submodule patches

Patches here capture changes to a `MaiRat/Broiler.*` git submodule whose remote is **outside this
session's GitHub push scope** (the push returns 403 — see `CLAUDE.md` → "Egress-scope caveat"). Each
patch is a `git format-patch` of a submodule commit; the submodule pointer in the parent repo is left
**unchanged** (CI clones the submodule by pointer, so bumping it to an unpushed commit would break the
build). A maintainer with push access applies the patch to the submodule, pushes it, and then bumps
the gitlink in the parent.

## How to apply

```sh
cd <Submodule>                       # e.g. Broiler.DOM
git am ../patches/NNNN-<slug>.patch  # or: git apply, then commit
git push origin HEAD                 # push to the MaiRat/ remote
cd ..
git add <Submodule>                  # bump the pointer in the parent
git commit -m "Bump <Submodule> for <slug>"
```

## Index

| Patch | Submodule | Summary | Active main-repo fallback |
|---|---|---|---|
| `0001-add-domnode-isequalnode.patch` | `Broiler.DOM` | Adds `DomNode.IsEqualNode` — the neutral DOM `Node.isEqualNode()` tree algorithm (HtmlBridge complexity-reduction roadmap Phase 4 items 4/5). | The bridge keeps its own `DomBridge.NodesAreEqual` / `CanonicalAttributesAreEqual` (`SubDocuments.cs`) and its `isEqualNode` binding uses it. Once the patch lands and the `Broiler.DOM` pointer is bumped, delete the bridge copy and delegate the binding to `node.IsEqualNode(other)` (equivalent: the bridge's element-text comparison via `BridgeText` is a no-op on the canonical tree, where an element's `NodeValue` is null). Behaviour is pinned by `Broiler.Cli.Tests/IsEqualNodePromotionTests.cs`. |
