# Broiler Code Phase 0

The harnesses, fixture, traces, and recorded baselines behind
[Phase 0 of the Broiler Code roadmap](../../docs/broiler-code-roadmap.md#phase-0---freeze-the-product-and-prove-the-risky-seams).

Phase 0's job is to prove the risky seams before anything is built on them. Each
harness here answers one question with evidence rather than with a design
argument, and each exits non-zero when its answer stops holding.

| Directory | What it is |
| --- | --- |
| [`fixture`](fixture/) | The `SampleReports` workspace: a stand-in for a user solution, with two projects, Android and browser-WebAssembly heads, and a deliberate cross-project error |
| [`Broiler.Code.Phase0.Prototype`](Broiler.Code.Phase0.Prototype/) | Immutable snapshots, the portable C# classifier, incremental classification, and the generation/snapshot controller |
| [`Broiler.Code.Phase0.Worker`](Broiler.Code.Phase0.Worker/) | The trusted out-of-process build worker and its six proof scenarios |
| [`Broiler.Code.Phase0.Roslyn`](Broiler.Code.Phase0.Roslyn/) | Roslyn footprint, and whether an evaluated graph can reach a local semantic service |
| [`payload-probes`](payload-probes/) | Two browser-WebAssembly publishes whose only difference is whether Roslyn is composed |
| [`traces`](traces/) | The named edit/analysis traces the budgets are stated against |
| [`baselines`](baselines/) | Recorded output from the machine named in the budgets document |

Decisions live in
[`docs/architecture/broiler-code.md`](../../docs/architecture/broiler-code.md);
numbers live in
[`docs/architecture/broiler-code-budgets.md`](../../docs/architecture/broiler-code-budgets.md).

## Running

```bash
dotnet run --project tests/broiler-code-phase0/Broiler.Code.Phase0.Prototype -c Release -- --iterations 9
```

Validates 17 correctness properties, then replays every trace. It fails if any
check fails, if a trace ends disagreeing with a full classification, or if any
trace ever painted a snapshot the buffer had already replaced.

```bash
dotnet run --project tests/broiler-code-phase0/Broiler.Code.Phase0.Worker -c Release
```

Runs six scenarios against the fixture. It fails if any of them stops holding.
Needs a .NET SDK satisfying the fixture's `global.json`.

```bash
dotnet run --project tests/broiler-code-phase0/Broiler.Code.Phase0.Roslyn -c Release -- --iterations 5
```

Measures the semantic service and fails if it stops agreeing with the
authoritative build about the fixture's error.

```bash
pwsh tests/broiler-code-phase0/payload-probes/measure-payloads.ps1
```

Publishes both probes and reports the payload difference. Needs the
`wasm-tools` workload.

## What the fixture is not in

The fixture is deliberately **not** registered in
[`eng/solutions.json`](../../eng/solutions.json). It fails to compile by design,
so a generated solution closure containing it would break build verification.
The three harnesses are registered, as `Broiler.Code.Phase0.slnx`; the two
payload probes are registered in `Broiler.WebAssembly.Tests.slnx` alongside the
existing browser-WASM baselines.

## Fixtures are generated, not committed

Large source blobs are generated in memory from a committed generator, following
the convention set by
[`tests/formatting-codes-phase0`](../formatting-codes-phase0/). The reviewable
artifacts are the generator, the traces, and the recorded counts — never a
checked-in ten-megabyte file.

Timing figures in `baselines/` are specific to the machine named in the budgets
document. The machine-independent figures — lines relexed, span counts, payload
bytes, reference counts, and every pass/fail — are the ones a test may assert
anywhere.

## What Phase 0 found

Short version, with the numbers in the budgets document:

- **The buffer, not the classifier, is the Phase 1 problem.** A whole-string
  snapshot costs 17.63 ms and 21.4 MB per keystroke on a 10.5 MB document;
  analysing the result costs 0.16 ms.
- **Over half the cost of putting Roslyn in a browser is localized message
  text** — 6.14 MiB of 10.78 MiB raw, in 26 satellite assemblies — and
  diagnostic identity never depends on message text.
- **A local semantic service fed the evaluated graph agrees exactly with the
  authoritative build**, down to the line and column.
- **The compile item list is part of the evaluated graph.** Deriving it by
  scanning directories produced 17 errors where the real build reports 1; the
  Roslyn harness hit that before being corrected to read `Compile` items.
- **Structured diagnostics are locale-independent and console text is not.**
  The recording machine emits German; the same build under English and Japanese
  produces byte-identical structured diagnostics.
- **SARIF shows diagnostics the console never prints** — the fixture's two
  information-severity analyzer diagnostics appear at no default verbosity.
