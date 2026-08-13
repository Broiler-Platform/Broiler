# Submodule patches waiting to be applied

**One patch is waiting on a maintainer.** See the index below.

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

| # | submodule | subject |
| --- | --- | --- |
| `0001` | `Broiler.JS` | Number the programs that have no script name of their own |

### `0001` — every anonymous program was called `vm.js`

`eval`, and the `Function` constructor's body, were all compiled as `vm.js`. One
name for every such program makes a stack trace through a module loader
unreadable, because the frames cannot be attributed: two frames reading
`vm.js:1,14` and `vm.js:5060,25473` give no way to tell whether that is one
program or two — and which it is decides whether the failing function was
defined where it was called or somewhere else entirely.

That is not hypothetical. A `b is not defined` on google.com has been reported
four times with exactly that pair of frames, and this ambiguity is why it cannot
be read: the identifier is in a module payload the loader evaluates, but the
trace cannot say *which* payload, or even that a payload is involved at all
rather than one program calling itself.

So the fallback numbers them, the way devtools shows `VM123`: frames read
`vm1.js`, `vm2.js` and so on, one name per compiled program. A script that
already has a name — every script from the document, since the script-naming
work — keeps it; only the fallback changes.

**Why it is not listed for the pixel suites.** It changes what a frame is
*called* and nothing about what any program computes, so no pixel moves either
way. Its behaviour is unit-tested inside the patch
(`AnonymousProgramNamingTests`), and listing it would add a patch application to
every pixel run for no observable difference.

**When it lands upstream:** bump the pointer and delete this patch.

## A stale entry in the apply script is not inert

An earlier `0001` (`Broiler.HTML`, root-relative stylesheet href) had **landed
upstream** — the pinned pointer *was* its commit — but was still listed. The
idempotence guard did not save it: the guard skips a patch whose *reverse* apply
succeeds, and the upstream commit was not byte-identical to the patch as
exported, so the reverse check failed too. Applying neither way, it was reported
as drifted and `scripts/apply-pending-wpt-patches.sh` exited 1 on **every** run —
taking down the suites it exists to serve, and every later entry with it.

So when that script reports drift, check whether the fix is simply upstream
before regenerating anything:

```sh
git -C <Submodule> log --oneline --grep '<the commit subject>'
```
