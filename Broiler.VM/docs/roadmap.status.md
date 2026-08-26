# Broiler.VM roadmap status

**Last updated:** 2026-08-26

**Authority:** This file is the authoritative current-evidence ledger for the milestones in the
[Broiler.VM roadmap](roadmap.md). The roadmap defines planned work and objective exit gates; this
ledger records whether those gates have accepted evidence.

No Broiler.VM milestone is complete merely because its design appears in the roadmap. At this
snapshot, **VM-0 through VM-8 are not started**. The repository contains the Broiler.VM
[component overview](../README.md) and roadmap documents, but no Broiler.VM implementation,
project or package definitions, tests, samples, build-proven dependency graph, profile catalog,
JavaScript profile, or WebAssembly profile.

---

## 1. Reading this ledger

The following categories must remain distinct:

- **Plan** is proposed scope, sequencing, ownership, or an exit gate in `roadmap.md`. It is not
  implementation or validation evidence.
- **Observed repository state** is a reviewable fact about the current checkout, such as the
  absence of Broiler.VM projects. It can explain a status but cannot by itself satisfy a future
  implementation, conformance, performance, Native AOT, or release gate.
- **Accepted evidence** is an immutable, reviewable evidence bundle that identifies the exact
  sources and gate, records the executed commands and environment, retains their outputs, and
  demonstrates every part of the objective exit gate. Only accepted evidence may advance a
  milestone to `Accepted`.

Broiler.JS implementation and historical measurements remain inputs or seed evidence only. They
do not establish that a Broiler.VM JavaScript profile exists or that any Broiler.VM gate has
passed. In particular, the current [Broiler.JS Phase 6 status](../../Broiler.JS/docs/roadmap/Phase-6.status.md)
does not supply a completed production VM milestone.

### Status vocabulary

| State | Meaning |
|---|---|
| `Not started` | No milestone-owned implementation or accepted gate evidence has been recorded. Planning text does not change this state. |
| `In progress` | Milestone-owned work or evidence collection has begun, but the objective exit gate has not been accepted. The ledger must link its working evidence and list every open gate condition. |
| `Blocked` | Work has a named external dependency that prevents the next action. The blocker, owner, and unblock condition must be recorded; lack of scheduling is not a blocker. |
| `Accepted` | Every objective exit condition has an immutable evidence bundle and owner/reviewer decision recorded here. Partial success cannot use this state. |
| `Superseded` | A dated decision replaced the milestone or gate. The replacement and decision record must be linked; evidence history is retained. |

---

## 2. Current milestone status

| Milestone | State | Current evidence | Immediate evidence-producing action |
|---|---|---|---|
| **VM-0 — ownership, terminology, neutral contract, and graph** | **Not started** | Roadmap hypotheses exist, but there is no approved boundary ADR, assigned core contract version, minimum lifecycle contract, resource-policy precedence, profile-ID policy, WebAssembly support manifest, project shell, architecture test, or build-proven graph. | Freeze the semantics-neutral graph and minimum contract independently of a final JavaScript compiler-composition choice. Record the lifecycle state machine, immutable verified-artifact ownership, trusted resource-limit precedence, static composition roots, initial Wasm/WASI and test-harness scope, dependency rules, and provisional JavaScript requirements; assign core contract version 1 and publish its amendment procedure; record an explicit decision on guest-initiated loads, asynchronous instantiation, external suspension, and aggregate budgets rather than leaving them silent; then prove the shell graph and forbidden edges. |
| **VM-1 — semantics-neutral runtime and static catalog** | **Not started** | No Broiler.VM contracts, runtime, catalog, composition root, fixture profile, or Native AOT construction host exists. | After VM-0 acceptance, implement and test the neutral contracts/catalog with a fixture profile, including whichever of guest-initiated-load mediation, artifact-provider registration, external suspension, and aggregate budget metering VM-0 assigned to the core, and their refusal paths. Keep concrete JavaScript and WebAssembly factories in their aggregate or single-profile composition roots rather than the generic runtime. |
| **VM-2 — bounded artifacts, verification, and resources** | **Not started** | No common descriptor, opaque verified-artifact handle, bounded loader, trusted-limit intersection, verifier result contract, malformed corpus, or fuzz target exists. | After VM-1 acceptance, prove the common boundary with immutable copied or decoded fixture artifacts, caller-mutation tests, bounded failures, explicit default/omission cases, host/profile/artifact intersection, and invocation-only tightening. Profile payload formats and verifiers close in VM-3 and VM-4 rather than serving as VM-2 prerequisites. |
| **VM-3 — JavaScript built-in** | **Not started** | Broiler.JS has numeric portable seed work, but no accepted production JavaScript profile ABI, canonical general format/verifier, profile executor, VM conformance result, or Broiler.VM Native AOT profile closure exists. | After VM-0 through VM-2 and terminal Broiler.JS MOD-M9, deliver JavaScript format, verifier, lowering, interpreter, conformance, and composition-specific Native AOT evidence through the static VM contract. |
| **VM-4 — WebAssembly built-in** | **Not started** | No Broiler.VM Wasm decoder, validator, module lifecycle, interpreter, import boundary, conformance harness, or Native AOT closure exists. | After VM-0 through VM-2, implement against VM-0's single pinned support manifest, including its Core/proposal/product-and-test import/linking/WASI scope and test-only WAST-ingestion identity; retain profile-specific malformed, conformance, extern-lifetime, lifecycle, test-tool-closure, and AOT evidence. |
| **VM-5 — composition and future built-in proof** | **Not started** | The static catalog is documented only. No application-local consumer profile, catalog drift test, accepted public source-composition contract, or exact JS-only/Wasm-only/aggregate closure report exists. | Open VM-5-COMMON, VM-5-JS, VM-5-WASM, and VM-5-AGG evidence independently. Prove the public application-profile contract without discovery or core changes, then prove each exact static closure. |
| **VM-6 — lifecycle, concurrency, diagnostics, and hosts** | **Not started** | No Broiler.VM lifecycle, reentrancy, cancellation, isolation, host-failure, diagnostics, disposal, or memory-plateau result exists. | Stress the VM-0/VM-1 lifecycle separately as VM-6-JS and VM-6-WASM when their slices exist; add VM-6-AGG only when both can run together. Retain host-boundary, reclamation, diagnostics, and isolation evidence for each child. |
| **VM-7 — measurements and gated optimization** | **Not started** | No accepted uninstrumented Broiler.VM baseline or optimization experiment exists for either profile. Existing Broiler.JS IL-engine measurements are not Broiler.VM profile evidence. | Once either profile closes its correctness gate, open its VM-7-*-BASELINE record independently. Mutable/shared experiments additionally wait for the matching VM-6 child and receive their own profile-qualified record. |
| **VM-8 — package, release, and recertification** | **Not started** | No Broiler.VM package, API baseline, pristine feed consumer, support table, release bundle, rollback result, or recertification record exists. | Open VM-8-JS and VM-8-WASM when their own dependencies close; either may release while the parent remains in progress. VM-8-AGG additionally requires both correctness gates and aggregate VM-5/VM-6 evidence. |

The immediate program action is therefore **VM-0: establish and prove the semantics-neutral
graph and minimum contract**. The preliminary MOD-M9-1 requirements packet is an input; the
terminal JavaScript MOD-M9/6-0 decision consumes that neutral boundary and completes the JavaScript
deployment/compiler-composition choice before VM-3. The terminal decision is not a prerequisite
for starting VM-0 or VM-1.

### Profile/composition child records

The parent rows above are rollups, not a reason to couple profile delivery. These child records are
the release-addressable units and all are currently `Not started`:

| Child record | State | Gate represented | Evidence / decision |
|---|---|---|---|
| **VM-5-COMMON** | **Not started** | public source-level profile contract, ID governance, application-local fixture, and trimmed/Native AOT consumer | None; no child-owned implementation or evidence bundle exists. |
| **VM-5-JS** | **Not started** | exact JavaScript-only static composition closure | None; no child-owned implementation or evidence bundle exists. |
| **VM-5-WASM** | **Not started** | exact WebAssembly-only static composition closure | None; no child-owned implementation or evidence bundle exists. |
| **VM-5-AGG** | **Not started** | exact aggregate static composition closure | None; no child-owned implementation or evidence bundle exists. |
| **VM-6-JS** | **Not started** | JavaScript lifecycle, host, diagnostics, isolation, cancellation, disposal, and plateau evidence | None; no child-owned implementation or evidence bundle exists. |
| **VM-6-WASM** | **Not started** | WebAssembly lifecycle, imported-extern lifetime, host, diagnostics, isolation, cancellation, disposal, and plateau evidence | None; no child-owned implementation or evidence bundle exists. |
| **VM-6-AGG** | **Not started** | mixed-profile lifecycle and isolation evidence after VM-6-JS and VM-6-WASM | None; no child-owned implementation or evidence bundle exists. |
| **VM-7-JS-BASELINE** | **Not started** | decision-grade immutable/single-runtime JavaScript baseline | None; no child-owned measurement or evidence bundle exists. |
| **VM-7-WASM-BASELINE** | **Not started** | decision-grade immutable/single-runtime WebAssembly baseline | None; no child-owned measurement or evidence bundle exists. |
| **VM-8-JS** | **Not started** | JavaScript-only package/release/recertification record | None; no child-owned implementation or evidence bundle exists. |
| **VM-8-WASM** | **Not started** | WebAssembly-only package/release/recertification record | None; no child-owned implementation or evidence bundle exists. |
| **VM-8-AGG** | **Not started** | aggregate package/release/recertification record after both correctness gates and mixed-composition evidence | None; no child-owned implementation or evidence bundle exists. |

Create each funded VM-7 experiment as `VM-7-JS-<experiment>` or
`VM-7-WASM-<experiment>`; create an aggregate experiment only for a separately measured aggregate
population. VM-5 and VM-6 parent rows become `Accepted` only when all their listed children are
accepted. VM-7 becomes `Accepted` when both baseline children and every funded experiment have a
terminal accepted/deferred/removed/owned-expiring disposition. VM-8 becomes `Accepted` only after
all three release children, but VM-8-JS or VM-8-WASM may ship while the parent is `In progress`.
Each parent remains `Not started` only while all of its children are `Not started`; once any child
starts or is accepted, an incomplete parent is `In progress` unless its own next action meets the
`Blocked` definition. The evidence/decision cell holds working links and open conditions while in
progress, or the immutable bundle ID, reviewer, and decision date when accepted.

---

## 3. Required evidence bundle

Every status claim beyond `Not started` must point to a retained bundle with all applicable
fields below. A command written in a plan is not evidence that the command ran.

| Field | Required record |
|---|---|
| **Identity** | Milestone and item IDs, roadmap/gate revision, core contract version, evidence-bundle ID, collection timestamp, owner, and reviewer. |
| **Source** | Aggregate repository commit, Broiler.JS submodule commit where applicable, dirty-tree state and patch identity, and exact paths/projects under test. |
| **Dependencies and corpus** | Lockfile/package identities, toolchain/SDK versions, WebAssembly specification and test-suite revisions, JavaScript manifests, fixture hashes, and applicable provenance/license decisions. |
| **Environment** | OS, architecture, RID, hardware/lane identity, runtime mode, configuration, JIT/trimming/Native AOT mode, effective environment variables, and resource limits. Secrets must be redacted without hiding semantically relevant configuration. |
| **Procedure** | Exact commands, working directories, ordered setup, inputs, repetitions/seeds, timeouts, and clean/pristine-consumer conditions. |
| **Outputs** | Durable logs, machine-readable results, binaries/packages or hashes, analyzer and trim/AOT warnings, crash dumps or minimized fuzz cases where applicable, and storage locations with retention policy. |
| **Decision** | Expected gate, actual result, unexplained failures, exclusions, deviations, support claim justified by the result, reviewer verdict, and follow-up owner. |
| **Validity** | Reproduction instructions, expiry or review date where evidence can age, and recertification triggers such as source, dependency, SDK, RID, manifest, format, API, or composition changes. |

Performance evidence must additionally follow the repository's decision-grade measurement rules,
retain every repetition and resource metric, identify candidate and control exactly, and report
negative or inconclusive results. Conformance evidence must retain the expected-result manifest,
all exclusions, and the complete pass/fail/skip/timeout accounting. Security/fuzz evidence must
retain corpus identity, budgets, duration or iteration count, sanitizer/runtime settings,
failures, and minimized regressions. Native AOT evidence must come from publishing and running the
declared composition on every claimed RID; analyzer success alone is not a publish-and-run result.

---

## 4. Update rules

1. Update this ledger in the same change that accepts, rejects, blocks, supersedes, or materially
   narrows a milestone claim. Preserve earlier evidence links and decisions as dated history.
2. Do not copy a planned exit gate into the evidence column. Link the immutable bundle and state
   what it demonstrated, including failures and exclusions.
3. Do not infer completion transitively. VM-0 acceptance does not accept VM-1; a JavaScript result
   does not accept WebAssembly; a single-profile result does not accept the aggregate; and JIT,
   trimmed, or one-RID success does not accept an untested Native AOT/RID claim.
4. Do not promote seed, shell, smoke, analyzer-only, or shape-only results beyond what they prove.
   A failing or partial bundle is retained but leaves the milestone `In progress` unless a named
   dependency meets the `Blocked` definition.
5. If a gate changes, record the gate revision and re-evaluate existing evidence. Evidence gathered
   for an older or broader/different population is not silently carried forward. A core contract
   amendment is such a change: record the new version and state, per affected record, what
   recertifies unchanged, what must be re-collected, and what is superseded.
6. Keep profile-specific status independent. VM-3 and VM-4 may progress separately. Update the
   named VM-5/VM-6/VM-7/VM-8 child record rather than promoting a parent because one profile passed;
   do not require an aggregate child for a single-profile release.
7. A child or milestone moves to `Accepted` only after its owner and reviewer confirm that every
   objective exit condition for that record is covered. Parent VM-5/VM-6/VM-7/VM-8 states follow
   the rollup rules above. Record the decision date and evidence-bundle ID in the affected row.

Until such updates are recorded, the tables in §2 remain the complete Broiler.VM status: all nine
milestones and all listed child records are not started, and no implementation or release
capability is claimed.
