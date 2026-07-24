# Broiler.Documents ADR Index

ADRs 0001-0005 define the document-format component, model ownership, codec
contract, safety policy, and first RTF subset. The model-placement decision is
mirrored on the UI side by
`Broiler.UI/docs/adr/0018-richedit-document-model-promotion.md`. ADR 0006 freezes
the model-side Formatting Codes projection, grammar, mapping, and edit scope.
Accepted and partially superseded records remain here for traceability; current
follow-up work is in [the component roadmap](../roadmap.md).

| ADR | Topic |
|---|---|
| [0001](0001-component-topology-and-consumption-policy.md) | Component topology and consumption policy |
| [0002](0002-document-model-ownership-and-promotion.md) | Document model ownership and promotion (Path A) |
| [0003](0003-codec-contract-and-signature-probe.md) | Codec contract and signature probe |
| [0004](0004-document-read-limits-and-rtf-sanitization.md) | Document read limits and RTF sanitization policy |
| [0005](0005-rtf-first-release-subset-and-text-encoding.md) | RTF first-release subset and text encoding |
| [0006](0006-formatting-codes-projection-and-grammar.md) | Formatting Codes projection and grammar |
