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

The numbering is an ordering hint, not a dependency: the two below touch disjoint files and each
applies to the pinned pointer on its own, in either order. `git apply --check` both before starting
if you are applying more than one.

---

## `0001-js-nested-member-call-clobbers-outer-call.patch`

- **Targets:** `Broiler.JS`
  (`Broiler.JavaScript.Compiler/Expressions/FastCompiler.VisitCallExpression.cs`, plus regressions
  in `Broiler.JavaScript.Integration.Tests/SuspendedCallArgumentTests.cs`)
- **Subject:** *Stop a nested member call clobbering the outer call inside a generator body*
- **Based on:** `ab5f797a`, the currently pinned pointer

`trace.push(items.join('+'))` does not run inside an `async` function. No error is raised and the
statement after it runs normally, so the failure is **silent** — and `console.log(list.join(', '))`
is the same shape, which is what makes a small-looking bug worth its patch.

The receiver and the resolved method of a member call live in two temps taken from a per-function
pool, and the arguments are compiled *before* those temps are acquired, so a nested call in the
arguments is handed the very same two back. Ordinary code survives that because both values are on
the IL evaluation stack by the time an argument runs. A generator or async body does not:
`FlattenBlocks`, the generator rewrite's last pass, lifts any block-valued operand's statements out
as siblings and passes a spilled temp in its place, and a nested member call compiles to exactly
such a block. Once hoisted, *its* two assignments run between the outer call's assignments and the
outer call's invocation, so the outer call invokes the inner callee on the inner receiver.
`var r = t.push(g.join('+'))` leaving `r` holding the *inner* call's value is what named the cause.

This is the same defect that was fixed for `obj.hit(await Promise.resolve(1))`, but the suspension
was never the cause — the hoist is. That fix guarded on an AST scan of the source for `await` or
`yield`, which cannot see the far more common plain nested call. The guard now asks what the
operands **compiled to**: a bare parameter or a constant emits no statements, so nothing can be
hoisted out of it and the pool stays safe; anything else gets locals on the call's own block. That
direction of approximation costs two locals and cannot be wrong, where the old one could and was.
Ordinary functions still pay nothing. The private-name key temp is pooled the same way and reachable
the same way by `this.#a(this.#b())`; it gets the same treatment.

Eleven regressions come with it, all without an `await` or a `yield`; ten of the eleven fail before
the change. Engine suites with the patch applied: integration 5178/5179, built-ins 2215/2215,
compiler 1400/1402, modules 104/104, and core, parser, runtime and module-extensions clean — the
three failures are pre-existing and unrelated (one documentation-file check, two known-gap
parameter-shadowing pins) and reproduce with the change stashed.

**There is no main-repo fallback, and there cannot be one.** The defect is in how the JavaScript
compiler allocates temps for a member call; nothing at a main-repo layer can intercept call
compilation. Until this patch is applied, every `async` function in every page that writes
`a.b(c.d())` silently skips that statement. That is the cost of the pointer staying where it is —
worth stating plainly rather than leaving to be rediscovered.

---

## `0002-js-json-module-default-export.patch`

- **Targets:** `Broiler.JS` (`Broiler.JavaScript.Modules/JSModuleContext.cs`, plus the new
  `Broiler.JavaScript.Modules.Tests/JsonModuleTests.cs`)
- **Subject:** *Give a JSON module the ES namespace ESM wants and require the value CommonJS wants*
- **Based on:** `ab5f797a`, the currently pinned pointer

`import d from './data.json'` was `undefined`, which made JSON modules unusable. One wrapper served
both callers — `module.exports = <json>` — which is exactly what CommonJS `require` wants and
exactly wrong for ESM: a default import reads `.default` off the parsed value and finds nothing. A
file whose whole content is `null` was worse than unusable: `JSModule`'s exports setter refuses
null, so it threw on load.

**The decision.** The two specifications disagree about what a JSON file exports, and one object
cannot satisfy both. ES2025 gives a JSON module exactly one export, `default`, holding the parsed
value, and no named exports; CommonJS hands back the parsed value itself. So the module now *stores*
the ES namespace — `module.exports = { default: (<json>) }` — and the CommonJS view unwraps it.
`LoadModuleAsync` takes an `esModule` flag for that, defaulting to the ES view; the two `require`
call sites pass `false`. Storing the namespace is also what makes arrays, numbers, strings, booleans
and `null` work — none of them can carry a `default` property, so decorating the parsed value could
never have fixed them.

**Deliberate deviation, pinned by a test:** `import { a } from './x.json'` used to read `a` off the
parsed object and is now `undefined`. Per spec it is a link error, which needs whole-module link
analysis this engine does not do; browsers and Node both reject the form, so nothing portable
relied on it.

Fourteen regressions in `JsonModuleTests`, eleven of which fail before the change. Engine suites
with the patch applied: modules 118/118, module-extensions 5/5, built-ins 2215/2215, integration
5167/5168, compiler 1400/1402 — the three failures pre-existing and unrelated.

**Main-repo fallback:** none, and none is needed to keep CI honest — without the patch JSON modules
stay exactly as broken as they were, rather than half-working. `BridgeModuleContext` (the main
repo's `JSModuleContext` subclass) overrides only `Resolve` / `GetModuleDirectory` /
`ReadModuleSourceAsync`, so it is unaffected either way; it was rebuilt and its
module/subdocument/import tests run against the patched engine, 322/323, the one failure being the
known iframe-module drain gap.
