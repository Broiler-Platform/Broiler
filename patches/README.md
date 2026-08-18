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

The directory was emptied again when the patch that held `0001` before this one
landed upstream, which is why the numbering restarts at `0001` once more. That
patch ("Parse the minified JavaScript real sites serve") is `Broiler.JS`
`e680338f` and the pinned pointer is `2bab1567`, so it reaches CI through the
pointer and its file is deleted — this directory is a backlog, not an archive.

The patch that held `0002` — "Carry a direct eval's bindings into the closures its
functions create" — landed upstream as `Broiler.JS` `8564eee2`, and the pinned
pointer is that commit, so it reaches CI through the pointer and its file is
deleted along with its entry in `scripts/apply-pending-wpt-patches.sh`. `0001`
keeps its number: the numbering restarts only when the directory empties.

## The index

| # | submodule | subject |
| --- | --- | --- |
| `0001` | `Broiler.JS` | Name the character a token cannot start with |

### `0001` — the report that named neither a token nor a character

`FastScanner` reported a character that cannot start any token by naming
`Token` — the token most recently produced. That says nothing about the
problem: the offending character has not been tokenised, which is the whole
point. And on the **first** character of a source no token has been produced at
all, so the report read

```
Unexpected token Empty:  at 1, 1
```

with neither a token nor a character in it — the message a maintainer opened a
bug with, twice.

That first-character case is the one worth naming properly, because a source
whose very first character cannot start a token is usually **not JavaScript**,
and *which* non-JavaScript it is, is the diagnosis:

```
Unexpected character '•' (U+2022) at 1, 1
Unexpected character U+001F (a control character) at 1, 1
Unexpected character U+FFFD (the replacement character — the source is not
  valid text in the encoding it was decoded with) at 1, 1
```

A bullet or a dash says a data block reached the engine (which is what the
main-repo half of this change stops happening). A control character says a
response body was never decompressed. U+FFFD says it was decoded with the wrong
encoding. An astral character is a surrogate pair, so it is named by the code
point the pair forms rather than by half of one; an unpaired surrogate is called
that; end of input is reported as end of input rather than as U+FFFF.

Only the fall-through at the end of `_ReadToken` changes — the one site that
reaches a character no case handles. Every other caller of `Unexpected()` is
reporting a token that does exist, where naming it is right. 12 parser test
cases pin the wording and the classification, plus five that assert the
characters which legitimately start a token (`$`, `_`, accented and CJK
identifiers, the ASCII operator set, and non-ASCII inside strings and comments)
still scan.

**Why it is not listed in `scripts/apply-pending-wpt-patches.sh`.** It changes
the text of an error message and nothing else. No pixel can move.

**It is the smaller half of the change.** The cause of the reported failure is
in the main repo and is already applied here: `ScriptExtractionService` executed
every `<script>` in a document whatever its `type`, so JSON-LD, framework state,
speculation rules, import maps and client-side templates were all compiled as
JavaScript. This patch is what makes the *next* report of this shape legible; it
is not what stops it happening.
