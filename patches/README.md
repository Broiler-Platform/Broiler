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
| `0001` | `Broiler.HTML` | Fetch a root-relative stylesheet href from the origin, not the filesystem |

### `0001` — a root-relative `<link href>` was read off the local disk

`ResolveStylesheetSource` decided "is this href already absolute?" with
`Uri.TryCreate(src, UriKind.Absolute)`. On Unix that is a different question from
the one it means to ask: `/style.css` parses absolute there, as
`file:///style.css`. So a root-relative href looked already-resolved, was never
rebased on the document URL, and `LoadStylesheet` then took its `file:` branch and
looked for the sheet on the local filesystem. The miss is swallowed as a
`CssParsing` error, so the page renders **completely unstyled** with no diagnostic
and no HTTP request. On Windows the same call returns `false`, so the sheet loaded
correctly there — which is the likely reason it went unnoticed.

The fix is the scheme guard `HtmlContainerInt.TryResolveHttpFontUrl` already
applies to the same call for `@font-face src`. Ten lines, eight of them comment.

**Why it is listed for the pixel suites.** www.7-zip.org links its stylesheet as
`<LINK href="/style.css">`, so the `seven-zip` case of the real-world render suite
renders with none of the page's CSS until this is applied — no table backgrounds,
no centred headings, and the body's `font-size: 80%` absent. That is as
pixel-moving as a change gets. The real-world workflow runs
`scripts/apply-pending-wpt-patches.sh`, which is where the entry lives.

**It does not move WPT pixels**, and that is not an oversight. The WPT runner
installs its own stylesheet-load handler (`WptTestRunner`, the `wptRoot` mapper)
that resolves root-relative paths such as `/fonts/ahem.css` to local files and
hands them back through `args.SetSrc` — which short-circuits
`ResolveStylesheetSource` entirely. WPT never exercised the broken path, which is
the second reason this survived so long.

**The main-repo half of the same investigation is already in the tree** and needs
nothing from this patch: the quirks-mode table font reset
(`Broiler.Layout.Engine.TableFontInheritanceQuirk`) and the DOCTYPE public-identifier
half of `DocumentModeContext.IsQuirksHtml`. They are what make 7-Zip's nested-table
text the right *size*; this patch is what makes its stylesheet *arrive*. Applying
this one without them renders the page styled but in a ~6px font.

**When it lands upstream:** bump the pointer, delete this patch and its entry in
`scripts/apply-pending-wpt-patches.sh`.
