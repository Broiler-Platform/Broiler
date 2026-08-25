# Broiler.VM

Broiler.VM is the planned NativeAOT-compatible bytecode execution component for Broiler.
It hosts statically linked bytecode-language profiles behind one bounded execution lifecycle.
The required initial built-in profiles are:

- **JavaScript**, executing versioned Broiler JavaScript bytecode produced by the approved
  Broiler.JS compiler composition; and
- **WebAssembly**, validating and executing binary WebAssembly modules for an explicitly
  versioned feature manifest.

Additional built-in profiles may be added in source and compiled into a product. Broiler.VM
does not discover plug-ins by scanning assemblies, loading types by name, or using a runtime
extension directory. That distinction is part of the Native AOT contract: every executable
profile and host capability must be rooted by a direct, typed reference.

## Status

This directory currently contains the component plan, not a completed VM implementation.
The numeric `Broiler.JavaScript.Portable` interpreter remains useful seed evidence, but it is
not a general JavaScript profile and proves nothing about WebAssembly execution. Likewise,
the repository's browser-WebAssembly applications are deployment consumers, not evidence of
a WebAssembly bytecode interpreter.

Implementation, package names, and public APIs remain subject to the graph and ownership gate
in [the roadmap](docs/roadmap.md). No profile or conformance claim should be inferred before
its objective exit gate is recorded there and in the owning status document.

## Component boundary

Broiler.VM owns profile selection, bounded artifact loading, execution lifecycle, resource
limits, cancellation, diagnostics, and the static profile catalog. A profile owns its own
format, verifier, value/frame model, control flow, semantics, imports, and conformance suite.
The core does not impose one opcode set or one value ABI on JavaScript and WebAssembly.

Source-language tooling remains outside the execution core:

- Broiler.JS owns JavaScript parsing, shared semantic analysis, and JavaScript-bytecode
  lowering. Runtime source compilation is a separate, explicitly selected deployment
  composition.
- The WebAssembly profile consumes binary modules. A WAT parser or source-language compiler
  is not implied by the first execution profile.
- Sharing one VM host does not imply JavaScript-to-WebAssembly interoperation. Any bridge is
  a separately designed and tested host capability.

## Roadmap and related plans

The complete architecture, milestones, evidence requirements, test matrix, release gates,
and risks are in [the Broiler.VM roadmap](docs/roadmap.md).

The JavaScript profile deliberately reuses, rather than duplicates, the existing Broiler.JS
bytecode work:

- [Phase 6 — correct bytecode interpreter](../Broiler.JS/docs/roadmap/Phase-6.md)
- [Phase 6 status and seed evidence](../Broiler.JS/docs/roadmap/Phase-6.status.md)
- [Phase 7 — shippable interpreter](../Broiler.JS/docs/roadmap/Phase-7.md)
- [Phase 8 — profile-led optimization and persistence](../Broiler.JS/docs/roadmap/Phase-8.md)
- [Phase 9 — optional JavaScript IL/bytecode adaptivity](../Broiler.JS/docs/roadmap/Phase-9.md)
