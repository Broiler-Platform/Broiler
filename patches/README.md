# Submodule patches waiting to be applied

**Nine patches are waiting on a maintainer.** See the index below.

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
| `0001` | `Broiler.JS` | Keep a direct eval's scope alive for the closures it creates |
| `0002` | `Broiler.JS` | Name the property in "Cannot read properties of undefined" |
| `0003` | `Broiler.JS` | Report promises rejected with nobody to handle them |
| `0004` | `Broiler.JS` | Record where an error was raised, not where its factory was wired |
| `0005` | `Broiler.DOM` | Parse a `<noscript>` body as raw text, as a scripting-enabled parser must |
| `0006` | `Broiler.JS/Broiler.Regex` | Tell the end of a pattern apart from a NUL inside one |
| `0007` | `Broiler.JS` | Say "is not a constructor", as every other construct site does |
| `0008` | `Broiler.HTML` | Paint a media element's box only when it shows controls |
| `0009` | `Broiler.JS` | Reject a `for` head that carries only one semicolon |

### `0001` — a closure a direct eval created lost the eval site's bindings

`eval("(function(){ return b; })")` threw `b is not defined` when the function it
returned was called, even though `eval("b")` at the same spot read the same
binding fine.

A direct eval's scope is **lexical**: the closure keeps the eval site's bindings
after that call has returned. Broiler made the caller's bindings reachable by
installing them as an overlay for the duration of the eval and withdrawing it on
return — right for code the eval *runs*, wrong for code the eval *creates*. The
names stop resolving at exactly the moment such a function is first called.

So a function created by directly-evalled code now captures those bindings — as
one created inside a `with` block already captured its with-chain — and
re-establishes them for the duration of a call. The live `JSVariable` objects are
captured rather than their values, so the binding stays shared in both
directions: a later write by the enclosing function is visible to the closure,
and a write by the closure lands on the caller's binding rather than on a fresh
global.

**Consulted only after every ordinary scope has failed**, on the read and the
write path alike, so nothing that resolves today resolves differently. Placing it
alongside the eval-binding walk instead broke Annex B block-level function
declarations, which own their name through `globalVars` and must not be shadowed
by the snapshot (`Issue619.AnnexBEvalFuncBlockScoping`,
`Issue912EvalHoistChar`) — worth knowing before anyone tries to "simplify" the
placement.

**Where it came from.** Five reports of `b is not defined` on google.com. Its
module loader is `function(e){return eval(e)}(src)` with `src` being
`0,function(){b(2,57,1,w)}` — the result stored and invoked later by the bundle.
The fragment that made it readable came from the program dump added for exactly
that purpose, which has since landed upstream.

**Why it is not listed for the pixel suites.** It decides whether page script
runs at all rather than what any of it paints, and the pixel suites do not
execute a loader of this shape. Its behaviour is unit-tested inside the patch
(`DirectEvalClosureScopeTests`).

**When it lands upstream:** bump the pointer and delete this patch.

### `0002` — the message said only that *something* was read off nothing

`Cannot read properties of undefined`, with no property named. On minified code
that does not locate the line, let alone the cause: the report it came from is a
TypeError at column 33839 of a 61 908-character line. Browsers append
`(reading 'foo')`; so does this.

Only the **computed** read was affected. A static one (`u.foo`) already named its
property, and a literal computed key (`u['foo']`) folds into that same path —
which is why it went unnoticed. The variable-key form `I[Y]` is what minified
code actually emits, and the only one that reached the generic message.

An **object** key is deliberately left undescribed: `GetValue` throws before
`ToPropertyKey` precisely because `ToObject(base)` comes first (6.2.5.5), so
describing such a key would run its `toString`/`@@toPrimitive` — user code, in an
order the spec forbids. A diagnostic does not get to change evaluation.

**Found while testing and not fixed here:** a numeric variable key
(`var k = 3; u[k]`) does not throw at all, it evaluates to `undefined`. That is a
separate defect in the indexed read path; the test records it rather than
covering for it.

**Not listed for the pixel suites** — it changes an error message, nothing a page
paints. `UndefinedPropertyReadMessageTests` covers it.

**When it lands upstream:** bump the pointer and delete this patch.

### `0003` — a rejected promise nobody handled was reported nowhere

A browser reports it as `Uncaught (in promise)`. Here it vanished. Three
spellings, all silent: a `throw` inside `.then`, `Promise.reject` with no
`.catch`, and an `async` function that throws. On a page whose control flow is
mostly promises — which now means most pages — that is the bulk of its failures,
and the reason a `--diagnostic-dir` bundle could report **zero** JavaScript
failures for a page that plainly did not work.

**The report has to be deferred**, which is why this is a tracker and not a log
line inside `Reject`. `Promise.reject(x)` is already rejected before the `.catch`
on the next line runs, and an `await` attaches its handler a microtask after the
promise settles. Reporting at the rejection would therefore call almost every
correctly-handled rejection unhandled. So: collect at the rejection, withdraw
when a handler arrives, and let the host ask what is left once its microtask
checkpoint has drained.

**Both places a promise becomes rejected are tracked**, because they are
genuinely two — `Reject`, and the `(value, state)` constructor that
`Promise.reject` and `CreateResolvedOrRejectedPromise` use to mint one already
rejected. Hooking only `Reject` caught the throw-inside-`then` case and neither
of the other two.

**Off by default**, so no existing run changes shape: `Rejected` and `Handled`
read one `bool` and return. Wiring it to `window.onunhandledrejection` — which
would make it always-on and is the point of having it — is the natural next step
and is deliberately not in this patch.

**The main repo builds without it.** `Broiler.Cli` reports what the tracker
collects, and `Broiler.JS` references nothing in this repository, so the type
could not be moved here the way a `Broiler.Layout` type would be. Instead
`Broiler.Cli.csproj` and `Broiler.Cli.Tests.csproj` probe for the file and define
`BROILER_JS_REJECTION_TRACKING`, the same shape
`Broiler.Render.Stage.Benchmarks.csproj` uses for `BRasterParallelism`. Without
the patch the reporting compiles out and the capture behaves as before; with it
applied, `UnhandledPromiseRejectionTests` compiles in and covers it.

**Not listed for the pixel suites** — it reports a failure, it does not change
what a page paints.

**When it lands upstream:** bump the pointer, delete this patch, and drop the two
`BroilerJsRejectionTracking` probes with their `#if`s.

### `0004` — every engine error blamed the line that installed the error factories

A TypeError raised while navigating to html5test.com reported its origin as

```
at InitializeFactories:...\Engine\Core\JSValueCoreExtensions.cs:17,1
```

which reads as a crash inside engine start-up. It is not one. Line 17 installs a
factory delegate; the actual failure was an ordinary
`Cannot get property length of undefined` in the page's fourth inline script.
Worse, that frame was the same for **every** TypeError the engine can raise, so
it distinguished nothing — and it sent the first reader of the report into the
wrong file.

`JSException` records the engine method that raised an error as the first frame
of the JavaScript stack, taken from the
`[CallerMemberName]`/`[CallerFilePath]`/`[CallerLineNumber]` trio that
`JSEngine.NewTypeError` and its siblings declare. Runtime and Storage cannot call
those directly — they do not reference the Engine assembly that knows how to
build a `JSError` — so they raise through a factory delegate. The delegates were
`Func<string, Exception>`, which has nowhere to carry caller info, so the
compiler filled it in at the only place those arguments were written: the wiring
lambda in the module initializer. **Nine delegates across three initializers,
nine constants.**

Caller-info attributes are honoured on a delegate's `Invoke` parameters, so the
factories are now named delegates that declare them — `JSErrorFactory` in Storage
(where `PropertySequence` needs it too) and `JSExceptionFactory` in Runtime for
the sites that want the `JSException` itself. Each throw site captures its own
position; the initializers forward what they are handed. The same TypeError now
reports `at Item:...\Runtime\JSUndefined.cs:41,1`, and two failures reaching one
factory can be told apart — which is what `EngineErrorOriginFrameTests` pins,
since "not the initializer" alone would be satisfied by any other constant.

**The JavaScript frames are untouched.** This is the frame above them, and it was
the only wrong one.

**Two things in the same trace are deliberately left alone**, because each is a
change to a web-visible API rather than a correction of wrong data, and the call
belongs to whoever owns that surface: `error.stack` opens with an **empty line**
where a browser writes `TypeError: <message>` (the `JSError` constructor computes
the stack before the `message` property exists), and it still carries an engine
source path — an absolute build-machine path, `D:\Broiler\...` in the original
report — that page script can read.

**Not listed for the pixel suites** — it changes a diagnostic, not anything a
page paints.

**When it lands upstream:** bump the pointer and delete this patch.

### `0005` — a `<noscript>` body was live content instead of raw text

The tokenizer parsed `<noscript>` contents as ordinary markup, which made the
fallback a live subtree: elements reachable with `querySelector`/`getElementById`,
an `<img>` that would be requested, a nested `<script>` a host would find and run.
All of it is content a page supplies for the case where scripts are **off**.

A scripting-enabled parser instead follows the generic raw text element parsing
algorithm for `noscript`, exactly as for `<script>` and `<style>`: the body becomes
one text node, nothing in it is live, and character references are not decoded. So
`noscript` joins `RawTextElements` in `HtmlTokenizer`.

Membership is **conditional on scripting being enabled** — with scripting off the
body must parse as markup so it can render — and the comment says so. The set can
be flat only because this engine has no scripting-disabled mode; if one is ever
added, that is the line that has to consult it.

**Nothing downstream sees a different serialized document.** `HtmlSerializer`
already listed `noscript` among the raw-text elements, so a text child round-trips
back to the same markup. `HtmlScriptScanner`, which is tokenizer-backed, stops
reporting a `<script>` nested inside a `noscript` — making true what
`PreloadScanner` already documented as true ("Broiler runs scripts, so the parser
treats a `noscript` body as text and nothing in it is ever loaded").

**The main repo does not depend on it, and CI is green without it.** The two
user-visible halves are fixed in this repository and stand alone:
`HtmlPostProcessor.StripNoscriptContent` stops the fallback rendering (commit
"Stop rendering `<noscript>` fallback while scripting is enabled"), and
`CaptureService`'s `NoscriptSpans` skip stops a `<script>` inside one executing —
that host extracts scripts with its own regex pass rather than through the parser,
so it needs its own skip whether or not this patch is applied. `NoscriptRenderingTests`
covers both and passes against the un-patched pointer; verified by reverting the
submodule and re-running. The DOM half — the fallback being raw text rather than a
reachable subtree — is what this patch adds, and its tests (`NoscriptRawTextTests`)
travel inside it.

**Not listed for the pixel suites** — it changes what the DOM holds, and the
rendering half is already live in this repository.

**When it lands upstream:** bump the pointer and delete this patch. The
`CaptureService` skip stays: it is about that host's regex extraction, not the parser.

### `0006` — a NUL inside a pattern looked exactly like the end of one

`Broiler.Regex`'s cursor reports "past the last character" by returning `'\0'` from
`Peek()`, and every end-of-pattern test compared against that sentinel. U+0000 is an
ordinary pattern character, so a pattern that merely *contained* one appeared to end
there. google.com's start page compiles a "no letters" character class whose first
range runs from NUL to space, and it died on the very first atom with
`Unterminated character class`.

**It arrives decoded**, which is the part worth keeping straight: the page builds the
class with `new RegExp(`*string*`)`, so the string literal's `\0` is consumed by the
*string* grammar and a real U+0000 is what reaches the pattern grammar. The escaped
spelling a regex **literal** preserves, `[\0-…]`, parsed correctly all along — the two
are different inputs to this parser, and only the decoded one was broken. Both are
covered by the tests, so the distinction cannot quietly rot.

Nor was it confined to character classes: a NUL as an atom, as a range end, in a group
name or inside `\p{…}` truncated the parse the same way. Every end-of-pattern test now
goes through an explicit `AtEnd` (plus `HasAt` for the one lookahead that wanted bounds
rather than a value); `Peek()` still returns `'\0'` past the end, but nothing reads it
as a signal.

**Separating the two fixed the mirror-image bug at the real end of input.** A pattern
ending in a lone `\` had no end-of-input guard on the path that reads the escaped
character, so it ran off the end of the string and raised `IndexOutOfRangeException` in
non-Unicode mode — an unhandled runtime fault where every caller catches
`RegexSyntaxException`. It is now the syntax error it always was.

**A nested submodule.** `Broiler.Regex` sits inside `Broiler.JS`, so applying this one
means committing in `Broiler.JS/Broiler.Regex`, pushing, bumping the gitlink **in
`Broiler.JS`**, and only then bumping `Broiler.JS` in this repository — two pointer
bumps, not one.

**No main-repo fallback, and none needed.** `JSRegExp.TryBuildBroilerForGaps` already
wraps the parse in a `try`/`catch` and falls back to the .NET translator on failure, so
the un-patched engine mis-routes this pattern rather than breaking the page — the
exception is real but swallowed, which is why it surfaced from a debugger rather than
as a page error. The fix cannot be staged at a main-repo layer either: the parser is
the whole of it, and nothing here can stand in for it.

**Not listed for the pixel suites.** Routing to `Broiler.Regex` happens only for
patterns that exercise a JS/.NET semantic gap (`GapScan`), and this class has none — it
compiles through the .NET translator with or without the patch, so no pixel moves
either way. Its behaviour is unit-tested inside the patch (`NulCharacterTests`).

**When it lands upstream:** bump both pointers and delete this patch.

### `0007` — two construct sites had a wording of their own

`new undefined()` said `cannot create instance of undefined`, and `new null()` the
matching `... of null`. No browser words it that way — V8, SpiderMonkey and JSC all
say `X is not a constructor` — and neither does the rest of this engine: `JSFunction`,
`JSSymbol`, `JSGenerator`, `JSGeneratorFunctionV2`, `JSReflect` and
`JSPromisePrototype` already raise `... is not a constructor`. These two were the only
sites left disagreeing, so this is a consistency fix rather than a new opinion.

**The throw it appears in is correct**, which is the part worth keeping straight.
html5test.com probes for WebRTC with
`new (window.RTCPeerConnection || window.msRTCPeerConnection || window.mozRTCPeerConnection || window.webkitRTCPeerConnection)(null)`.
Against an engine with no WebRTC every alternative is `undefined`, so this throws, the
page catches it, and the feature is correctly recorded as unsupported — a browser with
WebRTC disabled does the same. Only the message was wrong, and a wording no browser
produces reads as an engine fault in a trace where the right answer was in fact given.
See `docs/html5test-exceptions.md`.

**`InvokeFunction` is deliberately untouched.** `undefined is not a function` already
matches the browsers; sweeping it along "for consistency" would have moved it away from
the wording every engine agrees on.

**Not listed for the pixel suites** — it changes an error message, nothing a page
paints. `ConstructNonConstructorMessageTests` travels inside the patch and covers it,
including that the call-path message did not move.

**No main-repo fallback, and none is needed.** Nothing in this repository asserts either
message, so the un-patched engine behaves exactly as before; the cost of not applying it
is a non-standard string in a stack trace.

**When it lands upstream:** bump the pointer and delete this patch.

### `0008` — every `<video>` on a page was a solid black rectangle

A `<video>` with no decodable media painted its whole box black, and an `<audio>`
without `controls` laid out as a 300×32 black bar instead of not laying out at all.
`conformance-checkers/html/elements/track/src-isvalid` is 250 `<video>` elements and
`.../audio/src-isvalid` 250 `<audio>` ones; Chromium renders both as blank white pages
and Broiler rendered walls of black, matching at **14.4 %** and **19.8 %**.

HTML §4.8.9 is explicit that a video element with neither a poster frame nor video data
"represents … nothing", and the HTML rendering section's UA stylesheet makes
`audio:not([controls])` `display: none`. Checked against the reference browser directly:
an empty `<video>`, a `<video src="missing.mp4">` and a `<video autoplay><source></video>`
all paint transparent — the div behind each one shows through — and only `<video controls>`
draws anything, the control scrim. An `<audio controls>` is a 300×**54** light bar, not 32.

So the placeholder fill now follows `controls` rather than the element: a dark scrim under
a video's controls, a light bar under an audio's, and nothing at all without them. The
default audio height moves 32 → 54 to match. The three tests go to **100 %**.

**Listed for the pixel suites** (`scripts/apply-pending-wpt-patches.sh`). It moves any page
that merely contains a media element, and there is no main-repo half to fall back on: the
fill is set during the style cascade, where nothing downstream can tell a UA-injected
background from an author one.

**The main-repo tests moved with it, but not to the patched behaviour.**
`WptCompositingTests`'s three video cases asserted the black fill directly. They now assert
the replaced box's *extent*, against an author background they set themselves — true with
the patch and without it — because a fill the element is not supposed to draw is not
something a test should pin. Transparency is pinned by the WPT tests above instead.

**When it lands upstream:** bump the pointer, drop the entry from
`scripts/apply-pending-wpt-patches.sh`, and delete this patch.

### `0009` — `for(;)` did not fail to compile, it ran forever

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
