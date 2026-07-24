# Broiler.UI ADR Index

These records define the core UI architecture, RichEdit family, document-model
promotion, filesystem topology, and Formatting Codes view. ADR 0018 supersedes
the model *placement* in ADRs 0013 and 0014; their remaining design decisions
still apply. Accepted and partially superseded records are retained for
traceability. Current follow-up work is in
[the component roadmap](../roadmap.md).

| ADR | Topic |
|---|---|
| [0001](0001-ui-root-and-per-type-assembly-rule.md) | UI root and per-type assembly rule |
| [0002](0002-logical-versus-native-windows.md) | Logical versus native windows |
| [0003](0003-graphics-submission-boundary.md) | Graphics submission boundary |
| [0004](0004-input-and-text-service-boundary.md) | Input and text-service boundary |
| [0005](0005-ui-context-and-reentrancy.md) | UI context and reentrancy |
| [0006](0006-layout-protocol.md) | Layout protocol |
| [0007](0007-implementation-factories.md) | Implementation factories |
| [0008](0008-accessibility-semantic-bridge.md) | Accessibility semantic bridge |
| [0009](0009-edit-text-model.md) | Edit text model |
| [0010](0010-theme-and-visual-state-model.md) | Theme and visual state model |
| [0011](0011-compatibility-removal.md) | Compatibility removal |
| [0012](0012-package-repository-topology.md) | Package/repository topology |
| [0013](0013-richedit-assembly-boundaries-and-dom-adapter.md) | RichEdit assembly boundaries and DOM adapter policy |
| [0014](0014-rich-text-document-model.md) | Rich text document model |
| [0015](0015-formatting-command-model.md) | Formatting command model |
| [0016](0016-rich-clipboard-and-html-sanitization.md) | Rich clipboard and HTML sanitization |
| [0017](0017-richedit-accessibility-semantics.md) | RichEdit accessibility semantics |
| [0018](0018-richedit-document-model-promotion.md) | RichEdit document model promotion (to `Broiler.Documents.Model`) |
| [0019](0019-directory-structure-topology.md) | Directory structure topology |
| [0020](0020-formatting-code-view-and-writer-integration.md) | Formatting Code view and Writer integration |
