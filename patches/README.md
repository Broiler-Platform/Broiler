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
| `0001` | `Broiler.CSS` | Expose the `@supports` evaluator for CSSOM `CSS.supports()` |

### `0001` — `CSS.supports()` had no truthful answer to give

The cascade already resolves an `@supports` prelude with the full
`<supports-condition>` grammar and the feature-support oracle beside it, but only
internally. CSSOM's `CSS.supports()` has to answer the same question the same
way — a page that asks whether a feature is supported and then styles with it
must not get one answer from the method and another from the rule. This patch
makes that existing evaluation reachable, validating the grammar first so a
malformed condition is `false` rather than an error (`CSS.supports()` never
throws). Nineteen lines, most of them comment.

**The main-repo half ships without it.** `window.CSS` and `CSS.escape()` are in
this repo (`Dom.Features.CssBinding`), and the binding reaches the evaluator *by
name* rather than by a direct call, so the assembly compiles against a
`Broiler.CSS` that does not expose one yet. Until this patch is applied
`CSS.supports()` reports `false` for everything; once the pointer moves it starts
answering truthfully with no change on this side. That fallback is the
conservative direction on purpose — a page told a feature is missing uses the
fallback it already carries, whereas a page wrongly told a feature works commits
to something nothing will render.

**Why the fallback is not simply "good enough".** Answering `false` to everything
is safe but not free: a site that feature-detects `(display: grid)` takes its
pre-grid fallback and lays out down an entirely different path from the
reference. That is why the entry is in
`scripts/apply-pending-wpt-patches.sh` — the difference is a whole page, and only
a pixel suite can see it.

**What it is deliberately not.** The obvious implementation — round-tripping the
declaration through a detached element's `style` — does not work here: Broiler's
CSSOM stores what it is given without validating, so `totally-bogus-prop`
survives the round trip and that technique answers "supported" to everything.
Note also what the oracle models, which the patch does not change: what the
reference browser understands, not what Broiler's layout engine implements. That
is the right basis for `CSS.supports()`, since a page feature-detects to decide
which CSS to write, not to predict how it will be painted.

**When it lands upstream:** bump the pointer, delete this patch and its entry in
`scripts/apply-pending-wpt-patches.sh`, and collapse `CssBinding`'s by-name
lookup to a direct call.

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
