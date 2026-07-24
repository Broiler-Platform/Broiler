# Broiler.UI Human Review

**Status:** PENDING

Broiler.UI contains a platform-neutral retained UI runtime, standard control
implementations, RichEdit, Formatting Codes view, integrations, tests, and
samples. No attributable human approval of a current revision is recorded here.
This file must remain `PENDING` until a human reviewer selects a decision and
signs a review for an exact commit.

## Required review scope

- Core ADRs and the dependency/topology architecture tests.
- Input routing, focus/capture/modality, reentrancy, and teardown.
- Text editing, IME, clipboard, password/privacy, RichEdit, and Formatting Codes
  structured editing.
- Accessibility semantics and host bridges, including keyboard-only and screen
  reader evidence.
- Theme/contrast, text scaling, reduced motion, RTL/bidi, DPI, and control-state
  behavior.
- Rendering/resource ownership and denial-of-service risks from large or
  adversarial trees/documents.
- Package metadata, public API/XML documentation, dependencies, licenses,
  static analysis, and vulnerability scanning.
- Windows and Linux sample-host boundaries, especially remaining legacy Graphics
  input adapters.

The current residual work and intended evidence are listed in
[docs/roadmap.md](docs/roadmap.md).

## Human decision

The reviewer must identify the commit, commands/evidence, limitations, decision,
name, and date. AI tools may assemble evidence, but must not choose the decision,
sign the attestation, or change `PENDING` to an approval.
