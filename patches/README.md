# Submodule patches waiting to be applied

**Two patches are waiting on a maintainer.** See the index below.

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
| `0001` | `Broiler.JS` | Reject a `for` head that carries only one semicolon |
| `0002` | `Broiler.HTML` | Cap the image fetch timeout, as the stylesheet fetch already is |

The eight patches that held `0001`–`0008` before these two are all upstream and all
reachable through the pinned pointers, so their files are gone and the numbering has
restarted — which is the whole point of the section above, and the reason a `patches/NNNN`
reference in an older commit message names a different change than the file of that number
does today. Four of them (`Broiler.JS` 60c9182a direct-eval closure scope, f28c9a65 the named
property in a TypeError, 8059835a "is not a constructor", `Broiler.HTML` a9be60a media-element
painting) reverse-applied cleanly against the pointer, so the apply script was already skipping
them. The other four did **not** — `Broiler.JS` 7c1a9ae1 (unhandled rejections), 90bb350c (error
origin frames), `Broiler.DOM` e27ac6f (`<noscript>` raw text) and `Broiler.Regex` 1457450 (NUL vs
end of pattern) each landed in a form that is not byte-identical to the patch as exported, so their
reverse-apply check failed too. That is the drift signature described at the bottom of this file,
not evidence of a pending fix — which is exactly why the check above is the commit and not the
patch file.

### `0001` — `for(;)` did not fail to compile, it ran forever

`for ( Init_opt ; Test_opt ; Update_opt )` has exactly two semicolons, and each
clause is *omittable*, not *absent*. `ExpressionSequence` reports both the same
way — an `AstEmptyExpression` — so the for-head parser could not tell `for (;;)`
from `for (;)`. It read the missing update clause as an omitted one and built the
loop that spelling denotes: one with no test, which never terminates.

So a **SyntaxError became an infinite loop**. Every malformed one-semicolon head
was affected — `for(;)`, `for(a;)`, `for(;a)`, `for(var i=0;)`, `for(let i=0)` —
each parsed as a legal `for(;;)` instead of being rejected.

`ExpressionSequence` now also reports the terminator it actually consumed
(`TokenTypes.Empty` when it stopped without consuming one), and the C-style head
requires the test clause to end on `;` and the update clause on `)`. The
terminator is the only thing that separates the two cases; the AST produced for
every head that was already legal is unchanged, which is what the existing
`ParseProgram_ForHead_KeepsEveryClauseOptional` theory pins and what the new
`ParseProgram_ForHead_RequiresBothSemicolons` theory bounds from the other side.

**The newline half is the part worth reading.** Inspecting the terminator only
works if the parse *reaches* it, and it did not when a line terminator came
first: `ExpressionSequence` stopped at the newline with the `;` still unread. A
`for` head's semicolons are never supplied by ASI, so `for (i = 0\n; i < 5\n; i++)`
is perfectly ordinary code — and it was parsing correctly before, because while
nothing checked the terminator the clause boundaries happened to fall in the
right places anyway. Adding the check is what turned a latent quirk into a
rejection of valid input; the first version of this patch did exactly that, and
six such forms only failed once they were run rather than merely parsed. Line
terminators are now skipped before the terminator check, and *only* for a clause
that may be omitted — that is, only in a `for` head, never where the
`LineTerminator` break is what makes ASI work for an ordinary statement.

Both halves are pinned by tests, and the valid ones assert the **iteration
count**, not merely that the source parses: a head that parses into the wrong
AST is precisely the failure this patch is about, and parse-success cannot see
it.

**Where it came from.** WPT's timed-out-tests issue for 2026-08-16, which ranks
timeouts by source size on the reasoning that a sub-kilobyte document has too
little in it to be *legitimately* slow. Five of the ten smallest were
`html/webappapis/scripting/processing-model-2` — tests that check an uncaught
compile error reaches `window.onerror`, and each of them raises it with a
`<script>` whose entire content is `for(;) {}` (one via `support/syntax-error.js`,
a file that contains nothing else). The engine compiled that script rather than
rejecting it, and then ran it: each test burned the full 30-second per-test
budget and was reported as a timeout. All five now finish in about three seconds.

That ranking is worth taking seriously — it pointed straight at a parser
conformance bug from nothing but a file-size sort.

**Also in this patch:** `ForStatement`'s final `else` branch called
`stream.Unexpected()` without `throw`. That method *builds* the exception, it
does not raise it, so a head no branch could parse fell through into the body
parse with a null init/test/update instead of reporting the syntax error.

**Why it is listed for the pixel suite.** Unlike the other `Broiler.JS` entries
here, what this one fixes is a *timeout*, and a timeout is the one failure a
pixel comparison cannot reach — the run is aborted before anything is rendered.
It also costs the run wall-clock: 2.5 minutes of a shard's budget spent waiting
on loops that cannot end.

**When it lands upstream:** bump the pointer, drop the entry from
`scripts/apply-pending-wpt-patches.sh`, and delete this patch.

### `0002` — an unreachable `<img>` blocked the render for 100 seconds

Image loading on the render path is synchronous: `HtmlRender` sets
`AvoidAsyncImagesLoading`, so `DownloadImageFromUrl` runs inline on the layout
thread and blocks in `SharedHttpClient.Send`. That client set no `Timeout`, so it
took .NET's **100-second default** — more than three times any per-test budget —
and the only `CancellationTokenSource` bounding it is cancelled on `Dispose`.

Worse, it paid that twice: a failed URL is not remembered. `_imageDownloadCallbacks`
is an *in-flight* map whose entry is removed the moment a download completes, so
it coalesces concurrent requests only, and the next layout pass re-fetches.

`StylesheetLoadHandler` already fixed exactly this for `<link>` (WPT #1147);
images were simply missed at the time. Same cap, same reasoning, and the comment
now says so in both places.

**Where it came from.** `conformance-checkers/html/elements/img/src-isvalid.html`
— 88 `<img>` with 88 distinct sources, deliberately including IP literals and
documentation addresses (`http://192.0x00A80001`, `http://[2001::1]`) that
black-hole on a CI runner with real internet. One is enough to lose the test. It
renders in ~5 s in this container only because the agent proxy answers those
instantly, which is precisely why the timeout looked unreproducible locally.

**Why it is NOT listed in `scripts/apply-pending-wpt-patches.sh`.** The main-repo
half covers the WPT run — but **not** for the reason first recorded here, and the
wrong reason is worth keeping visible because it is what let the bug survive a
fix aimed straight at it. The claim was that `WptTestRunner`'s image handler
marks an off-corpus http(s) `<img>` handled, so the runner never reaches the
downloader. That handler is attached **per container**, and a WPT render is not
one container: the script bridge lays the document out through a
`HeadlessLayoutView` of its own to answer element-geometry queries, and the
runner has nowhere to attach anything to it. So this test kept timing out after
the handler landed — the geometry pass fetched all 34 off-corpus sources in full
before the gated render container ever ran. The gate now lives at the layout
layer instead (`DocumentRoot.IsUnreachableAbsoluteUrl`, consulted by
`CssBoxImage.StartContentImageLoad`), where every container funnels through it,
so the run reaches the downloader for no http(s) source at all. The patch still
matters for the **real browser**, which pins no document root and so is
deliberately unaffected by that gate: there, an unreachable `<img>` still stalls
the render on the 100-second default. It is a correctness fix to land upstream,
not something a WPT run needs applied on top of the pointer.

**Also not fixed here, and not by the main-repo gate either:** `SetImageFromPath`
sends *any* absolute non-`file:` URI to the downloader, so a `mailto:`,
`javascript:`, `ftps:`, `madeupscheme:` or non-image `data:` source is handed to
an `HttpClient` that answers `NotSupportedException`. That is fast — it costs no
wall-clock and loses no test — but it is the wrong shape: only `http`/`https`
should reach a network client at all. 14 of this test's 88 sources take that
path.

**Not fixed here:** the missing negative cache. A URL that already failed is
still re-fetched on the next layout pass; with a 5-second cap that is now an
annoyance rather than a lost test, but it is the other half of the defect.

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
