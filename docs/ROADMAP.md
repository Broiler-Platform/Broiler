# Broiler root roadmap

- **Status:** Active preview
- **Scope:** Only unfinished work that crosses component or application boundaries
- **Last reconciled:** 2026-07-24

Component-local work is tracked in the roadmaps linked from
[the documentation index](README.md). This file does not repeat completed
extractions, phase logs, or per-test investigation journals.

## Release and distribution

### Publish a reproducible first preview

**Current evidence:** the repository has component package metadata, the
[`nuget-packages`](../.github/workflows/nuget-packages.yml) workflow, a
lockstep preview version, SourceLink/symbol-package configuration, and
commit-scoped human-review records.

**Next actions:**

1. Validate installation from an isolated package feed without relying on the
   aggregate checkout.
2. Ensure every release submodule pointer contains, or is paired with, every
   required pending patch recorded in [`patches/README.md`](../patches/README.md).
3. Complete exact-commit human, dependency, license, static-analysis, and
   vulnerability review for the release graph.
4. Configure the protected release environment and publishing credentials.
5. Run the tag path and install the resulting packages and applications on clean
   supported hosts.

**Exit gate:** the exact reviewed commit produces deterministic packages and
symbols, installs from the advertised feeds on supported platforms, passes the
published smoke suite, and can be reproduced without uncommitted submodule
changes.

### Deliver installation and update paths

**Current evidence:** the repository builds portable Windows and Linux
applications, but the proposed native installation and self-update system is not
a completed product surface.

**Next actions:**

- Freeze product identifiers, release channels, artifact names, signed manifest
  format, update ownership, rollback behavior, and key-rotation policy.
- Ship deterministic signed portable releases before adding an in-app updater.
- Add an atomic per-user portable install/update transaction with recovery and
  uninstall behavior.
- Add Windows and Linux native delivery only after the portable transaction and
  release-signing gates pass.
- Keep macOS delivery gated on native application, graphics, input, signing, and
  notarization support.

**Exit gate:** each claimed platform has one documented update owner, verified
signatures and hashes, rollback after interrupted activation, clean repair and
uninstall paths, and end-to-end release tests.

## Standards and test infrastructure

### Publish a reproducible Chromium baseline

**Current evidence:** Chromium 148 is captured in
[`chromium-148.lock.json`](../tests/m2-conformance/chromium-reference/chromium-148.lock.json),
but its WPT revision is unresolved and there is no published root snapshot or
alignment workflow.

**Next actions:**

1. Resolve and record exact WPT and Test262 revisions.
2. Pin the WPT and Octane workflows to reviewed commit SHAs instead of floating
   `master`.
3. Publish one generated snapshot that links the lock, focused JS/HTML/bridge
   results, visual references, and performance results.
4. Automate the refresh without making unrelated checkouts download the full WPT
   corpus.

**Exit gate:** a clean checkout can reproduce the same corpus and focused
results from immutable revisions, and an upstream refresh is a reviewable diff.

### Re-establish current Acid, Google, and WPT evidence

**Current evidence:** focused regression tests exist and historical campaigns
landed many fixes, but checked implementation tasks and a 100/100 Acid script
score do not establish current pixel fidelity or broad standards conformance.
The current WPT artifacts remain the evidence source:
[`HTML results`](../tests/html/wpt-results/) and
[`CSS results`](../tests/css/wpt-results/).

**Next actions:**

- Capture fresh Acid1/Acid2/Acid3 viewport references and report script score,
  geometry, content, and pixel metrics separately.
- Add a local HTTP fixture for the remaining Acid3 status/content-type cases.
- Re-run the Google comparison against a recorded input and Chromium revision;
  record actual milestone measurements rather than inferring compliance from API
  presence.
- Keep prioritized WPT failures in generated reports and component roadmaps.
  Root tracking should cover only cross-component runner, timeout, reference, and
  ownership problems.
- Prototype per-component stress attribution with a small Broiler.JS slice before
  investing in full coverage-guided selection.

**Exit gate:** every published claim names its corpus revision, environment,
metric, tolerances, skips, and reproducible command; regressions are assigned to
one owning component.

## Browser WebAssembly

The durable ownership and rendering decisions are in
[the browser WebAssembly architecture](architecture/browser-webassembly.md).
Phases 0–5 have local implementation and smoke evidence; they are not yet a
broad browser-support claim.

**Next actions:**

- Restore the phase baseline verifier's composition root by registering the
  managed image-codec catalog before PNG generation, then keep verification of
  the relocated fixture as an executable gate.
- Add committed Chromium and Firefox CI for the phase smoke suites and published
  application.
- Record frame time, input-to-present latency, memory, resize retention, payload,
  and ten-minute soak evidence for interpreted, trimmed, and supported AOT modes.
- Run real IME, trusted clipboard, keyboard-only, RTL, and screen-reader checks;
  publish the exact supported combinations.
- Finish and evidence the Writer WebAssembly workflow, browser resource
  open/save, and failure/permission UX.
- Package the browser application with immutable assets, integrity/cache policy,
  diagnostics, and an explicit support statement.
- Treat a full Broiler browser-engine port as a separate opt-in decision; it is
  not required for the Writer application preview.

**Exit gate:** the published Writer workflow passes the supported browser matrix,
performance and accessibility gates, handles capability denial honestly, and is
reproducible from CI artifacts.

## HtmlBridge runtime

The current assembly and ownership boundaries are in
[the HtmlBridge architecture](architecture/htmlbridge.md).

**Next actions:**

- Replace the remaining fixed script-phase buckets with one ordered task model
  that can interleave deferred/module tasks, timers, animation frames, and
  microtask checkpoints with explicit thread affinity.
- Replace the temporary process-static layout-view factory with composition-root
  or session injection so simultaneous hosts cannot overwrite configuration.
- Enable the native CSS `zoom` used-value route at every layout consumer,
  including the external renderer used by `CaptureService`, before deleting the
  serialization carry-through.
- Complete externally gated pinch-zoom and `::backdrop` browser/pixel corpus
  evidence before retiring their compatibility levers.
- Remove vestigial cutover flags only after their rollback and differential tests
  have been replaced by permanent invariant tests.

**Exit gate:** all execution surfaces use the same ordered scheduling model,
session dependencies are instance-scoped, native zoom/top-layer behavior is
enabled for every supported consumer, and focused plus broad regression gates
remain green.

## Linux application preview

Graphics, input, layout, media, and UI details belong to their component
roadmaps. Root work is limited to application composition and release evidence.

**Next actions:**

- Complete the supported graphics presentation path, input ownership migration,
  resize/device-loss handling, and deterministic fallback behavior.
- Run Browser and Writer smoke suites on the declared distro/driver matrix,
  including software rendering and permission-denied input cases.
- Record package dependencies, evdev permissions, diagnostics, accessibility
  limitations, and hardware evidence.

**Exit gate:** Browser and Writer install and run on the declared Linux matrix,
produce comparable artifacts, shut down without leaked native resources, and
publish an evidence-based preview support statement.

## PDF conversion decision

`Broiler.Cli --convert-pdf` describes an external `Broiler.Pdf` application, but
no `src/Broiler.Pdf` project exists in the current checkout. Do not continue an
old parser milestone as though that baseline were present.

**Next action:** choose one of the following and record an owner:

- restore/scaffold the standalone application and re-baseline its corpus,
  dependencies, CLI compatibility, security limits, and M1 entry gate; or
- remove the unavailable source-project fallback and narrow the CLI/documentation
  claim to an explicitly external tool.

**Exit gate:** the advertised CLI behavior resolves to a shipped, tested tool, or
fails with documentation that exactly matches the supported configuration.

## Maintenance policy

- Completed implementation records are removed after durable decisions and open
  work have moved to their owners.
- Pending submodule changes are tracked in
  [`patches/README.md`](../patches/README.md), not duplicated in incident
  roadmaps.
- New work enters this file only when at least two components or a root
  application/release workflow must coordinate to close it.
