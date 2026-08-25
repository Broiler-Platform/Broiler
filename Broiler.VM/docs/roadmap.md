# Broiler.VM roadmap

**Status:** Proposed component roadmap. No Broiler.VM milestone is complete merely because
this document exists.

Broiler.VM is the statically composed, NativeAOT-compatible bytecode execution component.
It executes validated artifacts through built-in bytecode-language profiles. **JavaScript**
and **WebAssembly** are the required initial built-ins. More built-in profiles may be added
as source/build-time contributions, but runtime assembly scanning, name-based activation,
downloadable plug-ins, and extension-directory loading are outside the supported model.

The JavaScript portions of this plan reuse the existing Broiler.JS bytecode roadmap. They do
not restart its capability decision, fork JavaScript semantics, or turn the numeric portable
seed into evidence for a general engine. The WebAssembly profile is an independent semantic
implementation with its own format, verifier, conformance manifest, and measurements.

---

## 1. Terminology and support claims

The repository already uses the word *profile* for several different boundaries. Documents,
APIs, test names, and release manifests must qualify the term when ambiguity is possible.

| Term | Meaning in this roadmap |
|---|---|
| **VM profile** | A bytecode language plus its format, feature/version manifest, verifier, value/frame model, and executor. `JavaScript` and `WebAssembly` are VM profiles. |
| **JavaScript bootstrap profile** | A Broiler.JS realm/global-feature selection such as `JavaScriptBootstrapProfile.Full`; it is not a VM profile. |
| **Deployment/compiler composition** | Which tools are present at run time: `execution-only`, `narrow-runtime-compiler`, or `general-runtime-compiler`. These are not separate VM profile identities. |
| **Feature manifest** | The exact language/proposal/host surface accepted by one version of a VM profile. A profile name alone is not a conformance claim. |
| **Built-in profile** | A profile whose factory and dependencies are directly referenced at build time and rooted in a static catalog. Built-in does not mean that every product must carry every profile package. |

The aggregate Broiler.VM product contains both required built-ins. A size-sensitive product may
use a statically defined JavaScript-only or WebAssembly-only composition, but it may not claim
the aggregate surface. An unknown profile, unsupported feature manifest, or incompatible format
version is a deterministic load failure, never a best-effort fallback to another profile.

### Scope

Broiler.VM owns:

- explicit profile selection and an immutable built-in catalog;
- bounded artifact loading and profile/version matching;
- execution lifecycle, cancellation or fuel, resource budgets, diagnostics, and result/trap
  transport;
- typed host-capability registration and per-runtime ownership; and
- composition, trimming, Native AOT, and package evidence for the component boundary.

Each VM profile owns:

- its bytecode payload format and feature manifest;
- decoding, validation, and profile-specific resource checks;
- its value, frame, call, control-flow, trap/exception, and suspension model;
- imports, exports, and conversions at its host boundary;
- conformance fixtures and profile-specific optimizations; and
- any compatibility promise for persisted artifacts.

Source compilers do not belong in the execution core. Broiler.JS owns JavaScript parsing,
backend-neutral semantic analysis, and JavaScript-bytecode lowering. The WebAssembly profile
accepts binary modules; WAT parsing and source-language compilation are not initial-profile
requirements.

### Non-goals for the first release

- one universal JavaScript/WebAssembly opcode set, tagged value, or frame ABI;
- reflection-based or unloadable runtime plug-ins;
- automatic artifact/profile detection by trying multiple decoders;
- an implied JavaScript-to-WebAssembly invocation bridge;
- every WebAssembly proposal, the component model, WAT, or a Wasm source compiler;
- JavaScript IL tier-up, deoptimization, or OSR in the VM core; and
- performance claims before correct uninstrumented baselines exist for each profile.

---

## 2. Engineering invariants

1. **A profile is selected explicitly.** The caller supplies a stable profile identity, or a
   checked Broiler.VM envelope supplies it and the caller confirms it. Raw WebAssembly remains
   usable under explicit `WebAssembly` selection. The runtime never guesses by probing every
   registered decoder.
2. **Registration is static and typed.** The default catalog directly references the JavaScript
   and WebAssembly factories. There is no `Assembly.Load`, `Type.GetType`, assembly scan,
   `Activator.CreateInstance`, magic type name, or module-initializer ordering dependency.
3. **Verification precedes execution.** Every external artifact is parsed with checked lengths
   and budgets and passes its selected profile verifier before an instruction executes.
4. **The core is semantics-neutral.** It provides lifecycle and safety contracts, not a lowest-
   common-denominator ISA. JavaScript and WebAssembly may share a primitive only after dependency,
   correctness, and representation evidence proves that sharing useful.
5. **Mutable state has an owner.** Frames, feedback, inline caches, quickening, host handles, and
   compiled artifacts belong to a runtime, realm/module, program, or function. Canonical bytecode
   and persisted artifacts contain no warmed state or process-local identities.
6. **Compiler closures are explicit.** A JavaScript execution-only application contains no
   parser or source compiler. A runtime-compiler application is a separate composition and must
   publish and execute that larger closure under Native AOT before it is supported.
7. **Native AOT is demonstrated, not inferred.** Analyzer success, a trimmed build, and the
   existing numeric seed are inputs. Each claimed deployment composition must publish and run its
   representative workload on every declared RID with trim/AOT warnings treated as errors.
8. **Unsupported surface is truthful.** A missing JavaScript construct, WebAssembly proposal,
   import type, host capability, or deployment mode has a documented deterministic failure. A
   shape-only stub cannot satisfy a capability gate.

---

## 3. Static profile registration

The exact public names are deferred to VM-0, but the required shape is a builder/catalog whose
entries contain immutable descriptors and direct factory delegates:

```csharp
var vm = VmRuntime.CreateBuilder()
    .AddBuiltIn(BuiltInVmProfiles.JavaScript)
    .AddBuiltIn(BuiltInVmProfiles.WebAssembly)
    .Build();
```

This example expresses dependency rooting, not a frozen API. A hand-maintained catalog is the
initial preference because two entries do not justify generator complexity. A source generator
may replace the table later if it emits direct calls, produces a reviewable manifest, and has a
test proving that generated and documented catalogs agree. Runtime reflection is not an allowed
substitute.

Every catalog entry must provide:

- a stable, non-localized profile ID and display name;
- a supported profile-format range and feature-manifest IDs;
- an AOT-rooted verifier and per-runtime executor factory;
- profile-specific limit defaults and host-capability descriptors;
- a conformance manifest/version and diagnostics identity; and
- package and ownership metadata used by architecture and release checks.

Registration rejects duplicate IDs, alias collisions, missing factories, unsupported versions,
and descriptors whose declared identity differs from the produced executor. Catalog order has no
semantic effect. A future built-in is added by referencing its assembly and adding its descriptor
factory to an explicit composition root; it does not require a switch inside the execution loop.

---

## 4. Package-boundary hypotheses

These names are hypotheses, not authorization to create assemblies. VM-0 must prove the graph
with project shells and the same assembly/package-budget discipline used by the
[Broiler.JS assembly roadmap](../../Broiler.JS/docs/roadmap/Assemblies.md).

| Logical boundary | Candidate package | Responsibility and dependency rule |
|---|---|---|
| Contracts | `Broiler.VM.Abstractions` | Profile IDs/descriptors, execution options/results, budgets, diagnostics, and typed host contracts; references no concrete profile. |
| Core runtime | `Broiler.VM.Runtime` | Builder, immutable catalog, bounded load/execute lifecycle, cancellation, and ownership; references abstractions, not Broiler.JS or a Wasm implementation. |
| JavaScript built-in | `Broiler.VM.JavaScript` | JavaScript payload reader/verifier/interpreter and adapter to the approved AOT-clean Broiler.JS runtime contracts; never references the IL emitter or optional CLR host. |
| WebAssembly built-in | `Broiler.VM.WebAssembly` | Wasm decoder/validator/interpreter, typed stack, module instance, memories/tables/globals, imports/exports, and traps; does not reference Broiler.JS. |
| Aggregate composition | `Broiler.VM` | Statically registers both required built-ins and supplies the default package/sample surface; contains no profile semantics of its own. |
| JavaScript compiler | Name selected by Broiler.JS MOD-M2/MOD-M9 | Parser, shared semantic IR, and JavaScript-bytecode lowering; outside the execution-only closure and independent of the IL emitter. |

Single-profile composition roots may be provided when package and image evidence justifies them.
They remain explicit packages/samples rather than a runtime option that dynamically removes an
already rooted profile. No new assembly is accepted merely to shorten a file: it must enforce a
dependency, AOT, deployment, ownership, test, or package boundary.

The target direction is:

```text
Broiler.JS FrontEnd/Semantics ─→ JavaScript bytecode compiler ─→ JS artifact
              │                                                   │
              └──────────────→ existing IL backend                ▼

Broiler.VM.Abstractions ← Broiler.VM.Runtime ← aggregate/static composition
          ↑                       ↑                   │
          ├── Broiler.VM.JavaScript ─────────────────┤
          └── Broiler.VM.WebAssembly ────────────────┘
```

The verified project graph may adjust names and split points, but it must retain these rules:
the core knows no concrete profile; the two profiles do not depend on one another; JavaScript
bytecode compilation does not reach IL; and only the aggregate or an explicit host composition
knows which built-ins it includes.

---

## 5. Artifact and versioning model

### Explicit descriptor plus profile-owned payload

The execution API receives an immutable artifact descriptor and bytes. The descriptor identifies
the VM profile, profile-format version, feature-manifest ID, and applicable resource policy. The
selected profile owns decoding of the payload:

- JavaScript uses the canonical, versioned format and verifier developed by Broiler.JS Phase 6.
- WebAssembly accepts a standard binary module under explicit `WebAssembly` selection and checks
  the Wasm binary version plus the selected proposal/feature manifest.

Raw payload support avoids wrapping interoperable `.wasm` modules merely to execute them. It does
not permit sniffing: a caller that labels JavaScript bytes as WebAssembly receives a deterministic
WebAssembly validation failure.

### Optional persisted envelope

If persistence is approved by the cold-start gate, a Broiler.VM cache/envelope records at least:

- envelope magic and schema version;
- stable profile ID, profile-format version, and feature-manifest ID;
- engine semantic/cache version and compiler identity when applicable;
- payload and section lengths with configured upper bounds;
- canonical source/module identity and host-capability/cache-key inputs;
- integrity/checksum data and atomic replacement state; and
- optional source/debug metadata whose positions are validated by the profile.

It never persists object references, delegates, intern-table indexes, process-local shape IDs,
warmed inline caches, quickened authoritative opcodes, host handles, or other mutable execution
state. Loading always re-verifies the envelope and profile payload. Runtime-compiler JavaScript
may recompile known source after an invalid cache; execution-only JavaScript and WebAssembly
report a defined load failure and accept a separately supplied fresh verified artifact.

Compatibility is opt-in. Internal formats may evolve before a version is declared persisted.
Supporting an old version requires a tested reader/migration or a documented rejection; silently
interpreting old bytes under new semantics is prohibited.

---

## 6. Built-in profiles

### JavaScript

The JavaScript VM profile is the Broiler.VM home for the bytecode interpreter planned in
[Broiler.JS Phase 6](../../Broiler.JS/docs/roadmap/Phase-6.md). Broiler.JS continues to own
parsing, early errors, binding, scope, hoisting, private names, direct-eval rules, free-name
analysis, backend-neutral lowering, JavaScript runtime operations, and the independent expected-
result manifest. Broiler.VM supplies the static profile host and bounded execution lifecycle.

The VM profile identity remains `JavaScript` across deployment compositions:

| Composition | Runtime contents | Required evidence |
|---|---|---|
| `execution-only` | JavaScript profile runtime, verifier, and precompiled bytecode; no parser/compiler | approved artifacts execute under Native AOT on every claimed RID |
| `narrow-runtime-compiler` | plus parser, shared semantic front end, and lowering for a named subset | approved source compiles inside the published AOT process and executes; exclusions are deterministic |
| `general-runtime-compiler` | plus the approved general source/compiler surface | independent expected/IL/VM conformance and the complete declared AOT closure |

The existing `Broiler.JavaScript.Portable` implementation is seed evidence only. Its numeric,
`double`-oriented bytecode must not freeze the general JavaScript ISA or value ABI, and migrating
or retaining it is decided by the graph/compatibility ADR rather than assumed.

Phase ownership remains:

- [Phase 6](../../Broiler.JS/docs/roadmap/Phase-6.md): shared semantic IR, JavaScript value/frame
  ABI, format/verifier, vertical interpreter slices, hard semantics, and the expected/IL/VM gate;
- [Phase 7](../../Broiler.JS/docs/roadmap/Phase-7.md): the uninstrumented JavaScript VM baseline
  and measured shippability work;
- [Phase 8](../../Broiler.JS/docs/roadmap/Phase-8.md): independently gated JavaScript feedback,
  quickening, dispatch, Native AOT PGO, and persistence; and
- [Phase 9](../../Broiler.JS/docs/roadmap/Phase-9.md): optional JavaScript bytecode-to-IL
  promotion, deoptimization, and OSR for dynamic-code-capable hosts. Phase 9 is not a generic
  Broiler.VM requirement and does not apply to WebAssembly by analogy.

### WebAssembly

The WebAssembly built-in owns a separate typed execution model. VM-0 pins the initial supported
standard edition and proposal set before implementation. A reasonable first vertical plan covers
binary decoding and validation, numeric types, structured control flow, functions and calls,
globals, linear memory, tables, module instantiation, imports/exports, and traps. SIMD, threads,
multiple memories, reference types beyond the selected baseline, exception handling, GC, tail
calls, memory64, the component model, and later proposals are supported only when named in a
versioned feature manifest and backed by their own tests.

The Wasm verifier checks types and stack states, structured branches, section order and indexes,
function/code agreement, memory/table/global limits, constant expressions, declared features,
and configured aggregate resources before instantiation. Runtime limits cover at least call depth,
instruction fuel or cancellation checks, memory pages/growth, table entries, module/function/
local counts, element/data segments, and host calls.

WebAssembly traps are profile results, not CLR process failures. Imports are resolved through a
typed, allowlisted host-capability registry; arbitrary reflection over host objects is not an
interop mechanism. Floating-point, conversion, memory, and trap edge cases follow the pinned
WebAssembly conformance oracle rather than JavaScript behavior.

### Future built-ins

A future profile is acceptable only when it has a named product requirement and owner. Its entry
proposal must include format provenance, feature/version policy, verifier design, resource model,
host boundary, conformance oracle, AOT RID matrix, package cost, maintenance budget, and explicit
reason it belongs in Broiler.VM rather than a separate component.

Adding one follows this sequence:

1. reserve a stable profile ID and approve the dependency/security ADR;
2. add the profile assembly and directly referenced descriptor/factory;
3. add malformed-input, conformance, lifecycle, and architecture tests;
4. add single-profile and aggregate Native AOT publish-and-run samples; and
5. update the catalog, support manifest, public API baseline, package graph, notices, and roadmap.

An out-of-tree consumer may build a custom statically linked composition from public contracts if
that API is approved later, but Broiler does not advertise or support a binary plug-in ABI.

---

## 7. Security, resources, and host boundary

Bytecode is untrusted input even when a local compiler produced it. Verification and resource
accounting are part of correctness, not optional hardening.

### Load-time requirements

- Checked arithmetic for every length, count, offset, index, and allocation calculation.
- Bounds on artifact bytes, sections, constants, functions, locals, metadata, nesting, and
  aggregate verifier work.
- Control/data-flow validation before execution, including unreachable regions and profile-
  specific handler or structured-control rules.
- Deterministic rejection for unknown opcodes, sections, features, versions, imports, and invalid
  metadata.
- No allocation based on an untrusted declared count before the count passes its configured bound.

### Run-time requirements

- Per-execution instruction fuel or bounded cancellation polling, call/frame depth, allocation,
  memory/table growth, host-call, and wall-clock policy supplied by the host.
- A defined result taxonomy separating normal completion, JavaScript throw, WebAssembly trap,
  cancellation, resource exhaustion, invalid artifact, and host failure.
- Host exceptions cannot tear down or corrupt another runtime; profile adapters translate them
  according to the declared host contract.
- Runtime, program/module, and profile-owned state is reclaimed on dispose and reaches a measured
  memory plateau under repeated load/run/evict cycles.
- Concurrent runtimes share only immutable verified artifacts unless a later ownership ADR and
  stress evidence explicitly approve more.

### Host capabilities

Hosts register narrow typed capabilities explicitly. A profile import names a declared capability
and signature; it cannot enumerate arbitrary CLR members. Capability lookup, permissions,
reentrancy, thread affinity, cancellation, and exception translation are part of the cache key or
runtime identity where they affect semantics. The initial shared host registry does not itself
bridge JavaScript values to WebAssembly imports.

---

## 8. Milestones

Every milestone records current evidence separately from plan statements. A status update may
mark an item complete only when its objective exit gate has durable commands, logs, manifests, and
source identities.

### VM-0 — Freeze ownership, terminology, and the build-proven graph

- **Owner:** Broiler.VM architecture owner with Broiler.JS front-end/runtime and WebAssembly-profile
  owners; release/AOT reviews the composition roots.
- **Current evidence:** No `Broiler.VM` implementation or verified project graph exists. The
  Broiler.JS expression-model/emitter split is landed seed evidence, while
  [Phase 6 status](../../Broiler.JS/docs/roadmap/Phase-6.status.md) records zero production VM
  items. Existing browser-Wasm applications do not implement a Wasm interpreter.
- **Next action:** Write the boundary ADR and project-shell spike. Pin profile terminology,
  dependency direction, package hypotheses, stable IDs, first Wasm feature manifest, JavaScript
  deployment decision crosswalk, host boundary, and raw-payload/envelope rules.
- **Dependencies:** Broiler.JS MOD-M2/MOD-M3 graph and AOT evidence; the JavaScript capability scope
  selected by MOD-M9/Phase 6 item 6-0; named ownership for the Wasm standard manifest.
- **Objective exit gate:** An acyclic shell graph builds; architecture tests express every
  forbidden edge; the ADR names package/composition roots, profile/version semantics, RIDs,
  security ownership, and support terminology; no unresolved document describes a VM profile as
  a JavaScript bootstrap or compiler profile.

### VM-1 — Build the semantics-neutral runtime and static catalog

- **Owner:** Broiler.VM core/runtime owner.
- **Current evidence:** Broiler.JS has explicit bootstrap and typed registry patterns, but there is
  no VM-wide profile contract, immutable catalog, or aggregate composition.
- **Next action:** Implement the minimal contracts, builder, descriptor validation, direct factory
  catalog, per-runtime executor creation, result taxonomy, limits, cancellation, diagnostics, and
  a fake profile used only for contract tests. Add direct JavaScript and WebAssembly entries as
  shells without claiming their semantics complete.
- **Dependencies:** VM-0 graph/ADR and package names; no dependency on production JavaScript or Wasm
  opcode implementation.
- **Objective exit gate:** Core/catalog tests prove deterministic registration, duplicate/alias
  rejection, unknown-profile and version failures, catalog-order independence, per-runtime state
  isolation, and explicit absence of reflection/name-based discovery. Trimmed and Native AOT test
  hosts construct the fake, JavaScript-shell, and WebAssembly-shell profiles through direct roots.

### VM-2 — Establish bounded artifacts, verification, and resource enforcement

- **Owner:** Broiler.VM core security owner plus each profile verifier owner.
- **Current evidence:** The portable JavaScript numeric seed validates a narrow immutable format;
  no common artifact descriptor, Wasm verifier, shared limit contract, corruption corpus, or
  coverage-guided fuzz result exists.
- **Next action:** Implement descriptor/profile matching, bounded readers, verifier result
  contracts, common envelope parsing where approved, resource-budget propagation, deterministic
  failure classes, malformed corpora, and fuzz entry points. Build one safe vertical artifact for
  each profile shell before expanding semantics.
- **Dependencies:** VM-1 runtime/catalog; VM-0 artifact ADR; profile owners must define their first
  format/feature versions and limits.
- **Objective exit gate:** Truncated, corrupt, oversized, mismatched, unknown-version, and
  resource-hostile artifacts fail before execution without out-of-budget allocation. Unit,
  property, and fuzz suites retain minimized regressions. The same failure categories are stable
  in JIT, trimmed, and Native AOT hosts.

### VM-3 — Deliver the JavaScript built-in profile

- **Owner:** Broiler.JS semantics/compiler owners and the Broiler.VM JavaScript-profile owner.
- **Current evidence:** The numeric `Portable` runtime/compiler and execution-only Native AOT
  sample are seeds only. Phase 6 records no general JavaScript interpreter, shared production
  semantic-IR migration, accepted JavaScript-profile ABI, format, verifier, or three-way conformance result.
- **Next action:** Follow Phase 6 ordering: scaffold the independent expected/IL/VM harness; migrate
  the IL backend to shared production semantic IR; specify the JavaScript `ValueSlot`, frame,
  environment, call, GC-root, completion, and suspension ABI; then grow format, verifier, lowering,
  and interpreter in vertical semantic slices. Register the resulting executor through VM-1's
  direct JavaScript descriptor.
- **Dependencies:** VM-0 through VM-2; the terminal JavaScript capability/deployment ADR; the
  applicable Broiler.JS MOD-M2/MOD-M3/MOD-M4 boundaries. Runtime compilation additionally depends
  on an AOT-clean parser/semantic/lowering closure.
- **Objective exit gate:** Every feature in the approved JavaScript manifest matches an independent
  expected result on the current IL and VM arms; malformed/resource cases pass VM-2; the
  execution-only application publishes and runs representative verified bytecode on every claimed
  RID; every approved runtime-compiler composition separately compiles source inside its published
  Native AOT process and executes it; precise exclusions and failure modes are public. This gate
  makes no performance claim.

### VM-4 — Deliver the WebAssembly built-in profile

- **Owner:** Broiler.VM WebAssembly-profile owner, with security review for validation and host
  imports.
- **Current evidence:** The repository contains browser-WebAssembly application/deployment work,
  but no Broiler bytecode VM that decodes, validates, instantiates, and executes Wasm modules.
- **Next action:** Pin the conformance-suite revision and initial proposal manifest, then implement
  binary decoding, validation, typed frames, module instantiation, numeric/control/function slices,
  globals, memories, tables, imports/exports, and traps vertically. Defer every unpinned proposal
  with a deterministic validation result.
- **Dependencies:** VM-0 through VM-2; approved Wasm feature and host-import manifests; provenance
  and licensing review for the pinned conformance corpus.
- **Objective exit gate:** The supported pinned WebAssembly spec-test manifest passes with no
  unexplained failures; unsupported proposals and malformed/resource-hostile modules fail
  deterministically; trap/import/memory/table lifecycle suites pass; WebAssembly-only and aggregate
  applications publish and run representative modules under Native AOT on every claimed RID. The
  manifest, not the profile name alone, defines the support claim.

### VM-5 — Prove composition and additional built-in extensibility

- **Owner:** Broiler.VM architecture/developer-experience owner with release engineering.
- **Current evidence:** The intended direct catalog is documented, but no third profile fixture,
  catalog drift check, or single-profile/aggregate package closure proves that it remains
  extensible without runtime discovery.
- **Next action:** Add a minimal test-only built-in profile using the contributor sequence, generate
  or validate the catalog/support manifest, add architecture tests for direct factories and package
  roots, and publish JS-only, Wasm-only, and aggregate closure reports.
- **Dependencies:** VM-1 and VM-2; both required profile shells must expose their final descriptor
  shape. Product JavaScript/Wasm completeness is not required to test catalog mechanics.
- **Objective exit gate:** A fixture profile is added without changing the core execution loop or
  using reflection; CI detects duplicate IDs, undocumented entries, missing factories, forbidden
  edges, and catalog/manifest drift; the three supported composition roots contain exactly their
  declared profiles and publish successfully under trimming and Native AOT.

### VM-6 — Harden lifecycle, concurrency, diagnostics, and host integration

- **Owner:** Broiler.VM runtime owner with JavaScript, WebAssembly, host-integration, and concurrency
  owners.
- **Current evidence:** Broiler.JS has a concurrency roadmap and isolated runtime mechanisms, but
  there is no cross-profile lifecycle, reentrancy, cancellation, host-failure, or memory-plateau
  evidence for Broiler.VM.
- **Next action:** Test independent runtimes and profile mixtures under create/load/run/cancel/
  dispose loops; define thread affinity and reentrancy; enforce host capability allowlists and
  typed signatures; attach stable source/bytecode/module diagnostics; and measure reclamation of
  frames, modules, memories, interned data, and caches.
- **Dependencies:** Correct vertical slices from VM-3 and VM-4; any shared artifacts or mutable
  optimizer state additionally require the ownership/publication gates in the Broiler.JS
  modernization concurrency plan.
- **Objective exit gate:** Stress and soak suites show deterministic isolation, bounded cancellation,
  correct exception/trap translation, no cross-runtime state leakage, no use-after-dispose, and a
  declared memory plateau. Diagnostics identify profile/version/artifact locations without leaking
  host secrets. Host imports cannot reach undeclared CLR surface.

### VM-7 — Take per-profile baselines and fund only measured optimization

- **Owner:** Performance owner plus the affected profile owner; persistence/security owns stored
  format changes.
- **Current evidence:** Broiler.JS Phases 7–9 describe gates but record no accepted production VM
  measurements. There is no Broiler.VM WebAssembly baseline. Catalogue labels and results from the
  current IL engine are not VM-profile evidence.
- **Next action:** Take uninstrumented decision-grade JavaScript and WebAssembly baselines on their
  representative JIT and Native AOT workloads. Attribute decode/verify, dispatch, values/boxing,
  calls, properties or memories/tables, host calls, allocation, GC, RSS, startup, image/package,
  artifact size, and tail latency. Open persistence, feedback, quickening, superinstruction,
  encoding, or PGO work only under its own measured gate.
- **Dependencies:** VM-3 or VM-4 correctness for the profile being measured; the repository's
  decision-grade measurement rules; VM-6 ownership for mutable/shared state. JavaScript IL tier-up
  additionally depends on the Phase 9 dynamic-code-capable entry gate.
- **Objective exit gate:** Each funded optimization ends accepted, retained as an owned/expiring
  experiment, deferred, or removed under a predeclared decision rule. Candidate/control semantics
  remain conformant; allocation, GC, memory, startup, code/image/artifact size, and tail guardrails
  pass. No result from one profile is generalized to another without a separate population and
  measurement.

### VM-8 — Package, publish, and continuously recertify

- **Owner:** Broiler.VM release owner with package, security, API, documentation, and profile
  owners.
- **Current evidence:** There are no Broiler.VM packages, public API baseline, feed-consumer tests,
  release support table, Native AOT RID bundle, or rollback contract.
- **Next action:** Finalize only the package boundaries justified by VM-0 evidence; create pristine
  feed consumers and JS-only/Wasm-only/aggregate samples; freeze API and artifact promises; publish
  support/exclusion tables; complete dependency, license, security, and human review; and wire
  graph/catalog/AOT/conformance drift checks into required CI.
- **Dependencies:** VM-3 and VM-4 release manifests, VM-5 compositions, VM-6 hardening. VM-7
  optimization is not required unless a product threshold says the correct baseline is unshippable.
- **Objective exit gate:** Every advertised package restores from a feed without repository project
  references, its public API/package graph matches the baseline, all required conformance and
  malformed-input suites pass, every claimed RID publishes and runs the declared composition with
  warnings as errors, notices/reviews are complete, rollback is tested, and recertification triggers
  are documented.

### Delivery order

```text
VM-0 graph/ownership
  └→ VM-1 core/static catalog
       └→ VM-2 artifact/verifier/resource boundary
            ├→ VM-3 JavaScript correctness ─┐
            ├→ VM-4 WebAssembly correctness ├→ VM-6 hardening ─→ VM-8 release
            └→ VM-5 future-profile proof ───┘          │
                                                       └→ VM-7 measured branches
```

VM-3 and VM-4 may proceed in parallel after VM-2. VM-5 can use shells and a fixture before either
profile is feature-complete. VM-7 is per-profile and may begin for one accepted profile while the
other remains under correctness work. VM-8 advertises only the profiles whose gates have closed.

---

## 9. Test and evidence matrix

| Area | Required tests/evidence | Failure that blocks release |
|---|---|---|
| Core/catalog | duplicate/alias/unknown IDs; version mismatch; explicit selection; order independence; factory identity; fake third profile | reflection/name discovery, silent replacement, or catalog/manifest drift |
| Dependency architecture | acyclic graph; core references no profile; profiles do not reference one another; JavaScript profile/compiler has no IL/Emit closure; composition contains only declared profiles | forbidden project/assembly edge or undeclared dynamic loading |
| Artifact safety | truncation, invalid sizes/indexes/opcodes/sections/control flow, corrupt envelope, checksums, resource exhaustion, minimized fuzz corpus | invalid instruction executes, unbounded allocation, crash, hang, or nondeterministic failure class |
| JavaScript correctness | pinned independent expected outcomes, current IL arm, VM arm, source/precompiled/round-trip arms, approved test262 and host manifests | unexplained expected/IL/VM delta or undocumented exclusion |
| WebAssembly correctness | pinned spec-test/proposal manifest, validation, instantiation, traps, numeric edges, memory/table/global/import/export behavior | unexplained supported-manifest failure or acceptance of an undeclared proposal |
| Lifecycle/concurrency | repeated load/run/cancel/dispose, independent runtimes, mixed profiles, reentrancy, memory plateau, cache eviction | shared mutable leakage, race, unbounded retention, use-after-dispose, or unbounded cancellation latency |
| Host security | typed allowlist, signature mismatch, permission denial, thread affinity, host exception translation, secret-safe diagnostics | arbitrary CLR discovery/access or cross-runtime capability leak |
| Native AOT | JavaScript-only, WebAssembly-only, aggregate, and each approved runtime-compiler composition; declared RID/device matrix; warnings/suppressions inventory | a claimed composition fails publish/run or reaches forbidden dynamic code |
| Packaging | pristine feed restore/build/run, API/package baselines, dependency/license/notices, image/package sizes | repository-only success, undeclared dependency, missing notice, or unsupported surface implied by package/API |
| Performance | uninstrumented candidate/control identity, A/A lane validity, per-profile workload, allocation/GC/RSS/startup/tail/code/image/artifact sizes | claim lacks a predeclared rule, semantic bundle, resource guardrail, or comparable control |

Generated results are evidence artifacts, not substitutes for pinned manifests and durable summaries.
Every accepted bundle records source revision, clean/dirty inputs, SDK/runtime, publish properties,
profile and feature versions, RID/device, effective GC/JIT/AOT state, commands, and raw outputs.

---

## 10. Release gates

A Broiler.VM preview or stable release must satisfy all applicable gates:

1. **Support truth:** the public table names VM profile, feature manifest, deployment/compiler
   composition, host capabilities, RIDs, and deterministic exclusions separately.
2. **Graph and registration:** generated/current dependency closure matches VM-0, the catalog is
   static and documented, and no portable composition reaches dynamic loading or IL Emit.
3. **Correctness and safety:** the profile's conformance manifest, malformed corpus, fuzz
   regressions, resource limits, lifecycle, and host-security suites pass.
4. **Native AOT:** each advertised composition publishes and runs representative artifacts on its
   declared matrix with trim/AOT warnings treated as errors. Suppressions are reviewed and scoped.
5. **Packages and consumers:** packages restore from a feed, samples use public APIs, API/package
   baselines and notices are current, and aggregate/single-profile claims match their closures.
6. **Operations:** diagnostics, cancellation, rollback, format-version rejection/migration,
   vulnerability response, and recertification owners are named.
7. **Measurement honesty:** performance is not a correctness prerequisite unless the product ADR
   sets a threshold. Every published performance statement meets the owning measurement gate.

Recertification is required when the SDK/runtime, compiler or verifier, profile format/feature
manifest, package graph, host capability surface, Native AOT settings, RID matrix, cache identity,
resource defaults, conformance corpus, or representative workload changes.

---

## 11. Risks and stop conditions

| Risk | Mitigation / stop condition |
|---|---|
| A generic core becomes a lowest-common-denominator language runtime | Keep opcodes, values, frames, verifier rules, and semantics profile-owned. Stop a proposed shared primitive when it introduces a profile-to-profile dependency or semantic conversion tax without evidence. |
| JavaScript semantics are forked between IL and VM | Migrate IL to the shared production semantic IR first and require the independent expected/IL/VM gate. Agreement between two Broiler backends is not an oracle. |
| The numeric portable seed freezes an unsuitable ABI | Treat it as execution/AOT seed evidence only; prove the general JavaScript ABI before accepting a persisted format. |
| WebAssembly scope grows with every proposal | Pin a feature manifest and reject everything else deterministically. A proposal enters only with owner, tests, resources, AOT evidence, and maintenance budget. |
| Static registration silently stops being extensible | Prove a test-only third profile, catalog/manifest drift tests, and direct composition roots. Do not replace compile-time extensibility with reflection. |
| Trimming removes a profile or host path | Directly root factories/capabilities and publish/run JS-only, Wasm-only, aggregate, and runtime-compiler samples. A linker annotation without execution is insufficient. |
| Aggregate package size becomes mandatory | Keep core/profile/aggregate boundaries explicit and add single-profile compositions only where measured package evidence justifies them. |
| Malicious bytecode exhausts verifier or runtime resources | Checked/bounded readers, pre-execution verification, fuel/cancellation, memory/table/frame/import budgets, fuzzing, and stable resource failure results are release gates. |
| Mutable feedback or caches leak across runtimes | Keep canonical artifacts immutable and mutable state owner-scoped; require disposal/plateau/concurrency tests before sharing anything. |
| A common host registry becomes arbitrary CLR interop | Typed allowlisted capabilities only; no member enumeration or name-based CLR binding in portable profiles. |
| One host is mistaken for JS/Wasm interoperability | Keep profiles isolated. Fund a bridge only after a separate value/lifetime/exception/security ADR and conformance plan. |
| Internal formats become accidental public contracts | Version from the first byte, but promise persistence only after its explicit Phase 8/VM-7 gate. Reject unsupported versions deterministically. |
| VM work is justified as an unmeasured IL speed-up | Capability/correctness comes first. On dynamic-code-capable hosts, use accepted per-profile candidate/control evidence before any performance claim. |

Stop or re-scope a milestone when its graph is cyclic, a portable closure reaches Emit or runtime
discovery, a verifier cannot bound work before allocation/execution, the independent conformance
oracle cannot be maintained, the declared Native AOT composition cannot publish and run, or the
named ownership/maintenance ceiling is absent. A difficult or slow milestone is not itself a stop
condition; an untruthful support claim is.

---

## 12. Platform and specification references

VM-0 records immutable revisions for implementation and release evidence; these moving links
are discovery entry points, not substitutes for the pinned manifests:

- [.NET Native AOT deployment and limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [.NET Native AOT warning guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/fixing-warnings)
- [WebAssembly Core Specification](https://webassembly.github.io/spec/core/)
- [WebAssembly specification tests](https://github.com/WebAssembly/spec/tree/main/test)

## 13. Existing Broiler.JS plan crosswalk

This roadmap is the component and multi-profile owner. The linked Broiler.JS documents remain the
detailed JavaScript-profile work plans and evidence ledgers:

- [Broiler.JS roadmap index](../../Broiler.JS/docs/roadmap/README.md)
- [Modernization program and MOD-M9 decision](../../Broiler.JS/docs/roadmap/Modernization.md)
- [Modernization delivery DEL-9 through DEL-11](../../Broiler.JS/docs/roadmap/ModernizationDelivery.md)
- [Assembly restructure and AOT boundary](../../Broiler.JS/docs/roadmap/Assemblies.md)
- [Assembly dependency rules](../../Broiler.JS/docs/architecture/dependencies.md)
- [Phase 6 plan](../../Broiler.JS/docs/roadmap/Phase-6.md) and
  [status](../../Broiler.JS/docs/roadmap/Phase-6.status.md)
- [Phase 7 plan](../../Broiler.JS/docs/roadmap/Phase-7.md) and
  [status](../../Broiler.JS/docs/roadmap/Phase-7.status.md)
- [Phase 8 plan](../../Broiler.JS/docs/roadmap/Phase-8.md) and
  [status](../../Broiler.JS/docs/roadmap/Phase-8.status.md)
- [Phase 9 plan](../../Broiler.JS/docs/roadmap/Phase-9.md) and
  [status](../../Broiler.JS/docs/roadmap/Phase-9.status.md)
- [Broiler.JS public API and profile claims](../../Broiler.JS/docs/public-api.md)

Evidence is never transferred by analogy. VM core or WebAssembly progress does not close a
Broiler.JS Phase 6 item; JavaScript conformance does not close a WebAssembly milestone; a
WebAssembly Native AOT run does not prove a JavaScript runtime compiler closure; and Phase 9's
optional IL adaptivity is not part of the common VM release gate.
