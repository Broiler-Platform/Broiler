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
| `0001` | `Broiler.JS` | Parse a postfix `++`/`--` as part of a prefix unary operand |

### `0001` — `!c++ && 1` was a syntax error

The postfix belongs to the operand: `!c++` is `!(c++)`, because the grammar
reaches the postfix through `UpdateExpression : LeftHandSideExpression ++`, which
sits *below* `UnaryExpression : ! UnaryExpression`.
`FastParser.SinglePrefixPostfixExpression` took the postfix only on the path
where no prefix operator had been parsed — the `previous != None` branch returned
before the postfix loop — so the `++` of `!c++` was left in the token stream.

That is invisible when the expression ends there, which is why `!c++` and
`(!c++)` both parsed fine, and fatal the moment anything follows: the stray token
made the next operator unexpected. Every operator class was affected, under every
prefix operator — `!c++ && 1`, `!c++ || 1`, `!c++ + 1`, `!c++ === false`,
`!c++ ? 1 : 2`, `-c++ && 1`, `~c++ && 1`, `typeof c++ && 1`, `void c++ && 1`.

The fix moves the postfix loop above the prefix wrap. ASI is untouched: the
loop's own `LinesSkipped` guard still refuses a postfix across a line terminator,
so `!c\n++d` stays two statements.

**Why it is listed for the pixel suites.** A syntax error rejects a *whole
script*, not the statement holding it, and `!c++ && …` is the ordinary minified
spelling of a run-once guard — so the construct is everywhere in real-world
bundles. Without this patch google.com's 1.1 MB main script does not compile at
all (it failed at line 466 over a single `++`), so nothing the page's largest
script defines ever exists. A page whose largest script never ran renders as
something no reference matches, which makes this the real-world render suite's
core case; its entry lives in `scripts/apply-pending-wpt-patches.sh`.

**There is no main-repo fallback**, and that is not an oversight — the fix is
parser logic and cannot move to a main-repo layer. So the main repo carries no
test asserting the fixed behaviour either: until the patch is applied such a test
would fail CI. The regression tests travel *inside* the patch
(`ParserTests.ParseProgram_PostfixAfterPrefixUnary_*`, 20 cases covering each
prefix operator, each following operator class, the AST shape, and the ASI
guard), so they land exactly when the fix does.

**When it lands upstream:** bump the pointer, delete this patch and its entry in
`scripts/apply-pending-wpt-patches.sh`.

## A stale entry in the apply script is not inert

The patch that held `0001` before this one (`Broiler.HTML`, root-relative
stylesheet href) had **landed upstream** — the pinned `Broiler.HTML` pointer *is*
its commit, `1d11065` — but it was still listed. The idempotence guard did not
save it: the guard skips a patch whose *reverse* apply succeeds, and the upstream
commit is not byte-identical to the patch as exported, so the reverse check
failed too. Neither applying nor reverse-applying, it was reported as drifted and
`scripts/apply-pending-wpt-patches.sh` exited 1 on **every** run — taking down the
suites it exists to serve, and every later entry with it.

So when that script reports drift, check whether the fix is simply upstream
before regenerating anything:

```sh
git -C <Submodule> log --oneline --grep '<the commit subject>'
```
