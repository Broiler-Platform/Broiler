# Broiler.VM

Broiler.VM is the planned NativeAOT-compatible bytecode execution component for Broiler.
It hosts statically linked bytecode-language profiles behind one bounded execution lifecycle.
The required initial built-in profiles are:

- **JavaScript**, executing versioned Broiler JavaScript bytecode produced by the approved
  Broiler.JS compiler composition; and
- **WebAssembly**, validating and executing binary WebAssembly modules for an explicitly
  versioned feature manifest.

Additional Broiler-provided or application-local built-in profiles may be added through the
source-level composition contract and compiled into a product. That public contract is proved in
VM-5 and frozen for compatibility in VM-8; it is not a binary plug-in ABI. Broiler.VM does not
discover plug-ins by scanning assemblies, loading types by name, or using a runtime extension
directory. That distinction is part of the Native AOT contract: every executable profile and host
capability must be rooted by a direct, typed reference.

## Status

This directory currently contains the component plan, not a completed VM implementation.
The numeric `Broiler.JavaScript.Portable` interpreter remains useful seed evidence, but it is
not a general JavaScript profile and proves nothing about WebAssembly execution. Likewise,
the repository's browser-WebAssembly applications are deployment consumers, not evidence of
a WebAssembly bytecode interpreter.

Implementation, package names, and public APIs remain subject to the graph and ownership gate in
[the roadmap](docs/roadmap.md). [The status ledger](docs/roadmap.status.md) is the authority for
accepted evidence and currently records VM-0 through VM-8 as not started. No profile or conformance
claim should be inferred from planning text alone.

## Component boundary

Broiler.VM owns profile selection, bounded artifact loading, the immutable verified-artifact
boundary, the common execution lifecycle, trusted resource-limit precedence, cancellation,
diagnostics, profile-neutral operation-result envelopes, the static profile catalog, and the
numbered core contract version that carries them. Bounded mediation of guest-initiated loads and
of external suspension belongs to the core; the language meaning of either belongs to the
profile. A profile
owns its own format, verifier, value/frame model, control flow, semantics, typed result/fault
payloads, imports, and conformance suite. The core does not impose one opcode set, one value ABI, or
language-specific result cases on JavaScript and WebAssembly.

Source-language tooling remains outside the execution core:

- Broiler.JS owns JavaScript parsing, shared semantic analysis, and JavaScript-bytecode
  lowering. Runtime source compilation is a separate, explicitly selected deployment
  composition.
- The WebAssembly profile consumes binary Core modules and initially claims no WASI support. A
  product WAT/WAST parser or source-language compiler is not implied. The official WAST corpus is
  handled by isolated test tooling that must remain outside shipped and Native AOT closures.
- Sharing one VM host does not imply JavaScript-to-WebAssembly interoperation. Any bridge is
  a separately designed and tested host capability.

## Roadmap and related plans

The complete architecture, milestones, evidence requirements, test matrix, release gates, and
risks are in [the Broiler.VM roadmap](docs/roadmap.md); current evidence is tracked separately in
[the authoritative status ledger](docs/roadmap.status.md).

The JavaScript profile deliberately reuses, rather than duplicates, the existing Broiler.JS
bytecode work:

- [Phase 6 — correct bytecode interpreter](../Broiler.JS/docs/roadmap/Phase-6.md)
- [Phase 6 status and seed evidence](../Broiler.JS/docs/roadmap/Phase-6.status.md)
- [Phase 7 — shippable interpreter](../Broiler.JS/docs/roadmap/Phase-7.md)
- [Phase 8 — profile-led optimization and persistence](../Broiler.JS/docs/roadmap/Phase-8.md)
- [Phase 9 — optional JavaScript IL/bytecode adaptivity](../Broiler.JS/docs/roadmap/Phase-9.md)
