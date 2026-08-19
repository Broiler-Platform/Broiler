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

The directory emptied again here, which is why the numbering restarts at `0001`
once more. Both patches it held are upstream and the pinned pointer contains
them, so each reached CI through the pointer and neither file was doing anything
but inviting a re-apply:

* `0001`, "Name the character a token cannot start with" — `Broiler.JS`
  `95aaa3ef`.
* `0002`, "Render a number as the number it was asked about, and find the empty
  string at the end" — `Broiler.JS` `e75de4ae`, which is the pinned pointer
  itself.

Both were checked the way this file says to check, and both answered "live on
CI":

```sh
git -C Broiler.JS merge-base --is-ancestor 95aaa3ef HEAD
git -C Broiler.JS merge-base --is-ancestor e75de4ae HEAD
```

## The index

| # | submodule | subject |
| --- | --- | --- |
| `0001` | `Broiler.JS` | Collect a JavaScript stack once, render it as often as asked |
| `0002` | `Broiler.HTML` | Say who is asking |

### `0002` — the sheets, pictures and fonts a server will not serve an anonymous client

`HttpClient` sends no `User-Agent` unless one is configured, and Wikimedia's User-Agent policy
answers a request that carries none with `403 Forbidden` before anything else about it matters. Each
of the three loaders in this submodule builds its own client, so each refused its resource on
`https://www.mediawiki.org/wiki/MediaWiki`: the `<link>` stylesheets, the `<img>` photographs on
`upload.wikimedia.org`, and any web font the page asked for. The patch gives all three the engine's
own token, `Broiler.Layout.Net.BroilerUserAgent` — a main-repository type this submodule already
references for `OfflineSubresources` — so it is one line per loader and adds no dependency.

**What is live without it, and what is not.** The reported failure — the instant `403` on the
document itself — is fixed in the main repository and needs nothing from here, as are the external
scripts, `fetch()` and XHR. What still fails against the pinned pointer is the *render*:
`HtmlRender` fetches a page's stylesheets through this submodule's `StylesheetLoadHandler` rather
than through the bridge's `ResourceLoader`, so a capture of that page comes back as bare document
flow instead of the Vector skin, with the photographs missing. Applying this patch is the whole
difference. The account is in `docs/mediawiki-user-agent-403.md`.

**Why it is not listed in `scripts/apply-pending-wpt-patches.sh`.** It can only change a resource
fetched over the network, and the WPT corpus is a directory on disk — the runner installs
`OfflineSubresources.FetchPolicy` and declines off-corpus URLs before a request is made. No WPT
pixel can move, so there is nothing for that script to exercise. The suite it *would* show up in is
`tests/real-world-sites`, which is observational and not part of the WPT gate.

**Why the main repository has no equivalent fallback.** The capture path renders through the static
`HtmlRender.RenderToFile*` helpers, which construct their own container: there is no host-side
`StylesheetLoad` handler to answer from, and the alternative — inlining every external sheet into
the document before handing it over — would mean re-implementing this submodule's relative-URL
rebasing in the caller, which is a larger change than the patch and a worse one.

### `0001` — one throw, five frames

`JSException.JSStackTrace` both renders an exception's JavaScript frames and
*collects* them: the walker appends each frame to the exception's own trace list
as it prints it, and that list is what keeps the frames printable by
`Exception.StackTrace` once the context that threw is gone. So the read has to
collect once and render thereafter, and it did neither — every read walked the
live frames and appended them again. An exception whose stack a host renders for
several sinks reported the whole stack once per sink.

The repeats are worse than redundant, because a frame is walked at its *current*
position: only the first copy carries the line it threw at, and each later copy
carries wherever that frame had unwound to by then. A function that failed at
line 3 and rethrew from its `catch` at line 4 came back as one line-3 frame
followed by four line-4 frames, naming the handler as if it were a call site.

It arrived in a report of `https://www.google.com/intl/de/about.html` as five
identical

```
at native in inline-0:line 14
```

lines — the shape of a five-deep recursion that never happened — above a
JavaScript-side `stack` that correctly showed the one frame there was. The two
halves of one report disagreeing is the tell: `stack` is a string captured once,
while the CLR-side rendering re-read a list that had been growing on every read.

**Why it is not listed in `scripts/apply-pending-wpt-patches.sh`.** It changes
how an exception's frames are rendered and nothing about what any program
computes. No pixel can move.

**It is the smaller half of the report it came from.** What actually failed on
that page — `document.currentScript` being unbound, so Google's tag-manager
loader threw on `new URL(document.currentScript.src)` — is in the main repo and
is already applied here. This patch is what makes the *next* report of that
shape legible; it is not what stops it happening.
