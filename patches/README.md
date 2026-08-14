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
git checkout -b <branch> && git am --keep-cr ../patches/NNNN-<slug>.patch
git push origin HEAD
cd .. && git add <Submodule>      # bump the pointer only once the push succeeds
```

**`--keep-cr` is not optional here.** Many `Broiler.JS`, `Broiler.HTML` and
`Broiler.CSS` sources are CRLF, and `git am` splits its mailbox with the CR
stripped unless told otherwise — so the patch's context lines arrive LF-only,
match nothing in a CRLF file, and it fails with `patch does not apply` against a
tree that is in fact untouched. The give-away is that `git apply --check` on the
same patch succeeds, because `git apply` never does that stripping. Reach for
`--keep-cr` before concluding a patch has drifted.

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
| `0001` | `Broiler.JS` | Say which promise a tracked rejection happened to |

### `0001` — the tracker knew a rejection happened, not what it happened to

`JSPromiseRejectionTracker` reports that a promise was rejected with nobody
waiting on it, and reports it as a reason: `TakePending` returns the rejection
reason alone, and the promise instance the tracker is keyed on never leaves its
dictionary. That is all a log line needs, and less than the event a browser
fires for these needs.

`unhandledrejection` carries the promise, and a page uses it as an **identity** —
to match the report against the work it started, and to recognise the same
promise if a handler arrives later. The reason cannot stand in for it: two
promises rejected with the same error are two rejections.

The patch adds `TakeNotifications`, which is `TakePending` with the key included,
and the state behind `rejectionhandled`: rejections already handed to the host
are remembered, and a handler arriving for one moves it to a reclaimed list for
the host to drain. `TakePending` stays and drains the same set, so a host that
only logs is unaffected. The retention is capped, because unlike the pending set
— emptied every time the host drains it — a rejection that is never handled is
never removed, and an uncapped table would retain every rejection on a long-lived
page.

**Without it the feature degrades rather than breaks.** `Broiler.HtmlBridge.Dom`
probes for the file it adds (`BroilerJsRejectionPromiseIdentity`, alongside the
older `BroilerJsRejectionTracking`): unpatched, `unhandledrejection` still fires
and is still cancelable, its `promise` is `undefined`, and `rejectionhandled`
does not fire. `RejectionHandledEventTests` is excluded from the build for the
same reason — the coverage cannot pass against an engine that cannot report the
identity. Both probes are file-existence checks, so applying this patch is all it
takes to turn the rest on.

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
