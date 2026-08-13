# Submodule patches waiting to be applied

**Nothing is waiting on a maintainer right now.** This directory is empty of
patches, which is the expected steady state — see below.

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
holds the matching list — the subset whose fix can move rendered pixels, so a
WPT run exercises it rather than testing against the un-fixed pointer — and is
idempotent, so a patch already contained in the pinned pointer is skipped rather
than re-applied.

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

## The index

_Empty._ The two patches that were here — the `Broiler.JS` postfix-`++` parser fix and the
`Broiler.CSS` `@supports` evaluator — both landed upstream, and both pinned pointers carry
them (`434db760` and `8be7a65`). They reach CI through the pointer now, so their files and
their `scripts/apply-pending-wpt-patches.sh` entries are gone with them: this directory is a
backlog, not an archive.

Numbering therefore restarts at `0001` for the next patch added.

## A stale entry in the apply script is not inert

An earlier `0001` (`Broiler.HTML`, root-relative stylesheet href) had **landed upstream** — the
pinned pointer *was* its commit — but was still listed. The idempotence guard did not save it:
the guard skips a patch whose *reverse* apply succeeds, and the upstream commit was not
byte-identical to the patch as exported, so the reverse check failed too. Applying neither way,
it was reported as drifted and `scripts/apply-pending-wpt-patches.sh` exited 1 on **every** run
— taking down the suites it exists to serve, and every later entry with it.

So when that script reports drift, check whether the fix is simply upstream before regenerating
anything:

```sh
git -C <Submodule> log --oneline --grep '<the commit subject>'
```
