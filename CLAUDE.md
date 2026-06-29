# Broiler — Claude Code project instructions

These notes persist across sessions. Keep them accurate; update when the
workflow changes.

## Submodules: modify them, but deliver the change as a PATCH — never push

`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`, `Broiler.JS`, and
`Broiler.Graphics` are git submodules (see `.gitmodules`), each with its own
remote under the `MaiRat/` org. The renderer, CSS engine, DOM, JS engine, and
graphics core live there — **a fix often belongs in a submodule, not the main
repo.** Do not contort a change into the main repo just to avoid touching a
submodule; write the fix at its correct layer.

**Hard rule: do NOT push submodule remotes, and do NOT bump submodule pointers
(gitlinks) in the parent.** Instead, capture each submodule change as a patch
file committed under `patches/` and attach it to the parent PR. A maintainer
applies the patch inside the submodule, pushes it there, and bumps the pointer
in a follow-up. (Pushing a submodule pointer the session can't also push the
commit for would break CI's submodule clone; and submodule remotes are outside
the session's GitHub scope anyway — see the caveat below.)

### Submodule change workflow

1. Edit files inside the submodule directory and verify the fix by building the
   parent (the build compiles submodule source in place).
2. Generate a patch from the submodule:
   ```sh
   cd <Submodule>
   git checkout -b _tmp_patch
   git add -A && git commit -m "<message>"
   git format-patch -1 --stdout > ../patches/NNNN-<slug>.patch
   git checkout <pinned-sha> && git branch -D _tmp_patch   # leave submodule clean
   ```
3. Revert the submodule working tree so its pointer stays unchanged
   (`git submodule status` should show the original SHA, no `+`).
4. Add the patch (and an entry in `patches/README.md`) plus any main-repo
   changes, commit, and push the **parent** branch only.
5. In the PR, note which submodule the patch targets so it can be applied and
   the pointer bumped separately.

If the same bug also needs to work on CI *now* (before the patch is applied),
add an equivalent fallback fix at a main-repo layer and say so in the patch
index — e.g. issue #1119's `Broiler.HTML` `HtmlParser.AppendCanonicalNode`
text-node coalescing is shipped as `patches/0001-…`, with the active fallback
`DomBridge.RemoveRenderCommentNodes` in the main repo until the patch lands.

### Why not push (egress-scope caveat)

Pushes go through the session's git proxy, which only authorizes repos in the
session's GitHub scope. Pushing a submodule remote (e.g. `MaiRat/Broiler.HTML`)
returns **403** and must not be retried or routed around (per
`/root/.ccr/README.md`). The patch-file workflow above sidesteps this entirely
and keeps the parent buildable. (If a future session genuinely needs to push
submodules, that requires adding `MaiRat/Broiler.*` to the environment's GitHub
access scope — an environment-config change, not something to attempt from
inside the container.)

## Build & test

- .NET 10 SDK; build with `dotnet build <project> -c Release`.
- The `SessionStart` hook (`.claude/hooks/session-start.sh`) provisions the SDK
  and initializes submodules.
- WPT runner: `dotnet run --project src/Broiler.Wpt -- --wpt-dir tests/wpt
  --reference-dir tests/wpt/references [--subset <path>] [--failure-images <dir>]`.
  Pixel pass threshold is 99% match (≤1% differing pixels).
- WPT triage status and per-cluster history:
  `docs/roadmap/wpt-triage-and-diagnostics.md`.
- Some `Broiler.Cli.Tests` (PDF conversion) and some `Wpt_*_MatchesReference`
  tests can fail in a bare container for environmental reasons (missing
  `Broiler.Pdf` app, font differences) — baseline before attributing a failure
  to your change.

## Conventions

- Model identifiers must never appear in commits, PR text, or code — chat only.
- Develop on the task's designated branch; never push to a different branch
  without explicit permission.
