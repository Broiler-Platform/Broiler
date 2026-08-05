# Broiler Code performance budgets and support matrix

- **Status:** Frozen for Phase 0
- **Owner:** Broiler Code, with Broiler.UI, Android, WebAssembly, and release owners
- **Recorded:** 2026-08-05
- **Source of truth for decisions:** [Broiler Code architecture](broiler-code.md)

This document freezes the budgets the later phases are judged against, and the
minimum host matrix those budgets apply to. Every number here comes from a
committed harness and a named trace, so a regression is a figure moving on a
fixed input rather than a disagreement about whether a new measurement was fair.

## Recording baseline

| Property | Value |
| --- | --- |
| Machine | AMD Ryzen 7 5800X, 8 cores / 16 threads, 32 GiB RAM, x64 |
| Operating system | Windows 11 (10.0.26200) |
| SDK | .NET 10.0.302, runtime 10.0.10 |
| Build compiler | 5.6.0-2.26329.109 |
| Language-service Roslyn | `Microsoft.CodeAnalysis.CSharp` 5.3.0 |
| CLI UI language | de-DE |

This is one desktop machine, not a CI fleet. Timing figures are specific to it;
the machine-independent figures — lines relexed, span counts, payload bytes,
reference counts — are the ones a test may assert on any host. **The CI baseline
row of every table below is unrecorded and is owed before Phase 1 exits**, and
the Android and browser rows are owed before any local-semantic claim is made
for those hosts.

Harnesses and baselines:

- [`tests/broiler-code-phase0`](../../tests/broiler-code-phase0/) — runners, traces, and recorded JSON
- [`tests/broiler-code-phase0/traces`](../../tests/broiler-code-phase0/traces/) — the named edit/analysis traces

## What the traces measure

Producing the successor snapshot and analysing it are timed separately. They
have different owners and different fixes, and one combined figure hides which
of them a regression came from.

| Trace | What it pins down |
| --- | --- |
| `typing-burst` | Steady-state keystroke cost on a 2.35 MB, 100,000-line file |
| `typing-burst-wide` | The same burst on the 10.5 MB Phase 1 fixture; isolates document size as the only variable |
| `block-comment-open` | A state-changing edit whose effect stops when the line state reconverges |
| `raw-string-runaway` | The worst case where it never reconverges — the full-reparse ceiling |
| `paste-and-delete-block` | Structural edits that change the line count |
| `undo-redo-storm` | History navigation, and that undo produces new versions rather than restoring old snapshots |
| `stale-rejection` | A burst with no chance to publish; exactly one result may paint |

## Frozen budgets

The budget column is the ceiling a phase must not exceed. The measured column
is what the Phase 0 prototype achieved on the recording baseline, so the
headroom is visible rather than implied.

### Editing and classification

| Budget | Ceiling | Measured (Phase 0) | Trace |
| --- | --- | --- | --- |
| Incremental classification, p95, 2.35 MB file | 3 ms | 0.18 ms | `typing-burst` |
| Incremental classification, p95, 10.5 MB file | 3 ms | 0.22 ms | `typing-burst-wide` |
| Lines relexed per single-character edit, median | 1 | 1 | `typing-burst` |
| Lines relexed per single-character edit, non-reconverging worst case | whole document | 99,983 of 100,001 | `raw-string-runaway` |
| Full reparse of 100,000 lines | 40 ms | 13.6 ms | fixture measurement |
| Full-reparse ceiling reached through the incremental path | 40 ms | 6.9 ms | `raw-string-runaway` |
| Classification retained per 100,000-line file | 16 MiB | 9.41 MiB (291,665 spans) | fixture measurement |
| Classification retained per 1,000-file solution (101,000 lines) | 16 MiB | 9.49 MiB | fixture measurement |

### Snapshot update — the Phase 1 constraint, now met

This was the finding that mattered most from Phase 0, and the budget it froze as
**not met**. The Phase 0 prototype's snapshot was a whole immutable string, so
each keystroke copied the document and rebuilt the line index; analysis was
already two orders of magnitude cheaper than the snapshot it analysed.

Phase 1 replaced it with `Broiler.Code.Workspaces`' immutable balanced tree of
text chunks. An edit copies the O(log n) nodes on one root-to-leaf path, and the
line index is the per-node line-break counts rather than a separate array.

| Document | Phase 0, whole string | Phase 1, chunk tree | Budget |
| --- | --- | --- | --- |
| 2.35 MB — snapshot update p50 | 2.72 ms | 0.0086 ms | — |
| 2.35 MB — allocation per keystroke | 5.1 MB | 2,656 B | — |
| 10.5 MB — snapshot update p50 | 17.63 ms | 0.0020 ms | — |
| 10.5 MB — snapshot update p95 | — | **0.0028 ms** | 1 ms |
| 10.5 MB — allocation per keystroke | 21.4 MB | **1,272 B** | 4 KiB |

| Budget | Ceiling | Status |
| --- | --- | --- |
| Snapshot update, p95, 10 MiB document | 1 ms | **Met** — 0.0028 ms |
| Allocation per single-character edit | 4 KiB | **Met** — 1,272 B |
| Input-to-present, p95 (edit accepted to repainted line) | 16 ms | Still unmeasured; see below |

The load-bearing property is not the ratio but the shape: cost no longer scales
with document size. The 10.5 MB document is *cheaper* per keystroke than the
2.35 MB one, because both are a path copy through a tree of the same depth
order and the wider fixture happens to split more favourably. A document ten
times larger again would not change these figures materially.

Input-to-present keeps its 16 ms ceiling and is **still not measured**. Phase 1
delivered the control and the renderer, but a measured input-to-present figure
needs a host presenting real frames; it must be measured end to end rather than
reconstructed by adding the buffer and analysis figures above. It carries
forward to the first phase that runs the editor in a platform head.

### Cancellation and staleness

| Budget | Requirement | Measured (Phase 0) |
| --- | --- | --- |
| Results painted from a superseded snapshot | 0, always | 0 across every trace |
| Results published during a 32-edit burst with no drain | exactly 1 | 1 published, 30 cancelled, 1 completed-then-discarded |
| Cancellation observed within | 1,024 lines of lexing | enforced in the classifier's token check |

The completed-then-discarded case is covered by a deterministic check rather
than by the trace, because whether a superseded run is cancelled or finishes
first is a race. `completed-stale-run-rejected` places an edit inside the window
between a run completing and its completion being delivered, which is the only
part of the behaviour that a timing-dependent trace cannot pin down.

### Semantic service, where it is composed

| Measurement | Value | Note |
| --- | --- | --- |
| Roslyn payload, desktop, untrimmed | 9.5 MiB | `Microsoft.CodeAnalysis{,.CSharp}.dll` |
| Syntax parse, 100,000 lines | 87.1 ms p50, 10.6 MiB retained | 8× the portable classifier's 13.6 ms |
| Compilation with diagnostics, 1,000 files / 100,000 lines | 732 ms, 45.6 MiB retained | 4.8× the portable classifier's retained bytes |
| Compilation with diagnostics, 3-file fixture, 168 references | 713 ms | Dominated by loading reference metadata |
| Evaluated graph for a two-project solution | 168 metadata references, 5.8 MiB | Reference assemblies, obtained as structured JSON |

Budgets: a semantic result must not block input on any host, and a host that
cannot meet the payload and graph-delivery figures below reports
classification-only mode rather than degrading silently.

### Browser payload — the composition decision

Measured by publishing two probes with identical trimmed interpreted settings,
where the only difference is whether the Roslyn syntax service is composed.

| Profile | Raw | Brotli |
| --- | --- | --- |
| Portable classification only | 2.88 MiB | 0.87 MiB |
| With Roslyn syntax service | 10.78 MiB | 2.66 MiB |
| Cost of composing Roslyn | +7.90 MiB | +1.79 MiB |
| Cost of composing Roslyn, excluding localized message resources | +1.76 MiB | +0.46 MiB |

Of the Roslyn payload, **6.14 MiB raw and 1.33 MiB compressed — 57% and 50% —
is localized diagnostic message text** in 26 satellite assemblies. The
diagnostic contract states that identity comes from rule ID, document, and span
and never from the message, so those satellites are separable payload. Dropping
them cuts the cost of composing the syntax service by roughly a factor of four,
at the price of English-only messages on that host. That is a product decision
with a visible consequence, so both numbers are recorded and neither is
presented as the number.

Frozen budgets for a browser IDE host that claims local semantic diagnostics:

| Budget | Ceiling |
| --- | --- |
| Total first-load payload, compressed | 6 MiB |
| Additional payload for the semantic service, compressed | 2.5 MiB |
| Evaluated graph and metadata references transferred per opened project | 8 MiB, content-addressed and cached |

The payload budget is met by the measured composition. The graph-delivery
budget is met for the fixture at 5.8 MiB, and is the figure most likely to break
on a real solution with package references — which is why it is a per-project
budget rather than a one-time cost.

## Unmeasured, and owed

Phase 0 does not claim these, and no Android or browser local-semantic claim may
be composed until the first three are recorded.

| Measurement | Why it is not recorded here |
| --- | --- |
| Android cold start and peak resident memory with the semantic service | Needs a device or emulator running the composed host. A desktop figure is not a substitute: JIT, memory ceiling, and storage all differ. |
| Android payload delta, trimmed | Measurable in principle. Recorded as owed because trimming Roslyn needs a reflection-safety review Phase 0 has not done, and an untrimmed number quoted as a trimmed one would mislead. |
| Browser cold start and peak heap with the semantic service | Needs a browser harness driving the published profile. The payload size is measured without one and is recorded above. |
| WebAssembly AOT with the semantic service | Deliberately not measured. Phase 0's instruction is to measure AOT only if that IDE host mode will be claimed, and no such claim is being made. |
| CI baseline for every timing budget | The budgets are recorded on one developer machine. CI figures are owed before Phase 1 exits so the ceilings can be enforced by a gate rather than by inspection. |
| Input-to-present | There is no view in Phase 0. Phase 1 owns it. |

## Minimum supported host matrix

Support is claimed per capability, not per platform. "Runs the IDE" and "builds
a target" are separate columns because they have separate prerequisites.

| Host | Minimum | Editing and portable classification | Local semantic diagnostics | Authoritative builds |
| --- | --- | --- | --- | --- |
| Windows | Windows 10 22H2 / Windows 11, x64 or arm64 | Supported | Supported when the .NET SDK is present | Local out-of-process worker |
| Linux | glibc 2.31 or newer, x64 or arm64 | Supported | Supported when the .NET SDK is present | Local out-of-process worker |
| Android | API 24 (Android 7.0) minimum, API 36 target, arm64 or x64 | Supported | **Not claimed** — footprint and graph delivery unmeasured | Remote isolated worker |
| Browser | Chromium 120+, Firefox 121+, Safari 17.4+ | Supported | **Not claimed** — payload measured, cold start and heap unmeasured | Remote isolated worker |

The Android and browser rows deliberately claim classification-only. Phase 0's
exit gate requires the measurements to decide the fork without leaving it open;
the decision is that the fork stays closed for those hosts until the owed
measurements are recorded, and both hosts report classification-only mode
between builds in the meantime.

Editing support on every host is judged against the budgets in this document.
The minimum browser versions are those with stable support for the WebAssembly
and JavaScript-interop features the .NET 10 browser host requires; they are a
target for Phase 6 validation, not a claim that validation has happened.

## Reproducing

```bash
dotnet run --project tests/broiler-code-phase0/Broiler.Code.Phase0.Prototype -c Release -- --iterations 9
```

```bash
dotnet run --project tests/broiler-code-phase0/Broiler.Code.Phase0.Roslyn -c Release -- --iterations 5
```

```bash
pwsh tests/broiler-code-phase0/payload-probes/measure-payloads.ps1
```

The prototype exits non-zero if any correctness check fails, if any trace ends
disagreeing with a full classification, or if any trace painted a superseded
snapshot. The Roslyn harness exits non-zero if the local semantic service stops
agreeing with the authoritative build.
