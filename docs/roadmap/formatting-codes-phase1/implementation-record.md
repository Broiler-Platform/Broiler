# Formatting Codes Phase 1 implementation record

**Completed:** 2026-07-12  
**Roadmap:** [`../formatting-codes-pane-roadmap.md`](../formatting-codes-pane-roadmap.md)

Phase 1 delivers the platform-neutral, output-only grammar-version 1 projector.
No UI control, Writer binding, raw-source parser, or document mutation is part of
this phase.

## Delivered component

`Broiler.Documents.FormatCodes` references only `Broiler.Documents.Model` at the
project boundary and has no package, UI, DOM, input, codec, clipboard, network,
file, or platform dependency.

Public contracts include:

- `FormatCodeProjector` and immutable `FormatCodeProjection`;
- typed canonical tokens, pending tokens, diagnostics, and edit-capability
  metadata;
- document-to-projection and projection-to-document caret mappings with explicit
  boundary affinity;
- configurable output, token, and quoted-value limits plus cancellation;
- a separate pending caret-formatting overlay excluded from canonical text; and
- `FormatCodeProjectionPolicy`, which exposes the measured host recommendation
  for background projection.

The model grants only `Broiler.Documents.FormatCodes` access to its existing
opaque position representation through `InternalsVisibleTo`; raw indexes remain
non-public.

## Frozen grammar

[`GRAMMAR.md`](../../../Broiler.Documents/Broiler.Documents.FormatCodes/GRAMMAR.md)
freezes command spelling, transition order, invariant numbers, `#RRGGBBAA`
colors, paragraph/structure codes, quoted values, content escaping, diagnostics,
mapping behavior, and pending-overlay semantics.

The signed example is covered by an exact golden test:

```text
[Bold ON]Hello World![Bold OFF]
```

Every `InlineStyle` and `ParagraphStyle` field has canonical coverage. Literal
brackets and backslashes, tabs, soft breaks, controls, format characters,
unpaired surrogates, Unicode combining text, emoji, RTL, and CJK content are
covered without confusing content and commands.

## Verification

Release verification on .NET SDK 10.0.301 / runtime 10.0.9:

| Scope | Result |
|---|---:|
| `Broiler.Documents.FormatCodes.Tests` | 29 passed, 0 failed |
| Complete `Broiler.Documents.slnx` test suite | 256 passed, 0 failed |
| RichEdit abstraction tests | 76 passed, 0 failed |
| RichEdit Standard tests | 59 passed, 0 failed |
| RichEdit RTF integration tests | 6 passed, 0 failed |
| Total verified tests | **397 passed, 0 failed** |
| Desktop Writer | Release build passed, 0 warnings |
| WebAssembly Writer | Release build passed, 0 warnings |

The focused suite includes exact goldens, deterministic randomized style
properties, normalized-document equivalence, every-offset mapping, reverse
boundary affinity, escaping, invalid-state diagnostics, pending overlays,
resource limits, cancellation, dependency guards, and all six full-scale Phase
0 fixtures.

## Benchmark and threshold

The repeatable runner and instructions are under
[`tests/formatting-codes-phase1`](../../../tests/formatting-codes-phase1/README.md).
The captured result is
[`benchmark-baseline.windows-x64.json`](benchmark-baseline.windows-x64.json).

On the capture machine, median full rebuilds were approximately 0.89 ms for
100K plain characters, 9.96 ms for 1M plain characters, and 18.76 ms for the
100K/10,000-run stress fixture. The high-density fixture expanded to 567,101
projected characters and 39,999 tokens.

The frozen conservative Phase 2 host rule is background projection above either:

- 100,000 source UTF-16 characters; or
- 10,000 combined paragraph/run structural units.

This policy accounts for future token layout and stale-result handling, not only
the headless projector's CPU time. Timings remain machine-specific.

## Phase 2 entry condition

Phase 2 may consume typed tokens directly to build the read-only
`UiFormatCodeView`/`StandardFormatCodeView`. It must not parse bracket text for
interaction, include pending overlay text in canonical copy, or publish a
background result for a stale document snapshot.
