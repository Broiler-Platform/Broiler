# Broiler.VM roadmap

**Status:** Proposed component roadmap. [The evidence ledger](roadmap.status.md) currently records
VM-0 through VM-8 as not started. No Broiler.VM milestone is complete merely because this document
exists.

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
| **Built-in profile** | A profile whose factory and dependencies are directly referenced at build time and rooted in a static catalog. A built-in may be Broiler-provided or application-local; it is never discovered at run time. Built-in does not mean that every product must carry every profile package. |
| **Portable seed** | The existing `Broiler.JavaScript.Portable` numeric project and its Native AOT sample. In this roadmap *portable* names only that seed; a composition that must avoid dynamic code is a **dynamic-code-prohibited composition**, and gates use that term instead. |
| **Verified artifact** | The opaque, immutable, profile-bound output of successful verification. Execution and instantiation consume this handle, never caller-owned raw bytes. |
| **Core contract version** | The numbered revision of the profile-neutral lifecycle, operation-result, resource-authority, guest-initiated-load, external-control, and host-capability contract frozen by VM-0 and implemented by VM-1. It is versioned independently of any profile format or feature manifest. |
| **Guest-initiated load** | A verification requested by executing guest code rather than by the caller, such as JavaScript module resolution, dynamic `import()`, or `eval`. It produces an ordinary verified artifact through the ordinary verification path. |
| **Artifact-provider capability** | The typed, allowlisted host capability that answers a guest-initiated load with a descriptor and bytes. It is a distinct capability kind from a value-returning import and is never implied by one. |
| **External suspension** | A pause requested by the host or a diagnostic client rather than by guest code, from which execution may resume. It is distinct from guest-initiated suspension and from terminal cancellation. |

The aggregate Broiler.VM product contains both required built-ins. A size-sensitive product may
use a statically defined JavaScript-only or WebAssembly-only composition, but it may not claim
the aggregate surface. An unknown profile, unsupported feature manifest, or incompatible format
version is a deterministic load failure, never a best-effort fallback to another profile.

### Scope

Broiler.VM owns:

- explicit profile selection and an immutable built-in catalog;
- bounded artifact loading and profile/version matching, including bounded mediation of
  guest-initiated loads;
- the common verify/load/instantiate/invoke/suspend/resume/cancel/dispose lifecycle, per-runtime
  and shared aggregate resource-budget authority, diagnostics, and profile-neutral
  operation-result envelopes;
- typed host-capability registration, including artifact-provider capabilities, and per-runtime
  ownership;
- the numbered core contract version and the amendment procedure that changes it; and
- composition, trimming, Native AOT, and package evidence for the component boundary.

Each VM profile owns:

- its bytecode payload format and feature manifest;
- decoding, validation, and profile-specific resource checks;
- its value, frame, call, control-flow, trap/exception, and suspension model;
- imports, exports, and conversions at its host boundary;
- the language meaning of any guest-initiated load it declares, including specifier resolution,
  linking, lexical context, evaluation ordering, and what it exposes while externally suspended;
- its typed normal-result and fault payloads and the projection API that exposes them without
  adding language cases to the core;
- conformance fixtures and profile-specific optimizations; and
- any compatibility promise for its persisted profile payload.

Source compilers do not belong in the execution core. Broiler.JS owns JavaScript parsing,
backend-neutral semantic analysis, and JavaScript-bytecode lowering. The WebAssembly profile
accepts binary modules; product WAT/WAST parsing and source-language compilation are not
initial-profile requirements. The required test-only WAST ingestion path is isolated from product
packages and Native AOT closures as described under the WebAssembly profile.

### Non-goals for the first release

- one universal JavaScript/WebAssembly opcode set, tagged value, or frame ABI;
- reflection-based or unloadable runtime plug-ins;
- automatic artifact/profile detection by trying multiple decoders;
- an implied JavaScript-to-WebAssembly invocation bridge;
- every WebAssembly proposal; product WAT/WAST support; a Wasm source compiler; WASI 0.1;
  the component model, WIT, or WASI 0.2 and later for the first release;
- JavaScript IL tier-up, deoptimization, or OSR in the VM core;
- a debug wire protocol, a cross-profile inspection API, or a profile-neutral breakpoint model.
  VM-0 freezes only the external-suspension transitions that a profile-owned debug surface needs;
  and
- performance claims before correct uninstrumented baselines exist for each profile.

---

## 2. Engineering invariants

1. **A profile is selected explicitly.** The caller supplies a stable profile identity, or a
   checked Broiler.VM envelope supplies it and the caller confirms it. Raw WebAssembly remains
   usable under explicit `WebAssembly` selection. The runtime never guesses by probing every
   registered decoder.
2. **Registration is static and typed.** Aggregate and single-profile composition roots directly
   reference their profile factories; the generic runtime references no concrete profile. There is
   no `Assembly.Load`, `Type.GetType`, assembly scan, `Activator.CreateInstance`, magic type name,
   or module-initializer ordering dependency.
3. **Verification produces the only executable input.** Every external artifact is parsed with
   checked lengths and budgets. Verification snapshots or fully decodes caller-owned bytes into an
   opaque immutable handle, and only that handle may be instantiated or executed. Later mutation,
   disposal, or concurrent reuse of the caller's buffer cannot affect verified instructions. Bytes
   a profile obtains while executing take the same path: they become their own verified handle
   before anything in them runs, and no profile may execute source or bytes it acquired without
   one.
4. **The core is semantics-neutral.** It provides lifecycle and safety contracts, not a
   lowest-common-denominator ISA. JavaScript and WebAssembly may share a primitive only after
   dependency, correctness, and representation evidence proves that sharing is useful.
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
9. **Resource authority is trusted and monotonic.** At runtime creation the host supplies explicit
   ceilings or explicitly adopts bounded profile defaults; omission never means unbounded. Each
   profile may impose a stricter hard maximum, and artifact declarations may only request lower
   limits. Verification fixes their intersection as the handle's verification/instantiation
   ceilings before an untrusted allocation. Instance or invocation budgets may tighten those
   ceilings or allocate a remaining fuel/time allowance; they never raise them without producing a
   newly verified handle. Ceilings also compose: a host may create runtimes under one shared
   aggregate budget, a per-runtime ceiling may never exceed the parent's remaining allowance, and
   creating more runtimes may not multiply a host maximum.
10. **The common lifecycle is stable within a core contract version.** VM-0/VM-1 define ownership,
   state transitions, thread affinity, reentrancy, cancellation, suspension, resumption, and
   idempotent disposal. Profiles refine observable language behavior without changing the core
   state machine or adding JavaScript/WebAssembly cases to a core result enum. A capability the
   frozen contract cannot express is added by a numbered amendment, never by a profile-specific
   special case.
11. **Guest-initiated loads are mediated, bounded, and refusable.** A profile may obtain further
   artifacts while executing only through a declared artifact-provider capability. Each request is
   charged to the operation that made it, bounded in depth, fan-out, and cumulative bytes, and
   deterministically refused when the composition registers no provider. A profile never reaches
   the filesystem, the network, or a source compiler on its own.
12. **External control is a lifecycle state, not a side channel.** A host or diagnostic client may
   pause and resume execution only through transitions the core contract declares. Suspension
   requested from outside is distinct from guest-initiated suspension and from terminal
   cancellation, and what a paused profile exposes remains the profile's own surface.

### Core contract version and amendment

The profile-neutral contract frozen by VM-0 and implemented by VM-1 — lifecycle states and legal
transitions, operation-result categories, resource authority, verified-artifact ownership,
guest-initiated loads, external control, and the host-capability shape — carries one integer
**core contract version**, starting at 1. It is versioned separately from any profile format,
feature manifest, or package version, and every support table, catalog entry, and evidence bundle
names it.

Amendment is an expected event, not a failure. One is required whenever an approved profile
capability cannot be expressed by the frozen contract; the WebAssembly support manifest and the
terminal JavaScript MOD-M9/6-0 decision are the two known sources. The procedure is:

1. record the driving capability, the profile that needs it, and why no profile-owned design
   satisfies it;
2. mint core contract version *n+1* as a dated revision of the VM-0 ADR, stating which
   transitions, categories, or contracts changed and whether the change is additive;
3. re-evaluate accepted VM-1 through VM-8 evidence against the new version, recording what
   recertifies unchanged, what must be re-collected, and what is superseded, under the
   [status ledger](roadmap.status.md) update rules; and
4. publish the new version in the support table beside the profiles and packages that require it.

An additive amendment may leave existing profile packages source-compatible; changing an existing
transition or category may not. Neither is a reason to fork the contract: a second core state
machine maintained for one language is a stop condition under section 11.

---

## 3. Static profile registration

The exact public names are deferred to VM-0, but the required shape is a generic builder/catalog
whose entries contain immutable descriptors and direct factory delegates supplied by a composition
root:

```csharp
var catalog = VmCatalog.CreateBuilder()
    .AddBuiltIn(BuiltInVmProfiles.JavaScript)
    .AddBuiltIn(BuiltInVmProfiles.WebAssembly)
    .Build();

var vm = VmRuntime.Create(catalog);
```

This example is in the aggregate `Broiler.VM` composition, not `Broiler.VM.Runtime`; it expresses
dependency rooting, not a frozen API. JavaScript-only and WebAssembly-only roots supply their own
one-entry catalogs. A hand-maintained catalog is the initial preference because two entries do not
justify generator complexity. A source generator may replace the table later if it emits direct
calls, produces a reviewable manifest, and has a test proving that generated and documented
catalogs agree. Runtime reflection is not an allowed substitute.

Every catalog entry must provide:

- a stable, non-localized profile ID and display name;
- a supported profile-format range and feature-manifest IDs;
- an AOT-rooted verifier and per-runtime executor factory;
- bounded profile limit defaults that a host must explicitly adopt or override, plus
  host-capability descriptors;
- the core contract version it was built against, and whether it declares guest-initiated loads,
  asynchronous instantiation, or external suspension;
- a conformance manifest/version and diagnostics identity; and
- package and ownership metadata used by architecture and release checks.

Registration rejects duplicate IDs, alias collisions, missing factories, unsupported versions,
and descriptors whose declared identity differs from the produced executor. Catalog order has no
semantic effect. A future built-in is added by referencing its assembly and adding its descriptor
factory to an explicit composition root; it does not require a switch inside the execution loop.

The public source-level profile contract is intended to support **application-local statically
linked built-ins** once VM-5/VM-8 stabilize it. Broiler-owned IDs use the reserved `Broiler.*`
namespace; application-local IDs use a documented reverse-domain namespace. This is an AOT-safe
composition API, not a binary plug-in ABI: the profile is compiled with the application, its
generic instantiations and factories are rooted directly, and its compatibility is checked at
build/test time as well as catalog construction.

---

## 4. Package-boundary hypotheses

These names are hypotheses, not authorization to create assemblies. VM-0 must prove the graph
with project shells and the same assembly/package-budget discipline used by the
[Broiler.JS assembly roadmap](../../Broiler.JS/docs/roadmap/Assemblies.md).

| Logical boundary | Candidate package | Responsibility and dependency rule |
|---|---|---|
| Contracts | `Broiler.VM.Abstractions` | Profile IDs/descriptors, execution options/results, budgets, diagnostics, and typed host contracts; references no concrete profile. |
| Core runtime | `Broiler.VM.Runtime` | Builder, immutable catalog, bounded load/execute lifecycle, cancellation, and ownership; references abstractions, not Broiler.JS or a Wasm implementation. |
| JavaScript format contract | `Broiler.VM.JavaScript.Format` *(hypothesis)* | Canonical profile ID, opcodes, schema, encoder inputs, and bounded reader/verifier-facing structures shared by the JavaScript compiler and executor; contains no interpreter, realm, IL emitter, or host. VM-0 may keep this logical boundary in another assembly only if project shells preserve the same dependency rule. |
| JavaScript built-in | `Broiler.VM.JavaScript` | JavaScript payload reader/verifier/interpreter and adapter to the approved AOT-clean Broiler.JS runtime contracts; consumes the JavaScript format contract and never references the IL emitter or optional CLR host. |
| WebAssembly built-in | `Broiler.VM.WebAssembly` | Wasm decoder/validator/interpreter, typed stack, module instance, memories/tables/globals, imports/exports, and traps; does not reference Broiler.JS. |
| Aggregate composition | `Broiler.VM` | Statically registers both required built-ins and supplies the default package/sample surface; contains no profile semantics of its own. |
| JavaScript compiler | Name selected by Broiler.JS MOD-M2/MOD-M9 | Parser, shared semantic IR, and JavaScript-bytecode lowering; consumes the canonical JavaScript format contract, remains outside the execution-only closure, and is independent of the IL emitter and full interpreter. |

Single-profile composition roots may be provided when package and image evidence justifies them.
They remain explicit packages/samples rather than a runtime option that dynamically removes an
already rooted profile. No new assembly is accepted merely to shorten a file: it must enforce a
dependency, AOT, deployment, ownership, test, or package boundary.

The target direction is below; arrows mean **depends on**:

```text
JavaScript bytecode compiler ─→ Broiler.JS FrontEnd/Semantics
              │
              └─→ JavaScript format contract ←─ Broiler.VM.JavaScript

existing IL backend ──────────→ Broiler.JS FrontEnd/Semantics

Broiler.VM.Runtime ───────────→ Broiler.VM.Abstractions
Broiler.VM.JavaScript ────────→ Broiler.VM.Runtime / Abstractions
Broiler.VM.WebAssembly ───────→ Broiler.VM.Runtime / Abstractions
aggregate/static composition ─→ Runtime + JavaScript + WebAssembly
```

The verified project graph may adjust names and split points, but it must retain these rules:
the core knows no concrete profile; the two profiles do not depend on one another; the JavaScript
compiler and executor consume one canonical format contract without depending on one another;
JavaScript bytecode compilation does not reach IL; and only the aggregate or an explicit host
composition knows which built-ins it includes.

---

## 5. Artifact and versioning model

### Explicit descriptor, immutable verification result, and profile-owned payload

The verification API receives an immutable artifact descriptor plus caller-owned bytes. The
descriptor identifies the VM profile, profile-format version, feature-manifest ID, and any
artifact-requested limits. Those requests can only tighten the host/profile ceilings described in
section 7. If an artifact omits a limit, it adds no restriction; it does not remove the materialized
host/profile ceiling. The selected profile owns decoding of the payload:

- JavaScript uses the canonical, versioned format and verifier developed by Broiler.JS Phase 6.
- WebAssembly accepts a standard binary module under explicit `WebAssembly` selection and checks
  the Wasm binary version plus the selected proposal/feature manifest.

Raw payload support avoids wrapping interoperable `.wasm` modules merely to execute them. It does
not permit sniffing: a caller that labels JavaScript bytes as WebAssembly receives a deterministic
WebAssembly validation failure.

Successful verification returns an opaque `VerifiedArtifact`-shaped handle bound to the exact
profile descriptor, feature manifest, verifier/semantic version, effective verification/
instantiation ceilings, and host-signature assumptions used during validation. The handle owns a
byte snapshot or fully decoded immutable representation; it never aliases mutable caller storage.
Instantiate/execute
APIs accept only that handle. Sharing a handle across runtimes is allowed only when those identities
match and the profile declares the representation shareable; mutable instances, memories, realms,
feedback, imports, and host handles are never part of it.

VM-0 also fixes the handle lifetime. A handle is either ordinary managed immutable data or owns
explicitly disposable resources; it cannot be ambiguously borrowed from a runtime. If sharing and
disposal are both supported, explicit leases/reference ownership, idempotent disposal, and
deterministic use-after-dispose behavior prevent one runtime from invalidating another's input.

### Optional persisted envelope

If persistence is approved by the cold-start gate, ownership is split explicitly:

- Broiler.VM core owns a small bounded outer header, profile dispatch, byte ownership, atomic
  storage/replacement helpers, corruption reporting, and compatibility/rejection/migration of the
  outer envelope schema while treating the profile section as opaque.
- The profile owns its payload, semantic cache-key contribution, compiler/debug metadata,
  compatibility/migration policy, invalidation, and composition-specific fallback.

The common outer envelope plus the opaque profile section record at least:

- envelope magic and schema version;
- stable profile ID, profile-format version, and feature-manifest ID;
- engine semantic/cache version and compiler identity when applicable;
- payload and section lengths with configured upper bounds;
- canonical source/module identity and host-capability/cache-key inputs;
- corruption-detection checksum data and atomic replacement state; and
- optional source/debug metadata whose positions are validated by the profile.

It never persists object references, delegates, intern-table indexes, process-local shape IDs,
warmed inline caches, quickened authoritative opcodes, host handles, or other mutable execution
state. Loading always re-verifies the envelope and profile payload. Runtime-compiler JavaScript
may recompile known source after an invalid cache; execution-only JavaScript and WebAssembly
report a defined load failure and accept a separately supplied fresh verified artifact.

Compatibility is opt-in and has two independent version domains. Core decides whether an older
outer-envelope schema is read, migrated around its opaque profile bytes, or rejected. Each profile
separately decides whether its older payload is read, migrated, invalidated with a
composition-specific fallback, or rejected. Outer-envelope compatibility never implies profile
payload compatibility. Internal formats may evolve before a version is declared persisted, but
silently interpreting old bytes under new semantics is prohibited.

A checksum detects accidental corruption; it does not authenticate code or establish provenance.
Hosts that authorize artifacts from outside their trust boundary must separately bind an approved
content hash, signature, or distribution identity before execution. Verification remains mandatory
even for an authenticated artifact.

### Guest-initiated loads

A profile may need code the caller never supplied: JavaScript module resolution, dynamic
`import()`, direct `eval`, and the Function constructor all originate inside a running program.
The core treats that as an ordinary load requested from an unusual place, not as a second
execution path.

- The composition, not the guest, decides whether it is possible at all. A profile declares that
  it may request loads; the host either registers a typed artifact-provider capability or does
  not. An execution-only JavaScript composition registers none and refuses every request
  deterministically, which is the answer its capability manifest already publishes for `eval`.
- The provider returns a descriptor and bytes exactly as a caller would. The profile does not read
  files, open sockets, or invoke a compiler itself. A runtime-compiler composition supplies its
  compiler behind the provider capability, which keeps invariant 6's closure rule intact and keeps
  the compiler inside the declared Native AOT closure.
- The returned bytes become their own immutable verified handle before anything in them runs.
  Nesting relaxes no bound, skips no descriptor/profile match, and inherits no ceiling implicitly.
- Work is charged to the requesting operation. Nested verification and instantiation draw on the
  invoking instance or invocation's remaining fuel, time, and allocation allowance, and the nested
  handle's effective ceilings are the intersection of that remainder with the host and profile
  maxima. A nested load can exhaust an invocation; it can never enlarge one.
- Depth, fan-out, and cumulative nested bytes and verifier work have configured bounds. Detecting
  cycles in a module graph is the profile's problem; bounding recursion through the provider is
  the core's.
- Failures map onto existing categories rather than adding one. The nested load returns its own
  load/verification result, and the requesting operation reports the language-defined `profile
  fault`, or `host failure` and `resource exhaustion` when the provider or the budget failed
  rather than the artifact. Each carries the profile's typed payload identifying the request.
- Identity is recorded. The provider identity, capability version, and resolved artifact identity
  are cache-key inputs wherever a persisted envelope or semantic cache depends on them.

VM-0 freezes this contract even for a first release that ships no provider. Retrofitting
re-entrant verification into an already frozen lifecycle is a core contract amendment rather than
a profile change, and WebAssembly will never force the question: only the JavaScript surface
decided by 6-0 does.

---

## 6. Built-in profiles

### JavaScript

The JavaScript VM profile is the Broiler.VM home for the bytecode interpreter planned in
[Broiler.JS Phase 6](../../Broiler.JS/docs/roadmap/Phase-6.md). Broiler.JS continues to own
parsing, early errors, binding, scope, hoisting, private names, direct-eval rules, free-name
analysis, backend-neutral lowering, JavaScript runtime operations, and the independent
expected-result manifest. Broiler.VM supplies the static profile host and bounded execution
lifecycle.

The VM profile identity remains `JavaScript` across deployment compositions:

| Composition | Runtime contents | Required evidence |
|---|---|---|
| `execution-only` | JavaScript profile runtime, verifier, and precompiled bytecode; no parser/compiler | approved artifacts execute under Native AOT on every claimed RID |
| `narrow-runtime-compiler` | plus parser, shared semantic front end, and lowering for a named subset | approved source compiles inside the published AOT process and executes; exclusions are deterministic |
| `general-runtime-compiler` | plus the approved general source/compiler surface | independent expected/IL/VM conformance and the complete declared AOT closure |

The existing `Broiler.JavaScript.Portable` implementation is seed evidence only. Its numeric,
`double`-oriented bytecode must not freeze the general JavaScript ISA or value ABI, and migrating
or retaining it is decided by the graph/compatibility ADR rather than assumed.

Four entries on the 6-0 capability list are core-shaped rather than JavaScript-shaped, so the
profile cannot absorb them alone: dynamic `import()`, `eval`, and the Function constructor need
guest-initiated loads; a module graph with top-level await needs asynchronous instantiation;
breakpoints need external suspension; and Worker agents may place several JavaScript runtimes
under one shared aggregate budget. The MOD-M9-1 preliminary requirements packet answers all four
provisionally so that VM-0 can freeze or explicitly defer each one. A later reversal is handled as
a core contract amendment, not as a JavaScript-specific path through the core.

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

The WebAssembly built-in owns a separate typed execution model. VM-0 produces one immutable scope
ADR that pins the exact Core Specification and conformance-suite revisions, baseline features,
separately accepted proposals, import ABI, resource ceilings, and unsupported-feature behavior.
VM-4 consumes that ADR; it does not select or repin the scope while implementing it. A reasonable
first vertical plan covers binary decoding and validation, numeric types, structured control flow,
functions and calls, globals, linear memory, tables, module instantiation, imports/exports, and
traps. SIMD, threads, multiple memories, reference types beyond the selected baseline, exception
handling, GC, tail calls, memory64, the component model, and later proposals are supported only
when named in the versioned feature manifest and backed by their own tests.

The initial profile is **Core WebAssembly with explicitly registered Broiler host imports**; it
does not claim WASI. WASI 0.1/Preview 1 may be proposed later as a separately versioned, allowlisted
host-capability package with filesystem, environment, clock, random, network, and process authority
documented independently. WASI 0.2 and later depend on WIT/component-model decisions and remain out
of scope with the component model. The first Core import ABI is synchronous. Async imports or
stack-switching require a separate feature manifest and lifecycle/security ADR.

The support manifest separately names product and test-only import namespaces and whether product
hosts may link exports from an existing Wasm instance. The conformance host may implement
`spectest` and WAST `register` only in test projects. In every case the linker resolves module/name/
kind/signature explicitly; it never searches CLR members or assemblies.

The Wasm verifier checks types and stack states, structured branches, section order and indexes,
function/code agreement, memory/table/global limits, constant expressions, declared features,
and configured aggregate resources before instantiation. Runtime limits cover at least call depth,
instruction fuel or cancellation checks, memory pages/growth, table entries, module/function/
local counts, element/data segments, and host calls.

WebAssembly traps are profile results, not CLR process failures. Imports are resolved through the
typed linker to an allowlisted host capability or an explicitly supplied Wasm extern; arbitrary
reflection over host objects is not an interop mechanism. Floating-point, conversion, memory, and
trap edge cases follow the pinned WebAssembly conformance oracle rather than JavaScript behavior.

The profile lifecycle distinguishes an immutable verified module from a mutable module instance:
resolve typed imports, instantiate memories/tables/globals and active segments, run the optional
start function, invoke exports zero or more times, and dispose the instance. Multiple instances may
share a verified module. Core Wasm also permits **explicit imported-extern aliasing**: two instances
may receive the same typed function, table, memory, or mutable global when the support manifest and
linker explicitly allow it. The extern provider owns its lifetime/lease; growth, disposal,
reentrancy, limit accounting, and cross-instance visibility have stress tests, and no instance may
invalidate another's live lease. Instantiation checks the extern's actual and declared maximum
against the verified ceilings, and the ownership contract states whether a provider-owned resource
is charged to the provider, each instance reference, or both so aliasing cannot bypass or
accidentally multiply a budget. This ordinary Core linking is distinct from the
threads/shared-memory proposal, whose concurrent memory semantics remain unsupported unless a
later manifest says otherwise. Host-visible memory/table handles define behavior across growth and
disposal and cannot retain an invalid raw span.

Product WAT parsing remains a non-goal. The official Core conformance corpus is written as WAST
scripts, so VM-0 also pins one **test-only** path: a WAST script runner/converter or reviewed
generated binary/command fixtures that preserve assertions, registrations, multiple instances,
traps, invalid/malformed distinctions, and NaN expectations. Test tooling and text-format parsing
must remain outside every product package and Native AOT closure.

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

An out-of-tree consumer may build an application-local built-in from the stabilized public
source-level contracts and compile it into an explicit composition. VM-5 proves that workflow and
VM-8 freezes its supported API/package surface. Broiler does not advertise or support a binary
plug-in ABI, runtime discovery, or loadable extension directory.

---

## 7. Security, resources, and host boundary

Bytecode is untrusted input even when a local compiler produced it. Verification and resource
accounting are part of correctness, not optional hardening.

### Lifecycle and result boundary

VM-0 freezes the state model and VM-1 implements it before profile semantics expand:

1. an immutable catalog is built by a composition root;
2. a runtime is created with typed host capabilities, authoritative resource ceilings, and
   declared affinity/reentrancy rules;
3. raw bytes, or profile bytes extracted from a bounded persisted envelope, are verified into an
   immutable profile-bound handle;
4. a verified JavaScript program/module or WebAssembly module is instantiated into profile-owned
   mutable state;
5. execution or export invocation completes, suspends when the selected profile/host contract
   permits it, or returns a generic invocation outcome with a profile-owned typed payload;
6. a suspended operation resumes, is cancelled, or is disposed. Guest-initiated suspension resumes
   on the profile's own terms; external suspension resumes at the host's request and cannot be
   used to observe state the profile does not expose; and
7. cancellation and idempotent disposal transition sessions, instances, and any explicitly
   disposable verified handles to documented terminal states and reject later use deterministically.

Steps 3 and 4 may recur inside step 5 when a profile makes a guest-initiated load. The nested
operation is an ordinary instance of the same steps, runs under the requesting operation's
remaining budget, and may neither reorder nor skip them.

All public stages use a profile-neutral **operation-result envelope**, but their legal categories
are stage-specific:

- load/verification returns a verified handle or `invalid artifact`, `resource exhaustion`, or
  `cancellation`. Optional envelope loading is a bounded preprocessing step whose outer-schema,
  corruption, migration, profile, and version failures use `invalid artifact`; it never yields an
  executable handle or bypasses profile verification;
- instantiation, including WebAssembly import resolution and `start`, returns an instance or
  `profile fault`, `resource exhaustion`, `cancellation`, `host failure`, or `suspension` when the
  profile's declared manifest permits asynchronous instantiation. A JavaScript module graph with
  top-level await is the known candidate; VM-0 records whether core contract version 1 admits it,
  and adding it afterwards is an amendment; and
- invocation returns `normal`, `profile fault`, `suspension`, `cancellation`, `resource
  exhaustion`, or `host failure`. External suspension reuses `suspension` and adds no category,
  and neither does a guest-initiated load: its failures surface as the requesting operation's
  `profile fault`, `host failure`, or `resource exhaustion`.

Illegal lifecycle transitions and use-after-dispose return one stable core `invalid state` outcome;
they are neither invalid artifacts nor language faults. JavaScript throws, WebAssembly validation
errors and traps, and future-profile details are typed payloads owned and interpreted by their
profiles; adding a profile does not add a case to the common core. A Wasm validation error is the
profile payload of load/verification's `invalid artifact`; a Wasm `start` trap is an instantiation
`profile fault`. VM-0 also decides which calls may originate on another thread, whether
cancellation may be requested cross-thread, when reentrant execution is rejected, whether external
suspension may be requested and by whom, and how suspended state retains and releases runtime/host
resources. The reentrancy rules must state explicitly whether a guest-initiated load may re-enter
the runtime that requested it. Profiles separately define whether cancellation/resource
termination runs language cleanup and how that behavior is exposed.

### Load-time requirements

- Checked arithmetic for every length, count, offset, index, and allocation calculation.
- Effective limits are computed from profile hard maxima, host ceilings, and artifact requests
  before reading or allocating from an untrusted declared count; artifact metadata cannot raise a
  host or profile limit.
- Bounds on artifact bytes, sections, constants, functions, locals, metadata, nesting, and
  aggregate verifier work.
- Control/data-flow validation before execution, including unreachable regions and
  profile-specific handler or structured-control rules.
- Deterministic rejection for unknown opcodes, sections, features, versions, imports, and invalid
  metadata.
- No allocation based on an untrusted declared count before the count passes its configured bound.
- Configured bounds on guest-initiated loads: nesting depth, fan-out per operation, cumulative
  nested bytes, and cumulative nested verifier work, each charged to the requesting operation.
- Successful verification owns or fully decodes its input. Unit and stress tests mutate, dispose,
  and concurrently overwrite the original caller buffer after verification and prove that the
  verified handle and execution result cannot change.

### Run-time requirements

- Per-instance or per-invocation fuel/cancellation polling, call/frame depth, allocation,
  memory/table growth, host-call, and wall-clock budgets materialized from the verified handle and
  current host request. An omitted invocation override inherits the handle/runtime budget; an
  explicit override may only tighten it. Raising a verification/instantiation ceiling requires
  re-verification. Bulk memory/string helpers and other variable-work operations charge
  proportional work rather than one nominal instruction.
- Where a host creates several runtimes under one shared aggregate budget, fuel, wall-clock,
  allocation, and live-runtime counts are metered against the parent as well as each runtime.
  Exhausting the parent is reported as `resource exhaustion` to whichever operation observes it,
  and no runtime may be created or resumed once the parent has no remaining allowance.
- The stage-specific operation-result categories above; profile-owned APIs expose JavaScript
  throws, WebAssembly validation errors/traps, and future typed fault/result payloads.
- Host exceptions cannot tear down or corrupt another runtime; profile adapters translate them
  according to the declared host contract.
- Runtime, program/module, and profile-owned state is reclaimed on dispose and reaches a measured
  memory plateau under repeated load/run/evict cycles.
- Concurrent runtimes share only immutable verified artifacts by default. Explicit Wasm extern
  aliasing or any other mutable sharing requires a manifest-declared ownership/lease contract and
  the applicable VM-6 stress evidence; it is never inferred from using the same host registry.

### Host capabilities

Hosts register narrow typed capabilities explicitly. A profile import names a stable capability
ID, version, and signature; it cannot enumerate arbitrary CLR members. Capability lookup,
permissions, reentrancy, thread affinity, cancellation, and exception translation are part of the
cache key or runtime identity where they affect semantics. The initial shared host registry does
not itself bridge JavaScript values to WebAssembly imports or supply WASI implicitly.

An artifact-provider capability is a distinct capability kind rather than an ordinary import: it
answers a guest-initiated load with a descriptor and bytes instead of a value. It is declared,
allowlisted, versioned, and audited separately; registering value capabilities never implies one;
and a composition that omits it makes every guest-initiated load fail deterministically. Its
identity participates in cache keys, and a runtime source compiler, where a composition includes
one, is reached only through it.

---

## 8. Milestones

Every milestone records current evidence separately from plan statements. The authoritative current
state is [the Broiler.VM roadmap status ledger](roadmap.status.md). A status update may mark an item
complete only when its objective exit gate has durable commands, logs, manifests, and source
identities.

### VM-0 — Freeze ownership, terminology, and the build-proven graph

- **Owner:** Broiler.VM architecture owner with Broiler.JS front-end/runtime and WebAssembly-profile
  owners; release/AOT reviews the composition roots.
- **Current evidence:** No `Broiler.VM` implementation or verified project graph exists. The
  Broiler.JS expression-model/emitter split is landed seed evidence, while
  [Phase 6 status](../../Broiler.JS/docs/roadmap/Phase-6.status.md) records zero production VM
  items. Existing browser-Wasm applications do not implement a Wasm interpreter.
- **Next action:** Write the boundary ADR and project-shell spike. Pin profile terminology,
  dependency direction, package hypotheses, stable IDs, the minimum lifecycle and profile-neutral
  operation-result contracts, trusted resource-limit precedence, and immutable raw-payload/envelope
  ownership. Assign core contract version 1 and publish its amendment procedure. Decide and record
  the guest-initiated-load contract, the artifact-provider capability shape, the
  external-suspension transitions, whether asynchronous instantiation is admitted, and whether
  aggregate budgets are a core object or a host responsibility — each explicitly, even where the
  first release ships no implementation. Record preliminary JavaScript capability and
  deployment-decision inputs without waiting for terminal MOD-M9. Commit one authoritative
  WebAssembly support manifest, including its Core/proposal scope, product and test-only
  imports/linking, WASI stance, resource ceilings, provenance, and a test-only WAST ingestion
  proof. Specify the source-level contract and ID policy
  for application-local statically linked built-ins.
- **Dependencies:** The bounded preliminary JavaScript requirements packet, which must answer
  provisionally whether the JavaScript profile will need guest-initiated loads, asynchronous
  instantiation, external suspension, or a shared aggregate budget across Worker runtimes; and
  named ownership for the WebAssembly support manifest, corpus provenance, and licensing. Current
  Broiler.JS graph/AOT observations inform the shell hypotheses, but acceptance of MOD-M2, MOD-M3,
  or terminal MOD-M9/6-0 is not a dependency of VM-0 or VM-1 and cannot block generic core or
  WebAssembly progress.
  Terminal MOD-M9 consumes the neutral VM contract before VM-3.
- **Objective exit gate:** An acyclic shell graph builds; architecture tests express every
  forbidden edge; the ADR names package/composition roots, profile/version semantics, RIDs,
  security ownership, support terminology, lifecycle states, result/payload ownership, resource
  authority, verified-artifact ownership, and the supported source-level extension promise; core
  contract version 1 is assigned and its amendment procedure is published; the
  guest-initiated-load, asynchronous-instantiation, external-suspension, and aggregate-budget
  questions each carry a recorded decision rather than silence; no unresolved document describes a
  VM profile as a JavaScript bootstrap or compiler profile. The
  WebAssembly support manifest has one stable ID and no competing scope authority. A test spike
  accounts for every selected WAST command and proves that its runner/converter, text parser,
  `spectest` host, and generated-test metadata are absent from product and Native AOT closures.

### VM-1 — Build the semantics-neutral runtime and static catalog

- **Owner:** Broiler.VM core/runtime owner.
- **Current evidence:** Broiler.JS has explicit bootstrap and typed registry patterns, but there is
  no VM-wide profile contract, immutable catalog, or aggregate composition.
- **Next action:** Implement the minimal contracts, builder, descriptor validation, direct factory
  catalog, per-runtime executor creation, profile-neutral operation-result envelopes, typed profile
  payload boundary, limits, cancellation, diagnostics, lifecycle states, thread-affinity and
  reentrancy rules, and a fake profile used only for contract tests. Implement whichever of
  guest-initiated-load mediation, artifact-provider registration, external-suspension transitions,
  and aggregate budget metering VM-0 assigned to the core, including their refusal paths. Keep
  direct JavaScript and WebAssembly shell references in aggregate or single-profile composition
  projects; the generic runtime must not reference either concrete factory.
- **Dependencies:** VM-0 graph/ADR and package names; no dependency on production JavaScript or Wasm
  opcode implementation.
- **Objective exit gate:** Core/catalog tests prove deterministic registration, duplicate/alias
  rejection, unknown-profile and version failures, catalog-order independence, per-runtime state
  isolation, legal and illegal lifecycle transitions, cancellation/disposal behavior, declared
  thread affinity and reentrancy, typed profile-payload preservation, and explicit absence of
  reflection/name-based discovery. The fixture profile exercises a guest-initiated load through a
  fixture provider, deterministic refusal where no provider is registered, external suspension and
  resume, and aggregate budget exhaustion across several runtimes. Trimmed and Native AOT test
  hosts construct the fake profile through the generic contract and JavaScript/Wasm shells only
  through their direct composition roots. This accepted neutral contract, recorded with its core
  contract version, is the VM input consumed by Broiler.JS MOD-M9.

### VM-2 — Establish bounded artifacts, verification, and resource enforcement

- **Owner:** Broiler.VM core security owner, with profile verifier owners as contract consumers.
- **Current evidence:** The portable JavaScript numeric seed validates a narrow immutable format;
  no common artifact descriptor, Wasm verifier, shared limit contract, corruption corpus, or
  coverage-guided fuzz result exists.
- **Next action:** Implement descriptor/profile matching, bounded outer-envelope parsing where
  approved, an opaque immutable verified-artifact handle, trusted host/profile/artifact limit
  intersection, explicit default/omission behavior, invocation-only tightening, deterministic
  failure classes, a fixture-profile malformed corpus, and fuzz entry points. Bound guest-initiated
  loads: depth, fan-out, cumulative nested bytes and verifier work, charging to the requesting
  operation, and intersection of the nested handle's ceilings with the remaining allowance. Prove
  the common boundary with a fixture format; JavaScript and Wasm payload formats and verifiers
  remain owned by VM-3 and VM-4.
- **Dependencies:** VM-1 runtime/catalog and VM-0 artifact/resource ADR. Real profile owners supply
  interface probes and limit requirements, but their first production format is not a VM-2
  prerequisite.
- **Objective exit gate:** Truncated, corrupt, oversized, mismatched, unknown-version, and
  resource-hostile fixture artifacts fail before execution without out-of-budget allocation. The
  effective policy is computed before allocation and never exceeds the host ceiling. Execution
  consumes only the verified handle; tests mutate, dispose, and concurrently overwrite the
  caller's original buffer after verification without changing behavior. Unit, property, and fuzz
  suites retain minimized regressions, and the same failure categories are stable in JIT, trimmed,
  and Native AOT hosts. Omitted limits inherit materialized bounded policy, invocation overrides
  only tighten it, and a raised ceiling requires a newly verified handle. A fixture guest-initiated
  load cannot exceed, extend, or escape its requesting operation's budget; recursive and fan-out
  provider requests terminate at their configured bounds; and a composition with no registered
  provider refuses every request deterministically.

### VM-3 — Deliver the JavaScript built-in profile

- **Owner:** Broiler.JS semantics/compiler owners and the Broiler.VM JavaScript-profile owner.
- **Current evidence:** The numeric `Portable` runtime/compiler and execution-only Native AOT
  sample are seeds only. Phase 6 records no general JavaScript interpreter, shared production
  semantic-IR migration, accepted JavaScript-profile ABI, format, verifier, or three-way conformance result.
- **Next action:** Follow Phase 6 ordering: scaffold the independent expected/IL/VM harness; migrate
  the IL backend to shared production semantic IR; specify the JavaScript `ValueSlot`, frame,
  environment, call, GC-root, completion, and suspension ABI; then grow format, verifier, lowering,
  and interpreter in vertical semantic slices. Put canonical profile IDs, opcodes, schema, encoder
  inputs, and bounded decoder structures in the JavaScript format boundary consumed by both the
  compiler and executor; neither depends on the other. Register the resulting executor through the
  direct JavaScript composition root.
- **Dependencies:** VM-0 through VM-2; terminal Broiler.JS MOD-M9 after it has consumed VM-1's
  neutral contract; and the applicable MOD-M2/MOD-M3/MOD-M4 boundaries. Runtime compilation
  additionally depends on an AOT-clean parser/semantic/lowering closure. Where 6-0 approves dynamic
  `import()`, `eval`, top-level await, or debugging beyond what the accepted core contract version
  expresses, VM-3 waits for that amendment instead of adding a JavaScript-specific core path.
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
- **Next action:** Consume VM-0's pinned support manifest and conformance fixtures; do not repin or
  expand them in VM-4. Implement binary decoding, validation, typed frames, module instantiation,
  numeric/control/function slices, globals, memories, tables, declared imports/exports, and traps
  vertically. Keep an immutable verified module separate from each mutable instance; resolve
  imports, initialize segments, run the start function, invoke exports, and dispose through the
  frozen lifecycle. Reject every feature and import outside the manifest deterministically. If
  implementation evidence requires different scope, return to the VM-0 ADR and mint a new manifest
  version before continuing.
- **Dependencies:** VM-0's approved WebAssembly support manifest, conformance-ingestion proof,
  provenance, and licensing review; VM-1 and VM-2 contracts.
- **Objective exit gate:** The supported pinned WebAssembly spec-test manifest passes with no
  unexplained failure or skip, and every selected WAST command is traceable to the pinned suite and
  accounted as executed directly, converted losslessly, or explicitly excluded with a recorded
  reason. Unsupported
  proposals and malformed/resource-hostile modules fail deterministically; trap/import/start/
  memory/table/instance and explicitly imported-extern aliasing/lifetime suites pass; the default
  compositions reject unavailable
  `wasi_snapshot_preview1` imports and make no Component Model or WASI 0.2+ claim. A
  WebAssembly-only application publishes and runs representative modules under Native AOT on every
  claimed RID; aggregate closure and mixed-profile execution remain VM-5/VM-6/VM-8 evidence. The
  manifest, not the profile name alone, defines the support claim.

### VM-5 — Prove composition and additional built-in extensibility

- **Owner:** Broiler.VM architecture/developer-experience owner with release engineering.
- **Current evidence:** The intended direct catalog is documented, but no application-local profile
  fixture, ID-governance test, catalog drift check, or single-profile/aggregate package closure
  proves the advertised source-level extensibility without runtime discovery.
- **Next action:** In a separate consumer project, implement a minimal application-local built-in
  only through the public source contract and compose it by direct typed registration. Reserve the
  `Broiler.*` ID namespace for Broiler profiles and require an application-owned reverse-domain
  namespace for consumer profiles. Validate the catalog/support manifest, direct factories,
  package roots, and exact JavaScript-only, Wasm-only, aggregate, and fixture-consumer closures.
- **Dependencies:** VM-1 and VM-2 with stable public-candidate descriptor, verified-artifact, and
  executor contracts. Product JavaScript/Wasm completeness is not required to test composition.
- **Objective exit gate:** The consumer fixture is added without changing the core runtime,
  execution loop, or Broiler-owned profile projects and without reflection, name-based loading, or
  an extension directory. The milestone closes through independently recorded subgates:

  - **VM-5-COMMON:** public source contract, application-local fixture, ID governance, and its
    trimmed/Native AOT consumer;
  - **VM-5-JS:** the JavaScript-only root contains exactly the generic runtime and JavaScript
    profile shell;
  - **VM-5-WASM:** the Wasm-only root contains exactly the generic runtime and WebAssembly profile
    shell; and
  - **VM-5-AGG:** the aggregate root contains both required profiles and no undeclared one.

  CI detects duplicate/reserved IDs, undocumented entries, missing factories, forbidden edges, and
  catalog/manifest drift. Each applicable composition publishes/runs under trimming and Native
  AOT. VM-8 then freezes the source-compatibility promise; no binary plug-in ABI is implied. The
  parent VM-5 milestone is accepted only when all four subgates are accepted, but a matching
  profile release may consume its accepted common and profile-specific subgates earlier.

### VM-6 — Harden lifecycle, concurrency, diagnostics, and host integration

- **Owner:** Broiler.VM runtime owner with JavaScript, WebAssembly, host-integration, and concurrency
  owners.
- **Current evidence:** Broiler.JS has a concurrency roadmap and isolated runtime mechanisms, but
  there is no cross-profile lifecycle, reentrancy, cancellation, host-failure, or memory-plateau
  evidence for Broiler.VM.
- **Next action:** Validate and harden the lifecycle, affinity, reentrancy, cancellation, result,
  and disposal rules frozen in VM-0/VM-1. Test independent runtimes and available profile mixtures
  under create/verify/instantiate/run/suspend/resume/cancel/dispose loops; enforce host capability
  allowlists and typed signatures; attach stable source/bytecode/module diagnostics; and measure
  reclamation of frames, modules, memories, interned data, and caches. Stress guest-initiated loads
  under cancellation and disposal, external suspension and resume including a client that abandons
  a paused operation, and aggregate budget exhaustion across concurrent runtimes.
- **Dependencies:** A correct vertical slice from VM-3 or VM-4 for that profile's hardening gate;
  aggregate mixture evidence requires both. Shared artifacts or mutable optimizer state
  additionally require the applicable ownership/publication gates in the Broiler.JS modernization
  concurrency plan.
- **Objective exit gate:** Stress and soak suites show deterministic isolation, bounded cancellation,
  correct exception/trap translation, no cross-runtime state leakage, no use-after-dispose, and a
  declared memory plateau. A guest-initiated load in flight is cancelled and disposed with its
  requesting operation and leaves no partially verified state; an externally suspended operation
  resumes, cancels, or disposes deterministically and never blocks disposal indefinitely; and a
  shared aggregate budget is honored by concurrent runtimes rather than multiplied by them.
  Diagnostics identify profile/version/artifact locations without leaking host secrets. Host
  imports cannot reach undeclared CLR surface. Results close independently as
  **VM-6-JS**, **VM-6-WASM**, and **VM-6-AGG**; the aggregate child additionally requires
  accepted VM-6-JS/VM-6-WASM plus mixed-profile lifecycle and isolation evidence. The parent VM-6
  milestone is accepted only when all three child records are accepted.

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
- **Dependencies:** VM-3 or VM-4 correctness for the profile being measured and the repository's
  decision-grade measurement rules. An immutable single-runtime baseline may begin immediately
  after that correctness gate. Any mutable, shared, adaptive, cache-persistent, or concurrent
  optimization branch additionally requires the relevant VM-6 ownership/lifecycle evidence.
  JavaScript IL tier-up also depends on the Phase 9 dynamic-code-capable entry gate.
- **Objective exit gate:** Each funded optimization ends accepted, retained as an owned/expiring
  experiment, deferred, or removed under a predeclared decision rule. Candidate/control semantics
  remain conformant; allocation, GC, memory, startup, code/image/artifact size, and tail guardrails
  pass. No result from one profile is generalized to another without a separate population and
  measurement. Baselines are recorded independently as **VM-7-JS-BASELINE** and
  **VM-7-WASM-BASELINE**; every funded experiment has a profile-qualified child ID. The parent
  VM-7 milestone is accepted only when both baselines and every funded experiment have a terminal
  accepted/deferred/removed/owned-expiring disposition.

### VM-8 — Package, publish, and continuously recertify

- **Owner:** Broiler.VM release owner with package, security, API, documentation, and profile
  owners.
- **Current evidence:** There are no Broiler.VM packages, public API baseline, feed-consumer tests,
  release support table, Native AOT RID bundle, or rollback contract.
- **Next action:** Finalize only the package boundaries justified by VM-0 evidence; create pristine
  feed consumers and separate JavaScript-only, Wasm-only, aggregate, and application-local-profile
  samples; freeze API, source-extension, and artifact promises; publish support/exclusion tables;
  complete dependency, license, security, and human review; and wire graph/catalog/AOT/conformance
  drift checks into required CI and the status ledger.
- **Dependencies:** **VM-8-JS** requires VM-3, VM-5-COMMON, VM-5-JS, and VM-6-JS.
  **VM-8-WASM** requires VM-4, VM-5-COMMON, VM-5-WASM, and VM-6-WASM. **VM-8-AGG** requires both
  profile correctness gates, VM-5-COMMON/VM-5-AGG, and VM-6-JS/VM-6-WASM/VM-6-AGG. VM-7
  optimization is not required unless a product threshold says the correct baseline is unshippable.
- **Objective exit gate:** Every advertised package restores from a feed without repository project
  references, its public API/package graph matches the baseline, all required conformance and
  malformed-input suites pass, every claimed RID publishes and runs the declared composition with
  warnings as errors, notices/reviews are complete, rollback is tested, and recertification triggers
  are documented. JavaScript-only, Wasm-only, and aggregate claims close independently in the
  VM-8-JS, VM-8-WASM, and VM-8-AGG child records and name their exact manifests and evidence bundles
  in [the status ledger](roadmap.status.md). The parent VM-8 milestone is accepted only after all
  three children; a single-profile package may release while the parent remains in progress.

### Delivery order

```text
VM-0 graph/ownership
  └→ VM-1 core/static catalog
       ├→ Broiler.JS MOD-M9 terminal deployment decision ─┐
       └→ VM-2 immutable artifact/resource boundary       │
            ├→ VM-3 JavaScript correctness ←──────────────┘
            │    ├→ VM-7-JS-BASELINE
            │    └→ VM-6-JS hardening ─────────→ VM-8-JS release
            │                    └─────────────→ VM-7 mutable/shared branches
            ├→ VM-4 WebAssembly correctness
            │    ├→ VM-7-WASM-BASELINE
            │    └→ VM-6-WASM hardening ───────→ VM-8-WASM release
            │                    └─────────────→ VM-7 mutable/shared branches
            └→ VM-5-COMMON source-profile proof ────────→ every VM-8 child

VM-8-JS   also waits for VM-5-JS
VM-8-WASM also waits for VM-5-WASM
VM-8-AGG  = both correctness gates + VM-5-AGG + VM-6-AGG
```

VM-0 and VM-1 do not wait for the terminal MOD-M9/6-0 decision, but they do consume the MOD-M9-1
packet's provisional answers on guest-initiated loads, asynchronous instantiation, external
suspension, and aggregate budgets; a later reversal is handled as a core contract amendment rather
than a restart. Terminal MOD-M9 consumes the accepted neutral contract and must close before VM-3's
JavaScript composition claim. VM-3 and VM-4
may otherwise proceed in parallel after VM-2. VM-5 uses public-candidate contracts and a consumer
fixture before either required profile is
feature-complete. VM-6, VM-7, and VM-8 are tracked per profile; aggregate claims additionally wait
for both profile gates and mixed-composition evidence.

---

## 9. Test and evidence matrix

| Area | Required tests/evidence | Failure that blocks release |
|---|---|---|
| Core/catalog | duplicate/alias/unknown and reserved IDs; version mismatch; explicit selection; order independence; factory identity; public-contract application-local fixture; profile-neutral operation outcomes and typed payload preservation; core contract version recorded by every entry and support table | reflection/name discovery, silent replacement, core reference to a concrete profile, an undeclared or forked core contract version, or catalog/manifest drift |
| Dependency architecture | acyclic graph; core references no profile; profiles do not reference one another; JavaScript compiler and executor consume one canonical format contract without referencing each other; no IL/Emit closure in a dynamic-code-prohibited composition; composition contains only declared profiles | forbidden project/assembly edge, duplicate format authority, or undeclared dynamic loading |
| Artifact safety and policy | truncation, invalid sizes/indexes/opcodes/sections/control flow, corrupt envelope, post-verification caller-buffer mutation/disposal/concurrent overwrite, verified-handle profile/version identity and lease/disposal lifetime, explicit default adoption, omitted-limit inheritance, host/profile/artifact intersection, invocation-only tightening, guest-initiated-load depth/fan-out/cumulative bounds, nested budget charging, missing-provider refusal, minimized fuzz corpus | invalid instruction executes, caller mutation changes execution, one runtime invalidates another's shared handle, omission becomes unbounded, artifact/invocation policy raises a verified ceiling, a nested load enlarges or escapes its requesting operation's budget, a provider-less composition executes acquired bytes, unbounded allocation, crash, hang, or nondeterministic failure class |
| Persistence ownership | core outer-schema compatibility/rejection/migration, header/profile dispatch, and atomic corruption handling; profile payload/cache key/compiler/debug compatibility and migration/invalidation/fallback; content authorization separate from checksum | ambiguous migration owner, outer compatibility mistaken for payload compatibility, profile cache accepted under the wrong identity, torn update treated as valid, or checksum treated as authenticity |
| JavaScript correctness | pinned independent expected outcomes, current IL arm, VM arm, source/precompiled/round-trip arms, approved test262 and host manifests | unexplained expected/IL/VM delta or undocumented exclusion |
| WebAssembly correctness | single VM-0 support manifest; pinned Core/WAST corpus; WAST command/assertion/registration and lossless conversion accounting; validation, instantiation/start, traps, numeric edges, memory/table/global/import/export behavior; imported-extern aliasing/lifetime; explicit WASI and Component Model exclusions; test-tool closure audit | unexplained selected-manifest failure or skip, conversion loss, broken extern lifetime/visibility, acceptance of an undeclared proposal/import, accidental WASI claim, or WAT/WAST tooling in a shipped closure |
| Lifecycle/concurrency | frozen state transitions; repeated verify/instantiate/run/suspend/resume/cancel/dispose; external suspension, resume, and abandonment; guest-initiated load under cancellation and disposal; independent runtimes; per-profile and mixed-profile lanes; thread affinity; reentrancy; shared aggregate budget exhaustion; memory plateau; cache eviction | profile-specific state leaks into the core result enum, shared mutable leakage, race, unbounded retention, use-after-dispose, an externally suspended operation that cannot be resumed/cancelled/disposed, concurrent runtimes multiplying a host ceiling, or unbounded cancellation latency |
| Host security | typed allowlist, signature mismatch, permission denial, thread affinity, host exception translation, artifact-provider allowlist and its absence, secret-safe diagnostics | arbitrary CLR discovery/access, a provider reachable without declaration, a compiler reached outside the declared closure, or cross-runtime capability leak |
| Native AOT | JavaScript-only, WebAssembly-only, aggregate, application-local profile consumer, and each approved runtime-compiler composition; declared RID/device matrix; warnings/suppressions inventory and shipped dependency-closure audit | a claimed composition fails publish/run, reaches forbidden dynamic code/test tooling, or loses a directly rooted profile/capability |
| Packaging | pristine feed restore/build/run, API/package baselines, dependency/license/notices, image/package sizes | repository-only success, undeclared dependency, missing notice, or unsupported surface implied by package/API |
| Performance | uninstrumented candidate/control identity, A/A lane validity, per-profile workload, allocation/GC/RSS/startup/tail/code/image/artifact sizes | claim lacks a predeclared rule, semantic bundle, resource guardrail, or comparable control |

Generated results are evidence artifacts, not substitutes for pinned manifests and durable summaries.
Every accepted bundle records source revision, clean/dirty inputs, SDK/runtime, publish properties,
profile and feature versions, RID/device, effective GC/JIT/AOT state, commands, and raw outputs.

---

## 10. Release gates

A Broiler.VM preview or stable release must satisfy all applicable gates:

1. **Support truth:** the public table names VM profile, feature manifest, core contract version,
   deployment/compiler composition, host capabilities, guest-initiated-load and external-control
   support, RIDs, WASI/Component Model stance, and deterministic exclusions separately. A profile
   name alone is not a feature claim.
2. **Graph and registration:** generated/current dependency closure matches VM-0, the catalog is
   static and documented, the generic runtime references no concrete profile, and no
   dynamic-code-prohibited composition reaches dynamic loading or IL Emit. The public source-level
   profile contract and profile-ID namespace pass VM-5-COMMON; no binary plug-in ABI is implied.
3. **Correctness and safety:** the profile's conformance manifest, malformed corpus, fuzz
   regressions, immutable verified-artifact boundary, trusted limit intersection, lifecycle, and
   host-security suites pass.
4. **Lifecycle and results:** the frozen ownership, state-transition, affinity, reentrancy,
   suspension, resumption, external-control, guest-initiated-load, cancellation, and disposal rules
   pass for the advertised profile at its declared core contract version. Language throws, traps,
   and values remain typed profile payloads behind profile-neutral operation-result envelopes, and
   a guest-initiated load adds no core result category and cannot exceed its requesting
   operation's budget.
5. **Native AOT:** each advertised composition publishes and runs representative artifacts on its
   declared matrix with trim/AOT warnings treated as errors. Suppressions are reviewed and scoped.
6. **Packages and consumers:** packages restore from a feed, samples use public APIs, API/package
   baselines and notices are current, and JavaScript-only, Wasm-only, aggregate, and
   application-local-profile claims match their exact closures.
7. **Operations and persistence:** diagnostics, cancellation, rollback, format-version rejection,
   core-envelope recovery, profile-payload migration/invalidation/fallback, vulnerability response,
   and recertification owners are named.
8. **Evidence and release independence:** accepted evidence bundles and exact manifests are linked
   from the profile-qualified VM-5/VM-6/VM-7/VM-8 child records in
   [the status ledger](roadmap.status.md). A single-profile release does not wait for the other
   profile or aggregate child; an aggregate claim requires both profile correctness gates plus
   mixed-composition evidence.
9. **Measurement honesty:** performance is not a correctness prerequisite unless the product ADR
   sets a threshold. Every published performance statement meets the owning measurement gate.

Recertification is required when the SDK/runtime, compiler or verifier, core contract version,
profile format/feature manifest, package graph, host capability surface, Native AOT settings, RID
matrix, cache identity, resource defaults, conformance corpus, or representative workload
changes.

---

## 11. Risks and stop conditions

| Risk | Mitigation / stop condition |
|---|---|
| A generic core becomes a lowest-common-denominator language runtime | Keep opcodes, values, frames, verifier rules, and semantics profile-owned. Stop a proposed shared primitive when it introduces a profile-to-profile dependency or semantic conversion tax without evidence. |
| The core result enum grows one case per language | Keep only profile-neutral operation outcome categories in core and carry JavaScript throws, Wasm traps, and future-language outcomes as typed profile payloads/projections. Reject a profile that requires the core execution loop to learn its semantics. |
| JavaScript semantics are forked between IL and VM | Migrate IL to the shared production semantic IR first and require the independent expected/IL/VM gate. Agreement between two Broiler backends is not an oracle. |
| The JavaScript compiler and executor invent incompatible bytecode contracts | Give canonical IDs, opcodes, schema, encoder inputs, and bounded reader structures one neutral format owner. Neither compiler nor executor may reference the other or redefine the format. |
| The numeric portable seed freezes an unsuitable ABI | Treat it as execution/AOT seed evidence only; prove the general JavaScript ABI before accepting a persisted format. |
| WebAssembly scope grows with every proposal or import ecosystem | VM-0 owns one versioned Core/proposal/import/WASI manifest and VM-4 only consumes it. Reject everything else deterministically. A proposal or WASI capability enters only through a new manifest/ADR with owner, tests, resources, security, AOT evidence, and maintenance budget. |
| WAST conformance tooling leaks into a product closure | Keep the runner/converter, text parser, `spectest` host, and generated-test metadata in test-only projects; audit product and Native AOT dependency closures at VM-0, VM-4, and release. |
| Static registration silently stops being extensible | Prove an application-local consumer profile through the public source contract, governed IDs, catalog/manifest drift tests, and direct composition roots. Do not replace compile-time extensibility with reflection or imply a binary plug-in ABI. |
| Trimming removes a profile or host path | Directly root factories/capabilities and publish/run JS-only, Wasm-only, aggregate, and runtime-compiler samples. A linker annotation without execution is insufficient. |
| Aggregate package size becomes mandatory | Keep core/profile/aggregate boundaries explicit and add single-profile compositions only where measured package evidence justifies them. |
| Independent profile releases become unnecessarily coupled | Close JavaScript-only and Wasm-only VM-6/VM-8 records independently. Require both only for the aggregate package and claims that actually exercise both profiles. |
| Malicious bytecode exhausts verifier or runtime resources | Checked/bounded readers, pre-execution verification, fuel/cancellation, memory/table/frame/import budgets, fuzzing, and stable resource failure results are release gates. |
| An artifact weakens host policy by declaring larger limits | Treat the host ceiling as authoritative, allow the profile to tighten it, and allow the artifact only to request less. Compute the intersection before allocation and record the effective policy in the verified handle. |
| An approved profile capability does not fit the frozen core contract | Amend it: mint the next core contract version, state what changed, and recertify the affected evidence. Do not add a JavaScript-only or Wasm-only path to the core state machine, and do not maintain a second core contract per profile. |
| Guest-initiated loading becomes an unverified or unbounded back door | Route every acquired byte through ordinary verification, reach the host only through a declared artifact-provider capability, charge nested work to the requesting operation, and bound depth, fan-out, and cumulative bytes. A composition with no provider refuses deterministically. |
| A runtime source compiler is reached from inside a profile | Keep the compiler behind the artifact-provider capability so it stays inside the declared composition and Native AOT closure. An execution-only product registers no provider and therefore has no compile path. |
| Concurrent runtimes multiply a host ceiling | Meter fuel, wall-clock, allocation, and live-runtime counts against a shared aggregate budget as well as each runtime, and refuse creation and resumption once the parent allowance is spent. Several Worker-backed JavaScript runtimes are the known case. |
| External pause becomes an unbounded or privileged side channel | Declare who may request external suspension, keep it distinct from guest suspension and from terminal cancellation, bound how long a paused operation may block disposal, and leave what a paused profile exposes to the profile. |
| Caller-owned bytes change after verification | Snapshot or fully decode into an immutable profile-bound verified handle and execute only that handle. Mutation, disposal, and concurrent overwrite tests are release blockers. |
| Mutable feedback or caches leak across runtimes | Keep canonical artifacts immutable and mutable state owner-scoped; require disposal/plateau/concurrency tests before sharing anything. |
| A common host registry becomes arbitrary CLR interop | Typed allowlisted capabilities only; no member enumeration or name-based CLR binding in any profile. |
| One host is mistaken for JS/Wasm interoperability | Keep profiles isolated. Fund a bridge only after a separate value/lifetime/exception/security ADR and conformance plan. |
| Outer-envelope and profile-payload persistence responsibilities overlap | Core owns bounded dispatch, byte ownership, atomic storage, and corruption recovery; each profile owns its payload/cache identity, compiler/debug compatibility, migration, invalidation, and fallback. A checksum is not content authorization. |
| Internal formats become accidental public contracts | Version from the first byte, but promise persistence only after its explicit Phase 8/VM-7 gate. Reject unsupported versions deterministically. |
| VM work is justified as an unmeasured IL speed-up | Capability/correctness comes first. On dynamic-code-capable hosts, use accepted per-profile candidate/control evidence before any performance claim. |

Stop or re-scope a milestone when its graph is cyclic, a dynamic-code-prohibited closure reaches
Emit, runtime discovery, or test-only WAST tooling, a verifier cannot produce an immutable bounded
representation before execution, trusted policy can be weakened by artifact input, a second core
state machine is maintained for one language, the independent conformance oracle cannot be
maintained, the declared Native AOT composition cannot publish and run, or the named
ownership/maintenance ceiling is absent. A difficult or slow milestone is not itself a stop
condition; an untruthful support claim is.

---

## 12. Platform and specification references

VM-0 records immutable revisions for implementation and release evidence; these moving links
are discovery entry points, not substitutes for the pinned manifests:

- [.NET Native AOT deployment and limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [.NET Native AOT warning guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/fixing-warnings)
- [WebAssembly Core Specification](https://webassembly.github.io/spec/core/)
- [WebAssembly core specification tests and WAST runner](https://github.com/WebAssembly/spec/tree/main/test/core)
- [WebAssembly reference interpreter and WAST test infrastructure](https://github.com/WebAssembly/spec/blob/main/interpreter/README.md)
- [WASI Preview 1 / WASI 0.1](https://wasi.dev/releases/wasi-p1)
- [WASI 0.2 and its Component Model foundation](https://wasi.dev/releases/wasi-p2)

## 13. Existing Broiler.JS plan crosswalk

This roadmap is the component and multi-profile owner; current component evidence is kept in the
[Broiler.VM status ledger](roadmap.status.md). The linked Broiler.JS documents remain the detailed
JavaScript-profile work plans and evidence ledgers:

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
