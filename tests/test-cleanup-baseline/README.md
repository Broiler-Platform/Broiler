# Test-cleanup baseline

Recorded evidence for
[the test-suite retirement roadmap item](../../docs/ROADMAP.md#retire-obsolete-test-suites-and-historical-test-artifacts).

Batch 1 of that item requires the state of every affected suite to be captured
*before* anything is deleted, so that each later batch's "matches or improves on
the recorded baseline" gate has something to compare against. Without it a
cleanup that removes a failing test looks identical to one that fixes it, and a
regression introduced along the way hides behind a suite that was already red.

Two things live here:

- [`inventory.md`](inventory.md) — every file, method, production flag, solution
  root, script filter, and documentation link the cleanup proposes to touch,
  with what happened to it.
- [`focused-results.md`](focused-results.md) — measured pass/fail counts for the
  affected suites at the pre-cleanup commit, including the failures that were
  **already red for unrelated reasons**. Those matter most: they are the ones
  that would otherwise be miscounted as cleanup damage.

Delete this directory together with the roadmap item it serves. It is a
transitional record, not a permanent baseline — unlike
[`tests/wpt-baseline`](../wpt-baseline/), which CI keeps current.
