# Should `web-platform-tests/wpt` and `chromium/octane` be git submodules?

**Question.** We vendor the Broiler engine layers (`Broiler.HTML`, `.CSS`, `.DOM`,
`.JS`, `.Graphics`) as git submodules. Should we do the same for the two
*upstream* test corpora we consume — `web-platform-tests/wpt` and
`chromium/octane` — to "optimize test cases / CI runners / etc."?

**Short answer.** **No for WPT; marginal-yes-but-not-worth-it for Octane.**
The concrete goal behind the request — *deterministic, pinned, cheaply-fetched
test inputs* — is real and worth fixing, but a submodule is the wrong instrument
for it. Pin an upstream **commit SHA in the workflow** instead (see
[Recommendation](#recommendation)). This document lays out the pros and cons so
the decision is auditable.

---

## 1. How the two corpora are sourced today

| | WPT | Octane |
|---|---|---|
| Upstream | `github.com/web-platform-tests/wpt` | `github.com/chromium/octane` |
| Fetched | Shallow clone (`--depth 1 --branch master`) at CI/run time | Shallow clone (`--depth 1`) at CI/run time |
| Lands in | `tests/wpt/checkout/` (**gitignored**) | `tests/octane/checkout/` (**gitignored**) |
| Pinned to | `master` **HEAD at run time** — *not* pinned | `--octane-ref` input, default `master` HEAD — *not* pinned |
| CI reuse | `actions/cache` keyed on `CACHE_EPOCH`, shared by all 8 shards | none (re-cloned each run) |
| Committed in-tree | A **curated 408 KB corpus**: 216 CSS test files + 458 reference images (`git ls-files tests/wpt` → 678 files), the RF-LAYOUT-2 gate set — hand-picked, *not* the upstream tree | `tests/octane/results/*` only |
| Scripts | `scripts/run-wpt-tests.sh` (accepts `--wpt-dir` to reuse a checkout) | `scripts/run-octane-benchmarks.sh` (accepts `--octane-dir`) |

Two facts drive the whole analysis:

1. **These are third-party upstreams we never modify.** The existing submodule
   convention (`CLAUDE.md`) exists specifically to *edit a submodule, push to
   its `MaiRat/` remote, and bump the pointer* — with a patch-file fallback when
   the push is denied. That entire machinery is meaningless for WPT/Octane: we
   never commit into them. The one property a submodule would add over a plain
   clone is **pinning to an exact SHA**.

2. **The committed in-tree curated WPT corpus is separate and stays regardless.**
   The 678 tracked files under `tests/wpt/` are a curated gate set, committed
   directly (408 KB total). A submodule would mount the *full* upstream at a
   different path; it would neither replace nor shrink the curated set.

---

## 2. What "make them submodules" would actually change

Add entries to `.gitmodules` pointing at the upstreams, mount them at
(say) `tests/wpt/checkout/` and `tests/octane/checkout/`, and drop the runtime
`git clone` steps. CI would obtain them through `actions/checkout` instead.

The critical interaction: **all 7 workflows already check out with
`submodules: recursive`** — `broiler-preview-package`, `draft-release`,
`linux-port-build`, `nuget-packages`, `octane-benchmarks`, `wpt-tests`,
`writer-draft-release`. A recursive submodule checkout pulls **every** submodule,
so the giant WPT tree would be dragged into release packaging, NuGet publishing,
and the Linux port build — none of which touch WPT.

---

## 3. Pros

- **Deterministic, pinned inputs (the real win).** A submodule records an exact
  upstream SHA. Today both corpora float on `master` HEAD, so a green run and a
  red run can differ solely because upstream changed between them. Pinning makes
  WPT/Octane results reproducible and bisectable, and turns an upstream bump into
  a reviewable diff. *This is the one genuinely valuable property — and it does
  not require a submodule (see §6).*
- **Single provenance record.** The pinned SHA lives in the tree instead of being
  implied by "`master` on the day the cache was warmed," making the exact corpus
  auditable per commit.
- **One fetch mechanism.** Removes the bespoke clone-and-cache logic from
  `run-wpt-tests.sh` / `run-octane-benchmarks.sh`; `git submodule update` becomes
  the single path. (Modest: the scripts already accept `--wpt-dir`/`--octane-dir`.)
- **Offline/air-gapped friendliness.** `--recurse-submodules` in one place gives a
  fully-populated tree without per-script network calls.
- **Consistency of mental model.** "Everything external is a submodule" is one
  fewer special case to explain — *if* you ignore that the edit/push/patch
  workflow (the actual reason submodules exist here) never applies to these two.

## 4. Cons

- **WPT is enormous; recursive checkout blast radius is large.** The full
  `web-platform-tests/wpt` tree is hundreds of thousands of files and a working
  copy well over a gigabyte. Because all 7 workflows use `submodules: recursive`,
  every unrelated job (release, NuGet, Linux port) would start cloning it. Fixing
  that means auditing and pinning `submodules:` scope in each workflow — *more*
  CI complexity, not less.
- **Submodules shallow-fetch poorly.** The current WPT flow is deliberately
  `--depth 1` **plus** an 8-shard shared `actions/cache`. `actions/checkout`'s
  submodule support does not replicate that shard-shared, epoch-keyed cache; you
  would either lose the shared cache (re-fetch per shard/job) or rebuild the cache
  layer *around* the submodule — reimplementing what already exists.
- **Local-clone tax for everyone.** After `git clone --recurse-submodules`, every
  contributor — including those who only touch the C# engine — pays the full WPT
  download and disk cost. Today only people who actually run WPT pull it.
- **Pointer bumps become chores / rot silently.** A pinned SHA must be advanced
  deliberately. WPT lands hundreds of commits a week; the pointer will lag,
  someone must periodically bump-and-review, and CI clones strictly by pointer —
  exactly the "never bump a pointer you can't build against" hazard `CLAUDE.md`
  already warns about, now with an upstream we don't control.
- **The submodule *edit* workflow is dead weight here.** The `MaiRat/` push →
  pointer-bump → patch-fallback process is the entire justification for the
  existing submodules. For read-only upstreams it contributes nothing and invites
  confusion (e.g. someone trying to `format-patch` a WPT fix upstream we can't
  push to).
- **Two clashing sources of truth for WPT.** We would have both the curated
  in-tree corpus (`tests/wpt/**`, committed) *and* a full-tree submodule at
  `tests/wpt/checkout/`. Contributors must learn which drives which gate
  (RF-LAYOUT-2 curated gate vs. the sharded full-suite run).
- **`.git`/`.gitmodules` churn and submodule footguns.** Detached-HEAD states,
  `git submodule sync`, forgotten `--recurse` on pulls, and "modified
  (new commits)" noise in `git status` are recurring support costs — for a repo
  we only ever read.
- **Upstream availability coupling.** A submodule pointer to `chromium/octane` or
  `web-platform-tests/wpt` makes a recursive checkout fail hard if upstream is
  renamed, deleted, or rate-limits — with none of the retry/fallback the current
  scripts can carry.

---

## 5. Per-repo verdict (they are not the same case)

### WPT — **do not submodule.**
Size × the `submodules: recursive` blast radius × the shard-shared cache it would
break makes this clearly net-negative. The determinism benefit is real but
achievable far more cheaply (§6). The curated in-tree corpus already gives us a
*committed, reviewable* slice for the fast gate; the full suite is intentionally a
heavyweight, cache-optimized, dispatch-only job that a submodule would deoptimize.

### Octane — **submodule is defensible but still not worth it.**
Octane is tiny and rarely changes, so most WPT cons (size, cache, local tax)
nearly vanish. The remaining benefit over the status quo is purely *pinning*, and
the workflow already exposes `--octane-ref` for that. A submodule would still drag
Octane into all 7 recursive checkouts for a benchmark that only one workflow runs,
and still adds pointer-bump ceremony. Net: neutral-to-slightly-negative; not a
good use of a submodule slot.

---

## 6. Recommendation — pin without a submodule

Capture the one real benefit (deterministic inputs) without any submodule cost:

1. **Pin an upstream SHA in the workflow env**, not a floating branch:
   - `wpt-tests.yml`: replace `--branch master --depth 1` with a
     `WPT_COMMIT: <sha>` env var and `git fetch --depth 1 origin <sha>` (or
     `checkout <sha>`). Bumping the corpus becomes a one-line, reviewable PR that
     already invalidates the cache when paired with `CACHE_EPOCH`.
   - `octane-benchmarks.yml`: default `octane_ref` to a **SHA** instead of
     `master`.
2. **Keep the clone-and-cache path** in the scripts — it is doing real work
   (shard-shared, epoch-keyed WPT cache) that a submodule would force us to
   rebuild.
3. **Leave the curated in-tree WPT corpus as-is** — it is the right tool for the
   fast, committed, reviewable gate.
4. *(Optional)* Record the pinned SHAs in `tests/wpt-baseline/README.md` /
   `tests/octane/README.md` so provenance is documented next to the baselines.

This yields reproducibility and auditable bumps — the actual objective — with
zero change to checkout blast radius, local clone cost, or the submodule mental
model.

---

## 7. Summary table

| Criterion | Runtime clone (today) | Pinned SHA in workflow (recommended) | Git submodule |
|---|---|---|---|
| Deterministic / reproducible | ✗ (floats on `master`) | ✓ | ✓ |
| Auditable upstream bump | ✗ | ✓ (one-line PR) | ✓ (pointer PR) |
| Keeps shard-shared WPT cache | ✓ | ✓ | ✗ (must rebuild) |
| Blast radius on the other 6 workflows | none | none | **large** (recursive checkout) |
| Local clone cost for non-WPT devs | none | none | **full WPT tree** |
| Reuses existing edit/push/patch machinery | n/a | n/a | ✗ (never applies) |
| New ceremony introduced | none | trivial (bump a SHA) | pointer bumps + submodule hygiene |

**Bottom line:** the request's underlying goal is sound, but the answer is to
**pin upstream SHAs**, not to add submodules. Submodule the things you *edit and
own*; pin the things you only *read*.
