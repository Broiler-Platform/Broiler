# Pending submodule patches

Fixes that belong in a submodule (`Broiler.HTML`, `Broiler.CSS`, `Broiler.DOM`,
`Broiler.JS`, `Broiler.Graphics`) but could not be pushed to their remote from
the session that wrote them: the git proxy only authorises repos in the session's
GitHub scope, so a push to a submodule remote outside it returns **403**. Rather
than bump a submodule pointer at a commit CI cannot clone, the change is captured
here as a `git format-patch` file for a maintainer to apply.

The directory is a backlog, not an archive: it holds only what is *currently*
pending. A patch is deleted from here, along with its row below, once its fix is
upstream and the submodule pointer is bumped.

## Applying

```sh
cd <Submodule>
git am --keep-cr ../patches/NNNN-<slug>.patch
git push origin HEAD
cd ..
git add <Submodule>        # bump the pointer only after the push succeeds
```

**`--keep-cr` is not optional for a patch that touches a file with CRLF line endings**, which
several `Broiler.JS` sources are (mixed CRLF and LF, within one file). `git am` runs the patch
through `mailinfo`, which normalizes the line endings of the diff body unless told not to — so the
context lines stop matching the file and the apply fails with *"patch does not apply"* on a patch
that is perfectly good. `git apply` does not have the problem, which is exactly why it is not the
check: these instructions use `am`, so `am` is what a patch has to survive. Verified per patch by
applying it to a clean checkout of the pinned pointer and diffing the result against the branch it
was generated from.

## Exercised on CI before they land

`scripts/apply-pending-wpt-patches.sh` applies the patches listed in its
`PENDING_PATCHES` array to the checked-out submodule trees before the WPT run, so
a pending fix is reflected in CI's numbers rather than waiting on the pointer. It
is idempotent — a patch already contained in the pinned pointer reverse-applies
and is skipped — so an entry stops applying by itself once a maintainer lands it.

## Index

**Empty — nothing is pending.** `0123-css-cascade-rule-index` was the last entry;
it is upstream as `Broiler.CSS` `377c6dd` on `claude/css-cascade-rule-index` and
the submodule pointer is bumped, so the fix now reaches CI through the pointer
rather than through `apply-pending-wpt-patches.sh`. Its `PENDING_PATCHES` entry is
removed for the same reason: the script's idempotence guard would have skipped it
from here on, and an entry that can only ever skip is noise.

<!-- Retired index row, kept for the record of what 0123 claimed and measured:

| `0123-css-cascade-rule-index` | `Broiler.CSS` | **The cascade tested every rule of every sheet against every element; it now tests only the rules an element's own id, classes and tag can reach.** Multithreading roadmap item #11 — the item that document's Phase 0 measurements identify as its highest-value one, ahead of every parallel item including the rasterizer, and single-threaded work. The evidence it was written against: on the roadmap corpus's rule-heavy page (700 `<div>`s, two classes each, against a 900-rule sheet) `parse+cascade` was **96.5% of a 5.2 s render**, and `RuleScalingBenchmarks` — which holds the *matched* rule count fixed at four while growing the sheet — measured cost and allocation both linear in **total** rules: 32× the rules gave 30.8× the time and 32.0× the bytes. `CssCascadeRuleIndex` flattens the sheet set into document order once and files each rule under the simple selector its subject must carry (id / class / type), with everything else in a universal bucket; an element merges the buckets its own keys reach and tests those. **The correctness argument is one-sided by construction and that is the whole point**: a key is a *necessary* condition for a match, never a sufficient one, candidates still go through the unchanged selector matcher, so the index can only be wrong by *omitting* a rule — and every case that is not certainly narrower resolves to the universal bucket (a bare `:is()`/`:not()`, an attribute-only compound, a namespaced type, an escaped identifier, anything the key scanner does not model). Candidates come back in **document order**, via a k-way merge of the buckets rather than a sort, so the cascade's source-order tie-break is untouched — a rule that never matched never advanced the tie-break counter, so visiting only candidates yields the same winners. `@media`/`@supports` are resolved once at index-build time (they depend on the environment and the feature oracle, both fixed for an index's lifetime) so a non-applying group costs nothing per element; `@container` depends on the element, so those conditions travel with the entry and are evaluated per candidate. The index is memoized against a **sheet** generation bumped only by sheet/environment changes — deliberately not by DOM mutation, which bumps the computed-style caches constantly and would otherwise rebuild the index on every tree edit. **The linear scan is kept and kept reachable** behind `CssStyleEngine.UseRuleIndex`, as the index's oracle: `CssCascadeRuleIndexTests` (22 cases) asserts the two cascades agree property by property — id/class/type keying, source-order tie-breaks, a rule reachable through two keys, combinators, pseudo-elements, the six unkeyable selector shapes, matching and non-matching `@media`, `@supports`, nested groups, late-added sheets, `ClearStyleSheets`, a viewport change — and over a generated 1200-rule sheet against a 120-element document. **Measured, and the exit gate is the scaling shape rather than a single number**: with `RuleScalingBenchmarks` holding the matched set at four and growing only the rules that cannot match, 32× the rules used to cost 30.8× the time and 32.0× the bytes and now costs **1.64× the time and 1.13× the bytes** — 114.9 ms → 15.74 ms at 100 rules, 3 543.6 ms → **25.89 ms** at 3 200 (136.9×), with allocation 5 817.9 MB → **9.69 MB** (600×). On a whole render the corpus `rules` page goes **5 218.96 ms → 1 841.71 ms** end to end and its `parse+cascade` stage **5 035.35 ms → 1 698.11 ms** (2.8× and 3.0×) — the smaller figure is Amdahl, since that stage also parses 110 296 characters and then cascades the rules that do match. **No rendering change**: `css/css-backgrounds` + `css/CSS2/linebox` (62 tests) is identical before and after — same pass/fail/skip on every test and the same 98.62245927777205% average pixel match to fourteen digits. Component suites: `Broiler.CSS.Tests` 341/341, `Broiler.CSS.Dom.Tests` 383 passed with the two `CssDomArchitectureTests` failures that are **pre-existing** (verified on a clean tree: 361 passed, the same 2 failed). Applies cleanly to the pinned `Broiler.CSS` pointer (`edc9fa9`) and survives `git am --keep-cr` — verified by applying it to a fresh clone of the pin and diffing the result against the branch it was generated from (identical tree). **Listed in `scripts/apply-pending-wpt-patches.sh`**: unlike a thread-safety or scheduling patch, this one *could* move rendering if the key extraction were ever wrong, so having the WPT run exercise it against the pinned pointer is the check worth having — and its claim is precisely that the numbers do not move. |

-->
