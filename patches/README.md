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

None. Every patch that was here has landed upstream and its gitlink is bumped, so there is
no fallback the parent depends on — the pinned submodule commits already contain the fixes.
