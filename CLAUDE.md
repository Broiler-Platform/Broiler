# Broiler — Claude Code project instructions

These notes persist across sessions. Keep them accurate; update when the
workflow changes.

## Submodules: you may (and should) modify them

`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`, `Broiler.JS`, and
`Broiler.Graphics` are git submodules (see `.gitmodules`), each with its own
remote under the `MaiRat/` org. The renderer, CSS engine, DOM, JS engine, and
graphics core live there — **a fix often belongs in a submodule, not the main
repo.** Do not contort a change into the main repo just to avoid touching a
submodule; put the fix at its correct layer.

Prefer the correct layer over a main-repo workaround. Example: the inline
white-space / comment-collapse fix (issue #1119) belongs in
`Broiler.HTML/Source/Broiler.HTML.Dom/Parse/HtmlParser.cs`
(`AppendCanonicalNode`, coalesce consecutive text nodes); the equivalent
main-repo serialization patch in
`src/Broiler.HtmlBridge.Dom/DomBridge.Serialization.cs`
(`RemoveRenderCommentNodes`) is only a fallback for when the submodule remote
cannot be pushed.

### Submodule change workflow

1. Edit files inside the submodule directory.
2. In the submodule: create/checkout the working branch, commit, and
   `git push -u origin <branch>` to the submodule's own remote.
3. In the main repo: stage the updated submodule pointer (gitlink) plus any
   main-repo changes, commit, and push.
4. Open/refresh the parent PR; if the submodule change is on a branch (not the
   submodule's default branch), note in the PR that the submodule branch must
   be merged too, so the pinned SHA stays reachable.

### Egress-scope caveat (read before relying on a submodule push)

Pushes go through the session's git proxy, which only authorizes repos in the
session's GitHub scope. If the session is scoped to `mairat/broiler` only,
pushing to a submodule remote (e.g. `MaiRat/Broiler.HTML`) returns **403** and
must not be retried or routed around (per `/root/.ccr/README.md`). When that
happens:

- Do **not** point the parent at an unpushable submodule SHA — CI cannot clone
  it and the build breaks.
- Land the fix via the main-repo fallback layer instead, and report that the
  submodule remote needs to be added to the session scope to land the change at
  its proper layer.

To make submodule pushes work permanently, add the relevant `MaiRat/Broiler.*`
repositories to the environment's GitHub access scope.

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
