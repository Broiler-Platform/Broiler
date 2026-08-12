# Submodule patches waiting to be applied

**The backlog is empty. Nothing here is waiting on a maintainer.**

`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`, `Broiler.JS` and `Broiler.Graphics`
are git submodules with their own remotes. A session whose GitHub scope is this
repository alone cannot push to them — the git proxy answers **403** — so a fix
that belongs in a submodule is committed there, exported with
`git format-patch`, and left here for a maintainer to apply. The submodule
working tree is then reverted to its pinned commit and **the gitlink is not
bumped**: CI clones a submodule by pointer, and a pointer to a commit that was
never pushed would break the build.

Applying one:

```sh
cd <Submodule>
git checkout -b <branch> && git am ../patches/NNNN-<slug>.patch
git push origin HEAD
cd .. && git add <Submodule>      # bump the pointer only once the push succeeds
```

## This directory is a backlog, not an archive

A patch is deleted from here the moment its fix is upstream and the submodule
pointer is bumped, because from then on it reaches CI through the pointer and a
file that can only ever be skipped is noise. `scripts/apply-pending-wpt-patches.sh`
holds the matching list — also empty — and is idempotent, so a patch already
contained in the pinned pointer is skipped rather than re-applied.

**Check the pointer, not this file, before concluding a fix is pending.** The
numbering is *recycled*: numbers are assigned from `0001` against whatever the
directory holds at the time, so a patch number in an older commit message, code
comment or document does **not** identify the same change as today's patch of
that number. Prose that names a patch by number alone is evidence about the past
only. To decide whether a submodule fix is live, look for its commit:

```sh
git -C <Submodule> log --oneline --grep '<the commit subject>'
git -C <Submodule> merge-base --is-ancestor <sha> HEAD && echo "live on CI"
```

## Recently emptied

The six patches this directory held until 2026-08-12 (`0001`–`0006`) are all
upstream, and each is an ancestor of the pinned pointer, so every one of them is
live on CI:

| was | submodule | commit | subject |
| --- | --- | --- | --- |
| `0001` | `Broiler.HTML` | `1bf117a` | Let a caller say what the canvas composites its background against |
| `0002` | `Broiler.HTML` | `be76c7f` | Treat an empty inset clip as a clip, not as no clip |
| `0003` | `Broiler.DOM` | `55057b8` | Treat `<frame>` as a void element in the parser and serializer |
| `0004` | `Broiler.HTML` | `d1cdad4` | Paint a frame's canvas opaque only when its colour scheme differs from its embedder's |
| `0005` | `Broiler.HTML` | `f8db3c6` | Paint the four 3D border styles as bevels instead of flat sides |
| `0006` | `Broiler.HTML` | `f86b655` | Mitre a border's corners and anti-alias the diagonal |

Pinned at the time of writing: `Broiler.HTML` `f86b655`, `Broiler.DOM` `55057b8`
— the tips of `0006` and `0003` respectively.

They were not merely *semantically* present: each is a real commit in its
submodule's history and an ancestor of the pinned pointer, checked with
`merge-base --is-ancestor`. Worth recording, because by then **none of the six
applied cleanly in either direction any more** — the surrounding code had moved
on, so `git am` would have failed and a reverse-apply check would have said "not
applied". A drifted patch file for an applied fix is worse than no file: it reads
as outstanding work and cannot be applied to find out otherwise.
