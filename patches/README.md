# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their remote from
the session that wrote them: the git proxy only authorises repos in the session's
GitHub scope, so a push to a submodule remote outside it returns **403**. Rather
than bump a submodule pointer at a commit CI cannot clone, the change is captured
here as a `git format-patch` file for a maintainer to apply.

## Applying

```sh
cd <Submodule>
git am --keep-cr ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

Delete the patch file and its row below once the pointer is bumped.

**`--keep-cr` is not optional for a patch that touches a file with CRLF line endings**, which
several `Broiler.JS` sources are (mixed CRLF and LF, within one file). `git am` runs the patch
through `mailinfo`, which normalizes the line endings of the diff body unless told not to — so the
context lines stop matching the file and the apply fails with *"patch does not apply"* on a patch
that is perfectly good. `git apply` does not have the problem, which is exactly why it is not the
check: these instructions use `am`, so `am` is what a patch has to survive. Verified per patch by
applying it to a clean checkout of the pinned pointer and diffing the result against the branch it
was generated from.

## Index

| Patch | Submodule | Note |
| --- | --- | --- |
*(empty — nothing is pending)*

## What has cleared

**The handoff has now completed a fourth time, and this time it took all three stacks at
once.** The fifteen files pending at the last reading — `0087` (`Broiler.HTML`), `0088`
(`Broiler.CSS`) and `0089`–`0101` (`Broiler.JS`) — have all been applied, pushed and their
pointers bumped.

They were checked **patch by patch against each submodule's log rather than inferred from this
prose**, which is the only reading of a pointer this campaign now trusts: each patch's `Subject`
resolved to a commit, that commit's own `format-patch` output was diffed against the patch file
(identical once the `From <sha>` line, the blob `index` lines and the trailing git version are set
aside — the whole of the difference on all fifteen), and the pointer each was pending against was
confirmed an ancestor of the new pin.

| Patch | Submodule | Landed as | Pending against |
| --- | --- | --- | --- |
| `0087-html-backdrop-painting-props` | `Broiler.HTML` | `2f94c0d5` | `29bf9c33` |
| `0088-css-nth-child-of-selector` | `Broiler.CSS` | `076ed5d5` | `dba36efb` |
| `0089-js-numeric-tree-order` | `Broiler.JS` | `12760bb9` | `cca39b4d` |
| `0090-js-gc-pause-accounting` | `Broiler.JS` | `48ad65e7` | `cca39b4d` |
| `0091-js-update-target-census` | `Broiler.JS` | `01c79c46` | `cca39b4d` |
| `0092-js-numeric-local-defeat-tests` | `Broiler.JS` | `2bab9775` | `cca39b4d` |
| `0093-js-3-8a-defeat-ab` | `Broiler.JS` | `16389682` | `cca39b4d` |
| `0094-js-speculative-numeric-population` | `Broiler.JS` | `e0bb9b40` | `cca39b4d` |
| `0095-js-speculative-numeric-storage` | `Broiler.JS` | `cfed00ef` | `cca39b4d` |
| `0096-js-speculative-numeric-read-paths` | `Broiler.JS` | `c2667c29` | `cca39b4d` |
| `0097-js-imported-outer-numeric-population` | `Broiler.JS` | `6ff52f3b` | `cca39b4d` |
| `0098-js-async-job-scheduling` | `Broiler.JS` | `ba31a4a9` | `cca39b4d` |
| `0099-js-execution-exclusion` | `Broiler.JS` | `b80327ac` | `cca39b4d` |
| `0100-js-blocking-host-wait` | `Broiler.JS` | `3fa35e14` | `cca39b4d` |
| `0101-js-free-name-scan` | `Broiler.JS` | `14fa4f10` | `cca39b4d` |

**So every figure recorded for those fifteen now describes the pinned pointers directly**, rather
than a local build plus a patch series applied in order, which is what their roadmap sections had
to say while they were pending. The `Broiler.JS` pin is now `14fa4f10`, `Broiler.HTML` `2f94c0d5`
and `Broiler.CSS` `076ed5d5`.

The fifty-two patches this file carried before them (`0001`–`0086`, in seven earlier bumps) had
already cleared the same way.

**A 403 has now meant *deferred* rather than *stranded* four times running**, and what makes that
safe is unchanged: the pointer is never bumped locally, so nothing written here can name a commit
CI cannot clone. The renumbering note the last series carried — `0087`–`0099` written, `+2` shifted
once `main` landed the HTML and CSS pair on the same two numbers — is retired with the series, but
the reason it happened is not, and it is the one thing worth keeping from it: **`patches/` is one
flat namespace across every submodule**, so two branches numbering from the same high-water mark
collide whenever both are open, which is the ordinary case rather than an unlucky one. Number from
`git ls-files patches/` at the moment of writing, and re-verify the chain after any rename.
