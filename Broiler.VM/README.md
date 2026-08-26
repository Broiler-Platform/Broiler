# Broiler.VM

Broiler.VM is a new, planned NativeAOT-compatible component that executes verified bytecode
artifacts. It is a **host for language profiles, not a language**: it owns profile selection,
bounded loading, the verification boundary, the execution lifecycle, resource authority,
diagnostics, and composition evidence, and it owns no opcode set, value representation, or
language semantics of its own.

Profiles are separate components that reference the core; the core never references a profile.
**JavaScript** and **WebAssembly** are the two intended first profiles, as
`Broiler.VM.Profile.JavaScript` and `Broiler.VM.Profile.WebAssembly`. Neither is planned by the
core roadmap, and no core milestone depends on either existing.

A profile is added by compiling it into a product and registering its descriptor directly.
Broiler.VM does not discover plug-ins by scanning assemblies, loading types by name, or using a
runtime extension directory, and it offers no binary plug-in ABI. That is part of the Native AOT
contract: every executable profile and host capability must be rooted by a direct, typed
reference.

## Status

This directory contains the component plan, not an implementation.
[The status ledger](docs/roadmap.status.md) is the authority for accepted evidence and records
VM-0 through VM-6 as not started. No capability should be inferred from planning text alone.

## Component boundary

Broiler.VM owns profile selection, bounded artifact loading, the immutable verified-artifact
boundary, the common execution lifecycle, trusted resource-limit precedence, cancellation,
diagnostics, profile-neutral operation-result envelopes, the static profile catalog, the bounded
binary-reading primitives every profile needs, and the numbered core contract version that carries
them all. Bounded mediation of guest-initiated loads and of external suspension belongs to the
core; the language meaning of either belongs to the profile.

A profile owns its format, verifier, value/frame model, control flow, semantics, typed
result/fault payloads, imports, oracle, and conformance suite. The core imposes no opcode set, no
value ABI, and no language-specific result cases.

Redundancy between profiles is avoided by sharing **mechanism** — how bytes are read safely, how a
budget is charged — and never **semantics**. Values, frames, opcodes, and syntax trees are not
shared, and a new shared component is opened only through the extraction gate in
[section 8 of the roadmap](docs/roadmap.md).

## Relationship to Broiler.JS

`Broiler.JS` is a **legacy component** with its own roadmap, ledger, and consumers. Broiler.VM does
not depend on it, wrap it, or replace it on any schedule stated here, and no core gate may cite its
results as evidence. The JavaScript profile is expected to begin from a snapshot **copy** of that
component taken after its in-flight fix programme lands — a fork used as a base, with no dependency
edge in either direction. The conditions on that copy are recorded in
[section 9 of the roadmap](docs/roadmap.md).

## Roadmap

The architecture, milestones, evidence requirements, test matrix, release gates, and risks are in
[the Broiler.VM roadmap](docs/roadmap.md); current evidence is tracked separately in
[the authoritative status ledger](docs/roadmap.status.md).
