# Submodule patches awaiting upstream

Each file here is a `git format-patch` of a change that belongs in a submodule whose
remote is outside this session's GitHub scope, so the push returned 403 and the submodule
pointer was deliberately **not** bumped. Apply a patch inside its target submodule, push
there, then bump the gitlink in the parent — a pointer whose commit is not on the remote
would break CI, which clones submodules by pointer.

Delete a patch once its fix is upstream. This directory is a backlog, not an archive:
numbering restarts from `0001` against whatever is left, so the same number names
different changes at different times. Refer to a patch by its **commit subject**, never
by its number.

The previous entries here are gone because they are upstream — their submodule gitlinks
now point at commits that contain them, so their patch files were deleted:

- the Broiler.Regex Unicode/case-folding/iterative-matcher work and the Broiler.JS
  Unicode-routing work (`Broiler.JS` had reached `d20e506`, `Broiler.Regex` `4df3fb8`);
- the three Track 1 correctness patches — `Enumerate symbol-keyed own properties in
  insertion order (§10.1.11.1)`, `Run async and generator bodies under the strict-mode
  runtime flag`, and `Raise the missing early SyntaxErrors for var/lexical conflicts,
  labelled-function loop bodies, and script exports` — which pushed to
  `Broiler-Platform/Broiler.JS` once that remote was added to the session's scope, so the
  `Broiler.JS` gitlink was bumped from `d20e506` to `f1b78df`.

## Open patches

### `Broiler.JS` — Give a module's top-level lexicals their own environment

Target submodule: **`Broiler.JS`** (compiler + module runtime). Push to
`Broiler-Platform/Broiler.JS` returned 403 (outside this session's scope), so the gitlink is
**not** bumped.

A module's top-level `let`/`const`/`class` bindings were published into the realm's shared global
lexical environment, exactly as a script's top-level lexicals are, so every module shared one
realm-wide slot per name. A module that declared a top-level `const x` and, while its body was
still running, triggered a transitive import of another module that also declared a top-level
`const x` then hit the first module's read-only binding and threw "Cannot assign to read only
variable" (sibling imports at one level escaped only because each body had returned before the next
ran). The patch keeps a module's top-level lexicals local to its compiled body — for exported and
non-exported declarations alike — which is also the spec-correct scoping, and separately makes
`export *` respect export precedence so a star re-export no longer overwrites (or throws over) a
name the module already exports. Regressions live in the patch's `ModuleScopeIsolationTests`.

There is **no active main-repo fallback**: the fix is internal to the `Broiler.JS` compiler and
runtime, defines no type the parent references, and reverting the submodule tree leaves the parent
building. Until the patch is applied upstream and the gitlink bumped, CI clones the pinned
submodule commit, which does not yet contain it, so the module scope-isolation cases stay red on
CI.

Apply with `git -C Broiler.JS am ../patches/0001-module-top-level-lexical-own-environment.patch`,
push `Broiler.JS`, then bump the gitlink.
