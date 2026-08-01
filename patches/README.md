# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their remote from
the session that wrote them: the git proxy only authorises repos in the session's
GitHub scope, so a push to a submodule remote outside it returns **403**. Rather
than bump a submodule pointer at a commit CI cannot clone, the change is captured
here as a `git format-patch` file for a maintainer to apply.

## Applying

```sh
cd <Submodule>
git am ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

Delete the patch file and its row below once the pointer is bumped.

## Index

| Patch | Submodule | Summary |
| --- | --- | --- |
| `0046-js-octane-suite-engine-fixes.patch` | `Broiler.JS` | Five independent engine defects, one per failing Octane 2.0 suite, each reproducible in a few lines with no benchmark involved. **(1) Non-strict `eval` dropped `var` initializers inside nested functions and leaked them to the global object** — the direct-eval var-environment routing in `VisitVariableDeclaration` applied to every `var` declarator in the eval'd program, not just the eval's own top level, so the store went to a binding indexed off the global object while reads resolved to the function's hoisted local: `eval("(function(){ var x = 42; return x; })()")` returned `0` and defined a global `x`. Every other direct-eval hoisting site already guarded on `Function == null`. (CodeLoad.) **(2) `obj == null` ran ToPrimitive on the object** — IsLooselyEqual (7.2.14) makes Object-vs-null/undefined `false` at step 11 with no coercion, so the ubiquitous null-check idiom was calling user code; in Crypto, `if (r == null)` on a BigInteger reached `toString` → `toRadix` → `divRemTo` → the same test and recursed until the stack was exhausted, aborting the process and losing the whole suite. **(3) `undefined + x` string-concatenated** — `undefined + undefined` was `"undefinedundefined"` instead of `NaN`, because the numeric branch of `+` keyed off `CanBeNumber`, which excludes undefined for the relational operators; PdfJS's `this.end = (start + length) || bytes.length` with both arguments omitted stored that truthy string, so every stream reported a NaN length and parsing rejected the document as malformed. **(4) The last expression of a C-style `for` head's comma list was parsed and then discarded** — it stayed in `node` while the `AstSequenceExpression` built from the earlier operands replaced it, so `for (i = 0, len = a.length; …)` never assigned `len`; Typescript's `Binder.resolveBases` then reused the previous loop's bound and walked `type.implementsTypeLinks` past its end. **(5) Added the `read` shell builtin** next to `print` in `--script-host` mode — Emscripten's shell preamble references it unconditionally (`Module.read = read`) the way d8 and SpiderMonkey provide it, so its absence was a `ReferenceError` before zlib ran a line; `read(path)` returns a string, `read(path, "binary")` a `Uint8Array`, and a missing file raises a JavaScript `Error`. **No main-repo fallback is possible** — these are JavaScript semantics implemented entirely in `Broiler.JS`, and the Octane workflow builds the shell from the pinned pointer, so its committed results keep showing the five failures until this is applied. Measured with the patch applied: Crypto, PdfJS, CodeLoad and Typescript go from `crash`/`error` to `ok` (scores 127, 321, 83.4, 1009), and zlib gets past load and runs its measured loop. Full `Broiler.JS` suite green: 13 projects, 7281 tests. Independent of 0040–0045: no file overlap. |

This patch is deliberately **not** listed in `scripts/apply-pending-wpt-patches.sh`'s
`PENDING_PATCHES`. That list exists to keep the WPT run honest about fixes the
pinned pointers do not carry, and every entry has been vetted against the WPT
numbers. This one changes core JavaScript semantics (`+`, `==`, the `for` head,
`eval` scoping) that a large share of WPT exercises indirectly, so adding it
there would move that job's results without the re-baselining of
`tests/wpt/failed-tests` that such a move needs. It belongs in `PENDING_PATCHES`
only alongside that re-baseline.
