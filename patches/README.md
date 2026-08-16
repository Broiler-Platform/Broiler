# Submodule patches waiting to be applied

**Four patches are waiting on a maintainer.** See the index below.

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
| `0003` | `Broiler.HTML` | parse: generate the anonymous table a misparented table box needs |
| `0004` | `Broiler.HTML` | load: let the host decline a stylesheet fetch it knows cannot succeed |

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
half already covers the WPT run: no `<img>` in a run reaches the downloader with
an off-corpus http(s) URL, so the default this caps cannot be hit here. A
sandboxed conformance run should not touch the network at all, which is the more
correct behaviour independently. The patch matters for the **real browser**,
where no such policy is installed and an unreachable `<img>` still stalls the
render — so it is a correctness fix to land upstream, not something a WPT run
needs applied on top of the pointer.

**That claim was wrong once, and the correction is worth more than the claim.**
It used to rest on `WptTestRunner`'s image handler marking an off-corpus `<img>`
handled — which is true of the container the runner *paints* with, and only of
it. A run lays each document out **twice**: the script bridge lays it out
headlessly to answer geometry reads, in a container of its own that no handler
was ever attached to, and that pass fetched all 31 off-corpus hosts of
`src-isvalid.html` at the uncapped 100-second default, before the guarded render
had started. The test timed out again on 2026-08-16 with the handler in place,
which is what showed it. `Broiler.Layout.Engine.OfflineSubresources` now declines
the load in the engine, at `CssBoxImage`'s and `CssBox.Background`'s load sites,
so the policy holds for every container rather than one — and the entry can stay
off the list for the reason originally given.

**Not fixed here:** the missing negative cache. A URL that already failed is
still re-fetched on the next layout pass; with a 5-second cap that is now an
annoyance rather than a lost test, but it is the other half of the defect.

**When it lands upstream:** bump the pointer and delete this patch.

### `0003` — a table box outside a table painted nothing at all

CSS 2.1 §17.2.1 has two halves, and only one of them was implemented.
`CssLayoutEngineTable` generates the missing **children** — an anonymous row for a
stray cell in a table or a row group, an anonymous cell for stray content in a row.
But it only ever runs on a box that *is* a table, so nothing generated the missing
**parents**: a `table-row`, row group or `table-cell` sitting directly inside a block
or an inline. Such a box is neither `IsBlock` nor `IsInline`, so block layout walked
straight past it — it never laid out, never painted, and contributed no height.

**Where it came from.** WPT's `css/CSS2/tables/table-anonymous-objects-*` family, 103
of whose members were failing. Each is a "there should be no red" test that stacks a
green layer over a red one; the green layer is built out of spans carrying nothing but
`display: table-row-group`, so it painted nothing and the red layer underneath showed
through in full. That is also why the family's failures were split between
`MissingContent` and `ReferenceOverlayExposed` — the same absence, classified by which
of the two layers the test happened to put on top.

**Only the one-line call is in this patch.** The pass itself —
`Broiler.Layout.Engine.AnonymousTableBoxes` — is a main-repo file, so the patch is the
single line that reaches it from `DomParser`'s box fix-ups. It is placed before the
inline/block corrections so the generated table takes part in them as the block-level
box it is.

**Measured** on the family's own `rel=match` references (font-neutral, unlike a golden
comparison in a bare container): 46 → 48 passing, 60 tests improved against 7
regressed. Four tests moved from passing to failing and are worth understanding rather
than treating as a regression — in those the *red* layer is the one built from bare
table boxes, so they had been passing because the content under test rendered nothing
at all. Passing by omission; they now render and show the residual geometry gap.

**Why it is listed in `scripts/apply-pending-wpt-patches.sh`.** Without this line the
main-repo half never runs, so nothing about the fix reaches a WPT run.

**When it lands upstream:** bump the pointer, drop the entry from
`scripts/apply-pending-wpt-patches.sh`, and delete this patch.

### `0004` — the run had three fetchers and a policy on one of them

A timeout cap bounds an unreachable host; it does not make one free. The external
stylesheet client is capped at five seconds, and
`css/CSS2/cascade-import/cascade-import-009.xht` links three sheets to a `delayed-file`
CGI on a personal server that pauses 2, 5 and 8 seconds by design — so the cap is what
each of them costs, every time the document is laid out.

**And a run lays each document out more than once.** The script bridge lays it out
headlessly to answer the geometry reads scripts make (`DomBridge` → `HeadlessLayoutView`),
the runner lays it out again to paint it, and the bridge fetches external sheets through
a loader of its own besides — whose *speculative preload scan* puts them on the wire from
a worker before the parse has even started. Only the paint container had the runner's
handlers on it. So the same three unreachable sheets were fetched three times over, about
twenty-four seconds of a thirty-second budget, and the test was reported as a timeout
rather than run. The same absence is what kept
`conformance-checkers/html/elements/img/src-isvalid.html` timing out after its own fix had
landed — see `0002` above, which records the wrong reason it was thought to be covered.

**Only the one-line consult is in this patch.** The policy —
`Broiler.Layout.Engine.OfflineSubresources`, a host-set predicate over the URL, unset and
therefore inert for a renderer that has a network — is a main-repo type, as are the image
load sites (`CssBoxImage`, `CssBox.Background`) and the bridge's own loader. A `<link>` is
the one sub-resource whose load starts in the submodule (`DomParser` →
`StylesheetLoadHandler`), which is why this line exists at all.

**Measured** on `cascade-import-009.xht`: 30.7 s → 14.4 s with the main-repo half alone,
→ 2.3 s with this line as well (2.2 s is the process's own start-up, so that is the whole
of it). The main-repo half is therefore what stops the timeout; this patch buys back the
rest of the budget.

**The main repo builds and runs correctly without it.** It names nothing new in
`Broiler.HTML` — the dependency points the other way, at a main-repo type — so a workflow
that builds `src/Broiler.Wpt` without applying patches (the WPT Reftests one does exactly
that) compiles and behaves as it did, only slower on the documents this speeds up.

**When it lands upstream:** bump the pointer, drop the entry from
`scripts/apply-pending-wpt-patches.sh`, and delete this patch.

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
