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
| `0001` | `Broiler.JS` | Keep a direct eval's scope alive for the closures it creates |

### `0001` — a closure a direct eval created lost the eval site's bindings

`eval("(function(){ return b; })")` threw `b is not defined` when the function it
returned was called, even though `eval("b")` at the same spot read the same
binding fine.

A direct eval's scope is **lexical**: the closure keeps the eval site's bindings
after that call has returned. Broiler made the caller's bindings reachable by
installing them as an overlay for the duration of the eval and withdrawing it on
return — right for code the eval *runs*, wrong for code the eval *creates*. The
names stop resolving at exactly the moment such a function is first called.

So a function created by directly-evalled code now captures those bindings — as
one created inside a `with` block already captured its with-chain — and
re-establishes them for the duration of a call. The live `JSVariable` objects are
captured rather than their values, so the binding stays shared in both
directions: a later write by the enclosing function is visible to the closure,
and a write by the closure lands on the caller's binding rather than on a fresh
global.

**Consulted only after every ordinary scope has failed**, on the read and the
write path alike, so nothing that resolves today resolves differently. Placing it
alongside the eval-binding walk instead broke Annex B block-level function
declarations, which own their name through `globalVars` and must not be shadowed
by the snapshot (`Issue619.AnnexBEvalFuncBlockScoping`,
`Issue912EvalHoistChar`) — worth knowing before anyone tries to "simplify" the
placement.

**Where it came from.** Five reports of `b is not defined` on google.com. Its
module loader is `function(e){return eval(e)}(src)` with `src` being
`0,function(){b(2,57,1,w)}` — the result stored and invoked later by the bundle.
The fragment that made it readable came from the program dump added for exactly
that purpose, which has since landed upstream.

**Why it is not listed for the pixel suites.** It decides whether page script
runs at all rather than what any of it paints, and the pixel suites do not
execute a loader of this shape. Its behaviour is unit-tested inside the patch
(`DirectEvalClosureScopeTests`).

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
