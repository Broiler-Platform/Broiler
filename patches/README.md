# Submodule patches awaiting application

Fixes that belong in a submodule but could not be pushed to its remote: the push returns **403**
because the `Broiler-Platform/Broiler.*` repositories are outside this session's GitHub scope. Each
patch is generated with `git format-patch`, and the submodule pointer is deliberately **not** bumped
— CI clones the submodule by pointer, and bumping one to a commit that was never pushed would break
the clone.

This directory is a backlog, not an archive: a file is deleted once its fix is upstream, and the
numbering restarts from `0001` against whatever is left. So a `patches/NNNN` reference in an older
commit message or document is almost certainly dangling. Identify a patch by its **commit subject**
instead, and check whether it is live with:

```sh
git -C <Submodule> log --oneline --grep '<subject>'
git merge-base --is-ancestor <sha> HEAD
```

## Applying one

```sh
cd <Submodule>
git am ../patches/<file>.patch
git push origin HEAD          # from an environment whose scope includes the submodule remote
cd ..
git add <Submodule>           # bump the pointer only after the push succeeds
```

---

## `0001-js-for-await-unsettled-step-result.patch`

- **Targets:** `Broiler.JS` (`Broiler.JavaScript.Runtime/JSIterator.cs`, the new
  `Broiler.JavaScript.Runtime/AsyncIterationStep.cs`, `Broiler.JavaScript.Runtime/IElementEnumerator.cs`,
  `Broiler.JavaScript.LinqExpressions/…/IElementEnumeratorBuilder.cs`,
  `Broiler.JavaScript.Compiler/Statements/FastCompiler.VisitFor.cs`, plus the new
  `Broiler.JavaScript.Integration.Tests/ForAwaitUnsettledResultTests.cs`)
- **Subject:** *Stop for-await deadlocking on a step result that is not already settled*
- **Based on:** `2619c49`, the currently pinned pointer

`for await…of` unwrapped the result of `next()` inside `JSIterator` with
`promise.Task.GetAwaiter().GetResult()` — a blocking wait, on the one thread allowed to run a
context's JavaScript. That works only while the promise is **already settled**, which is why every
shape the suite covered passed: an array, an async generator, an iterator returning
`Promise.resolve(record)`. The moment `next()` hands back the ordinary `something.then(…)`, the job
that would settle it can never run, because the queue that runs it drains on the way out of the
execution the thread is stuck inside — `JSMicrotaskQueue`'s own documentation names that exact
pattern as the one it cannot support. **The agent hangs until the process is killed**, which is why
it never showed up as a failing test.

The step becomes three pieces with the state machine's own `await` between them:
`IElementEnumerator.AsyncNextRaw` calls `next()` and hands the result back unexamined, the compiled
loop awaits it, and `AsyncIterationStep` reads `done`/`value` off the settled record.

Seven regressions come with it, each of which **hung rather than failed** before the change. With the
patch applied the whole engine suite passes bar its pre-existing failures (two known-gap
parameter-shadowing cases and one documentation-file check).

**Main-repo fallback, and what it gates.** The bug is reachable from any page that writes
`for await (const x of asyncThing)` where the iterator returns a chained promise, and nothing in the
main repo can intercept that. What the main repo *can* do — and does — is refuse to hand pages one
more way to hit it: `ReadableStream.prototype.values` and its `@@asyncIterator` are written, correct
and verified, and are left commented out in
`src/Broiler.HtmlBridge.Dom/Polyfills/streams-and-file-reader.js` until this patch is live. Without
the patch, installing them would turn `for await (const chunk of response.body)` from a `TypeError` a
script survives into a capture that never settles. **Uncomment both statements when the patch lands**
— the comment there says so, and `ReadableStreamTests.Async_Iteration_Is_Absent_Until_The_Engine_Can_Drive_It`
pins the current state so the switch is a decision rather than a drift.
