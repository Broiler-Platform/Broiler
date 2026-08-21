# Submodule patches waiting to be applied

**Two patches are waiting on a maintainer.** See the index below.

`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`, `Broiler.JS` and `Broiler.Graphics`
are git submodules with their own remotes — and `Broiler.JS` has submodules of its
own (`Broiler.DateTime`, `Broiler.Regex`, `Broiler.Unicode`), so a patch can target
a repository *nested* one level further down. A session whose GitHub scope is this
repository alone cannot push to any of them — the git proxy answers **403** — so a
fix that belongs in a submodule is committed there, exported with
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

The directory was emptied by the previous submodule bump, so the numbering
restarts at `0001` with the two below.

## The index

| # | submodule | subject |
| --- | --- | --- |
| `0001` | `Broiler.CSS` | Keep a CSS escape from ending a rule, and drop three invalid declarations |
| `0002` | `Broiler.HTML` | Correct the box tree inside a float, and publish the document mode to the cascade |

Both come out of the Acid1/Acid2 rendering work and both move rendered pixels,
so both are listed in `scripts/apply-pending-wpt-patches.sh`.

**Apply `0001` before `0002`, and do not land `0002` alone.** `0002` calls
`Broiler.CSS.CssDocumentMode`, which `0001` adds, so Broiler.HTML does not
compile with only the second of the two. The apply script keeps them in order;
a maintainer landing them upstream should push the Broiler.CSS commit and bump
that pointer first.

`0001` is the larger of the two: four error-recovery fixes plus two value-level
ones. The escape fix is the load-bearing one — an escaped `\}` inside a
declaration value closed the rule it sat in, and every rule after it was dropped;
in Acid2 that was everything from `ul { display: table }` on. It also adds
`Broiler.CSS.CssDocumentMode`, a thread-static the cascade reads to tell quirks
mode from standards mode.

`0002` lets both box-tree correction passes descend into a float. Acid1's
`<form>` sits inside a floated `<li>`, and neither pass ever reached it, so both
of its radio-button lines laid out at zero size and painted nothing. It also
publishes the quirks flag `0001` reads: the flag's home is
`Broiler.Layout.DocumentModeContext` in this repository, but Broiler.CSS.Dom
cannot reference Broiler.Layout, and mirroring from Broiler.HTML rather than from
that setter is what keeps the type Broiler.CSS owns out of the main repository —
which has to build against the pinned pointers.

There is no main-repo fallback for either: the fixes are wholly inside the
submodules, and the pinned pointers still render Acid1 and Acid2 without them.
