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

The directory was empty when this patch was added, which is why the numbering
restarts at `0001` again.

## The index

| # | submodule | subject |
| --- | --- | --- |
| `0001` | `Broiler.JS` | Parse the minified JavaScript real sites serve |

### `0001` — five shapes that minifier output produces and FastParser rejected

A parse failure is total. One token the parser will not accept and the whole
script fails to compile, so nothing on the page runs — which is why each of
these is worth more than the small grammar corner it looks like. All five were
found the same way: by running `FastParser` over the scripts live sites actually
serve, so each is named here by the script that produced it.

* **A `/` after the `)` of a statement head was read as division.** The head of
  an `if` / `for` / `while` / `with` is followed by a *Statement*, which may
  perfectly well begin with a regular expression literal. marked.js — bundled
  into monaco-editor, and into the page bundles of several news sites — walks a
  markdown table's alignment row with
  `for(i=0;i<n;i++)/^ *-+: *$/.test(a[i])?…`. Read as division the regex never
  closes and the file is rejected. The scanner now records, for each open paren,
  whether it opened a statement head, and only a `)` that closed one allows a
  regex after it. Every other `)` still divides, which the
  `SlashAfterOtherParenthesis_IsStillDivision` theory and the evaluating
  `SlashAfterCallOrGrouping_StillDivides` test bound from the other side.

* **A template substitution parsed only an AssignmentExpression.** `${ … }` is a
  full `Expression`, so a comma sequence belongs there; swiper-bundle.min.js
  folds an entire initialisation into one interpolation. The `,` was left
  unconsumed and the script rejected.

* **An object *binding* pattern rejected a reserved word as a PropertyName.** A
  PropertyName is an IdentifierName, and the object *literal* path already
  accepted these — only the pattern path did not, because `false` / `true` /
  `null` / `in` / `instanceof` lex to token types of their own rather than
  `Identifier`. TypeScript's own bundle destructures groupBy's result as
  `const { false: decorators, true: metadata } = …`. The shorthand form stays an
  error: a reserved word is a legal key and never a legal BindingIdentifier.
  `export { x as in }` is allowed for the same reason — a ModuleExportName is an
  IdentifierName, and it is what minified re-export lists come to.

* **The `[~In]` of a `for` head leaked into every nested context.** A function
  body, an arrow, a call's arguments and a template substitution are each `[+In]`
  in their own right, so an `in` inside one of them within a for-head is an
  ordinary operator. core-js and its many re-bundles install properties from
  exactly that shape: `for (var i = 0, E = function (e) { e in C || define(C, e) }; …)`.
  The head's own top level still suppresses `in`, so a for-in loop is unaffected
  (`ForInHead_StillSuppressesInAtItsTopLevel`).

* **An Annex B IdentityEscape was neutralised only outside a character class.**
  `ClassEscape` reaches `IdentityEscape` too, so `/[^0-9a-zA-Z\-\_]/` is a valid
  literal — Adobe Launch's bundle sanitises input with precisely it. Failing the
  .NET validity check made the scanner fall back to reading the `/` as division
  and the file failed. `\-` keeps its backslash inside a class, where `-` is the
  range operator.

**Also in this patch: the report a failed statement produces.** When a production
gives up by returning false rather than throwing, it rewinds to where the
statement started, so the only token left to blame is the statement's first one —
and in a minified bundle that is character 1 of the file. Every such failure
therefore read `Unexpected token Negate: ! at 1, 1`, pointing at the `!` of a
`!function(){…}()` wrapper however far away the real problem was. That is the
report this patch started from. `FastTokenStream` now keeps a high-water mark
that survives the rewind, and `UnexpectedStatement` names where the parse
actually stopped — including "the end of the script" for a source that ended
early, which is what a truncated response looks like from inside the parser.

**Tests.** 54 parser cases covering each shape and its counterpart, plus
`MinifiedScriptParsingTests`, which *evaluates* every shape rather than only
parsing it: two of these fixes change how a token is read (`/` as a regex
delimiter, `in` as an operator), and a source that parses into the wrong tree is
exactly the failure parse-success cannot see. `Broiler.JavaScript.Parser.Tests`
is 184/184, `Broiler.JavaScript.Integration.Tests` 4627/4628 (the one failure,
`M8ValidationTests.M8_DocumentationFiles_Exist`, is a docs-layout check that
fails identically on the un-patched pointer) and
`Broiler.JavaScript.Compiler.Tests` 1318/1318.

**Why it is not listed in `scripts/apply-pending-wpt-patches.sh`.** It could be —
a script that does not parse renders nothing, which is the failure a pixel suite
exists to catch — but that argument needs a WPT test that actually uses one of
these shapes, and there is no evidence any does: every one of the five was found
in *minifier output*, and WPT's tests are hand-written. Listing an entry that can
only ever skip is noise, and a listed entry that later drifts against the pointer
fails the script and takes the whole run down with it. The precedent is the
direct-eval patch (`Broiler.JS` 60c9182a), which was never listed for the same
reason: it decides whether page script runs at all rather than what any of it
paints.

**Where it came from.** A `FastParseException` from a Google search page, reported
as `Unexpected token Negate: ! at 1, 1` — a report that, for the reason above,
does not say which script failed or where. The five fixes here are what a sweep of
real-world scripts turned up while chasing it; whether one of them is *that*
script's failure is not established, because the page that produced it could not
be reproduced from this container (Google serves it a script-light variant).

**A gap this patch does not close.** `export { a, b };` — a NamedExports list with
no `from` clause — is still rejected outright ("Expecting keyword from"), which
takes down a whole module. It accounted for 12 of the 21 remaining failures in the
sweep. It is not fixed here because the parser is only half of it: `AstExportStatement`
has no shape for a source-less export list and `VisitExportStatement` has no case
for one, so accepting it in the parser alone would produce a module that parses
and then exports nothing. That is module-linking work, not a parser fix.
