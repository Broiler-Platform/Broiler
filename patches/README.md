# Submodule patches

Changes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) are delivered here as patch files rather than
pushed to the submodule remotes. The submodule pointers in the parent repo are
**not** bumped by these PRs — a maintainer applies the patch in the submodule,
pushes it there, and updates the pointer in a follow-up. See `CLAUDE.md`
("Submodules") for the policy and rationale.

Each patch is a `git format-patch` output: apply it from **inside** the target
submodule directory.

## Applying a patch

```sh
cd Broiler.HTML                 # the target submodule for this patch
git am ../patches/0001-broiler-html-comment-whitespace-collapse.patch
# (or, to apply without committing:)
git apply ../patches/0001-broiler-html-comment-whitespace-collapse.patch
```

Then push the submodule and bump the pointer in the parent repo.

## Index

| Patch | Submodule | Issue | Summary |
|-------|-----------|-------|---------|
| `0001-broiler-html-comment-whitespace-collapse.patch` | `Broiler.HTML` | [#1119](https://github.com/MaiRat/Broiler/issues/1119) | `HtmlParser.AppendCanonicalNode`: coalesce consecutive text nodes so a comment between siblings no longer splits an inline white-space run into two collapsing runs (spurious space / content shift). This is the proper-layer version of the main-repo fallback `DomBridge.RemoveRenderCommentNodes`; once applied, that fallback can be removed. |
