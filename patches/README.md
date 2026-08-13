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
| `0001` | `Broiler.JS` | Let the anonymous programs be dumped to disk |

### `0001` — a frame naming `vm16.js` still does not say what `vm16.js` is

Numbering the programs that have no script of their own (`eval`, and the
`Function` constructor's body) made a trace through a module loader
attributable: frames say `vm16.js` now, rather than every one of them saying
`vm.js`. That patch has landed — the pinned `Broiler.JS` pointer is its commit,
`1c8ec446`.

But a name is only half of it. Knowing that a `b is not defined` came from
`vm16.js:1,14` still does not say what `vm16.js` *is*, and a payload a loader
evaluated exists nowhere on disk to go and look at. That is exactly the position
the google.com report is in, five traces later.

So each anonymous program is written to a file named exactly what the frames call
it: `vm16.js` in the trace is `vm16.js` in the dump directory, holding that
program's source.

**Off unless `BROILER_JS_DUMP_PROGRAMS` names a directory.** Page script is page
content: dumping it by default would write whatever a page evaluates — including
anything personal a response embedded — to disk on every render, and that should
be a deliberate act rather than a default. The directory is also settable
directly, as the other compiler switches are, so a test can drive it without the
environment. A failure to write is swallowed, because a diagnostic must never be
able to break the execution it is observing.

**Why it is not listed for the pixel suites.** It writes files when explicitly
asked to and changes nothing about what any program computes, so no pixel moves
either way. Its behaviour is unit-tested inside the patch
(`AnonymousProgramDumpTests`).

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
