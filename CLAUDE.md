# Broiler — Claude Code project instructions

These notes persist across sessions. Keep them accurate; update when the
workflow changes.

## Submodules: modify them; push if allowed, otherwise deliver as a PATCH

`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`, `Broiler.JS`, and
`Broiler.Graphics` are git submodules (see `.gitmodules`), each with its own
remote under the `MaiRat/` org. The renderer, CSS engine, DOM, JS engine, and
graphics core live there — **a fix often belongs in a submodule, not the main
repo.** Do not contort a change into the main repo just to avoid touching a
submodule; write the fix at its correct layer.

**Rule: try to push the submodule change to its remote and bump the pointer; if
the push is denied, fall back to a patch file.** First push the submodule commit
to its `MaiRat/` remote and — *only if that push succeeds* — bump the submodule
pointer (gitlink) in the parent. If the push is **denied** (403 — the submodule
remote is outside the session's GitHub scope; see the caveat below), do NOT bump
the pointer: instead capture the change as a patch file under `patches/` and
attach it to the parent PR for a maintainer to apply. Never bump a pointer whose
commit you could not push — CI clones the submodule by pointer and would break.

### Submodule change workflow

1. Edit files inside the submodule directory and verify the fix by building the
   parent (the build compiles submodule source in place).
2. Commit inside the submodule and **attempt the push**:
   ```sh
   cd <Submodule>
   git checkout -b <branch>            # or the designated submodule branch
   git add -A && git commit -m "<message>"
   git push origin HEAD                # attempt the push
   ```
   - **Push succeeds:** bump the submodule pointer in the parent
     (`git add <Submodule>` from the parent root) and push the parent branch.
     Done — no patch needed.
   - **Push denied (403):** do NOT retry or route around it (per
     `/root/.ccr/README.md`). Fall back to the patch step below.
3. Patch fallback — generate a patch and leave the pointer unchanged:
   ```sh
   cd <Submodule>
   git format-patch -1 --stdout > ../patches/NNNN-<slug>.patch
   git checkout <pinned-sha>           # revert working tree so `git submodule
   git branch -D <branch>              #   status` shows the original SHA, no `+`
   ```
4. Add the patch (and an entry in `patches/README.md`) plus any main-repo
   changes, commit, and push the **parent** branch only.
5. In the PR, note which submodule the patch targets so it can be applied and
   the pointer bumped separately.

If the same bug also needs to work on CI *now* (before a patch is applied), add
an equivalent fallback fix at a main-repo layer and say so in the patch index —
e.g. issue #1119's `Broiler.HTML` `HtmlParser.AppendCanonicalNode` text-node
coalescing is shipped as `patches/0001-…`, with the active fallback
`DomBridge.RemoveRenderCommentNodes` in the main repo until the patch lands.

### Egress-scope caveat

Pushes go through the session's git proxy, which only authorizes repos in the
session's GitHub scope. If a submodule remote (e.g. `MaiRat/Broiler.HTML`) is
outside that scope the push returns **403**; that 403 is the signal to fall back
to the patch workflow above — not to retry or route around it (per
`/root/.ccr/README.md`). Granting push access means adding `MaiRat/Broiler.*` to
the environment's GitHub access scope — an environment-config change, not
something to attempt from inside the container.

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
