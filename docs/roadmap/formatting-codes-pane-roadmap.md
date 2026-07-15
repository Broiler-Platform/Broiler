# Broiler Writer Formatting Codes Pane Roadmap

> **Status:** Phase 4 complete; Phase 5 is optional and requires an explicit go/no-go review
> **Last updated:** 2026-07-15
> **Scope:** An independently designed bottom pane inspired by the WordPerfect®
> word processor that presents Broiler's rich-text state as simple text tokens such as
> `[Bold ON]Hello World![Bold OFF]`.
> **Legal note:** The IP section is a product-risk assessment, not a legal
> opinion or freedom-to-operate opinion.

## 1. Executive decision

Proceed with an independently implemented feature under the user-facing name
**Formatting Codes** (alternatives: **Format Trace**, **Formatting Inspector**,
or **Markup Stream**), not **Reveal Codes**.

The recommended product sequence is:

1. Ship a deterministic, read-only projection of the current Broiler document.
2. Add two-way caret and selection navigation.
3. Add safe, token-aware formatting operations that participate in the existing
   undo history.
4. Consider unrestricted textual source editing only after a separate model and
   usability gate.

The core functional idea appears low risk to implement clean-room. The split
view in WordPerfect was publicly described as existing prior art in a patent filed in
1992, the closest located patents are expired, and Corel's own current patent
marking page does not identify a Reveal Codes patent. That does not prove that no
active implementation patent exists anywhere, so a claims-based search in the
actual launch countries remains a commercial-release gate.

The most important technical constraint is equally clear: Broiler stores
normalized, fully resolved style runs, not literal embedded control-code nodes.
The pane must therefore be a **canonical projection of the current Broiler
model**, not a claim that it reproduces the exact control stream from an imported
RTF, DOCX, HTML, Markdown, or WordPerfect file.

### Recommended delivery boundary

| Release | Capability | Recommendation |
|---|---|---|
| Preview | Read-only text plus formatting tokens | Commit to this |
| Beta | Synchronized caret, selection, copy, and code inspection | Commit to this |
| v1 | Structured operations: clear/edit formatting, edit text, insert formatting through commands | Commit after preview feedback |
| Advanced | Free typing/deleting/moving of raw code text | Optional; requires a separate go/no-go decision |

## 2. IP and reimplementation assessment

### 2.1 Practical conclusion

The recommended implementation is reasonable to pursue if Broiler:

- writes all code independently against its own document model;
- uses an original feature name, palette, typography, token grammar, icons,
  shortcut, and interactions;
- does not copy Corel/Alludo code, binaries, assets, screenshots, manual prose,
  or the complete WordPerfect code chart;
- does not market the feature as the **Reveal Codes** feature from WordPerfect or imply an
  affiliation; and
- obtains a targeted patent, trademark, and design clearance before a material
  commercial launch.

Risk rating for planning purposes:

| Area | Planning risk | Reason / action |
|---|---:|---|
| Synchronized lower text view of formatting state | Low | Decades-old concept and documented prior art; implement independently |
| ASCII labels such as `[Bold ON]` | Low | Short, functional vocabulary; use an original complete grammar |
| Product name **Reveal Codes** | Medium | Actively used as a feature identifier in WordPerfect; choose a different name |
| Pixel-level or interaction clone | Medium | GUI expression, trade dress, unfair-competition, and design rights can be separate from patents |
| Copying implementation, assets, screenshots, or documentation | High | Avoid entirely |

### 2.2 Patent findings checked on 2026-07-12

1. [US 5,345,551](https://patents.google.com/patent/US5345551A/en),
   *Method and system for synchronization of simultaneous displays of related
   data sources*, explicitly describes the formatted view in WordPerfect and
   synchronized control-code view as pre-existing technology. Its claims instead
   concern synchronization of different related sources through sections,
   messages, and separate window-controlling tasks. Google Patents reports it
   expired in 2012.
2. Corel's [official virtual patent-marking
   page](https://www.corel.com/en/patent/) lists five US patents for WordPerfect
   Office. None is titled or directed to a formatting-code pane:

   | Patent | Subject | Reported status |
   |---|---|---|
   | [US 6,225,996](https://patents.google.com/patent/US6225996B1/en) | Displaying a selected/off-screen spreadsheet cell value | Expired |
   | [US 6,317,758](https://patents.google.com/patent/US6317758B1/en) | Detecting/correcting spreadsheet cell-reference errors | Expired |
   | [US 6,731,309](https://patents.google.com/patent/US6731309B1/en) | Real-time preview of commands | Expired |
   | [US 6,959,422](https://patents.google.com/patent/US6959422B2/en) | Shortcut-key assignment management | Expired |
   | [US 7,827,483](https://patents.google.com/patent/US7827483B2/en) | Continuation concerning real-time preview | Expired in 2021 |

   Corel warns that the marking list may be incomplete and that other rights may
   be pending. The list is useful evidence, not a clearance opinion.
3. [US 7,043,689](https://patents.google.com/patent/US7043689B2/en),
   Adobe's *Managing zero-width markers*, is the closest later patent found. Its
   independent claims require a location-triggered tip window, a cursor inside
   the marker representation, and closing the tip when the document caret moves
   away. It cited WordPerfect and Microsoft's reveal-code material as prior art
   and expired in 2023.
4. [US 4,897,804](https://patents.google.com/patent/US4897804A/en), an older
   Brother patent with 1986 priority, covers an adjacent divided text/format
   display concept and is long expired.
5. The [Library of Congress WordPerfect format
   history](https://www.loc.gov/preservation/digital/formats/fdd/fdd000621.shtml)
   records ASCII document text with embedded formatting function codes in
   WordPerfect 4.x material from 1984-1986. This is further evidence that the
   underlying streaming-code idea is old.

US utility patents generally last 20 years from the relevant filing date,
subject to adjustments and other exceptions; European and German patents
generally have the same 20-year maximum. Patents are territorial. Age alone does
not eliminate a later improvement patent, and only the claims of a live patent
determine infringement.

### 2.3 Copyright, branding, and visual-design findings

- US copyright law excludes ideas, procedures, processes, systems, and methods
  of operation from copyright protection. It still protects original source
  code, documentation, graphics, and sufficiently original screen expression.
- EU Software Directive 2009/24/EC likewise protects program expression, not the
  ideas and principles underlying a program or its interfaces. A GUI can still
  receive separate protection when its expression is sufficiently original.
- WordPerfect is actively presented as a registered mark, and Corel/Alludo
  actively uses **Reveal Codes** as the name of this feature. A cursory search did
  not establish an exact registration for that phrase, but registration is not
  the only source of trademark or unfair-competition risk.
- A bottom pane, brackets, and ordinary formatting words are highly functional.
  Broiler should nevertheless avoid the exact colors and code-chip
  treatment used by WordPerfect, including its
  shapes, red block cursor, docking controls, icons, Alt+F3 shortcut, and overall
  visual composition.

### 2.4 Required commercial-release clearance

Before marketing a paid or widely distributed release, give counsel this
roadmap plus the finished interaction specification and request:

1. a claims-based FTO search in the US, EU/Germany, UK, and other actual launch
   markets, including continuations and active GUI/marker patents;
2. a live trademark search for the final public name;
3. a registered-design/design-patent search for any visual elements that ended
   up close to a current competitor UI; and
4. review of any comparative marketing copy.

Keep a short provenance record with the prior-art sources above, original design
mockups, grammar decisions, and commit history. If WordPerfect must be mentioned
in public compatibility documentation, use a factual attribution such as:

> WordPerfect is a registered trademark of Corel Corporation or Corel
> Corporation Limited. Broiler is not affiliated with or endorsed by Corel,
> Alludo, or their affiliates.

## 3. Product definition

### 3.1 Goal

Give users a transparent, searchable view of the state that actually drives
Broiler's document model. For the simplest case, the pane displays:

```text
[Bold ON]Hello World![Bold OFF]
```

The value is not the brackets themselves. The value is that a user can locate a
format boundary, understand why nearby text behaves differently, navigate to the
affected range, and safely correct it.

### 3.2 Terminology

- **Formatting Codes pane:** the user-visible lower pane.
- **Projection:** a deterministic token sequence derived from a
  `RichTextDocument` snapshot.
- **Code token:** textual representation of a style transition, paragraph
  property, or structural character.
- **Text token:** document content, escaped only where necessary to distinguish
  it from code syntax.
- **Source boundary:** an opaque `RichTextPosition` plus before/after affinity.
- **Canonical:** semantically equivalent Broiler documents produce the same
  projection, even if their source files used different control sequences.

### 3.3 In scope

- Bold, italic, underline, strikethrough, font family/size, foreground,
  highlight/background, and links.
- Alignment, line spacing, list kind, indent, and spacing before/after.
- Tabs, soft line breaks, paragraph breaks, empty paragraphs, and Unicode text.
- Show/hide, resizable bottom pane, wrapping option, and copy.
- Main-editor-to-pane and pane-to-main-editor caret/selection synchronization.
- Structured editing in a later phase, through the existing transaction model.
- Desktop Writer and WebAssembly Writer parity.

### 3.4 Non-goals

- Reproducing the UI of WordPerfect or its WPD binary control stream.
- Reconstructing source-format controls lost during RTF/DOCX/HTML/Markdown
  import.
- Preserving redundant codes that Broiler's normalized style-run model does not
  store.
- Turning the projection into Broiler's primary document file format.
- Exposing raw paragraph or UTF-16 indexes in public APIs.
- Shipping unrestricted raw-code editing in the initial release.
- Adding tables, images, headers/footers, fields, pagination codes, tracked
  changes, or unsupported document-model concepts merely for this pane.

## 4. Repository fit and current constraints

Broiler already has most of the hard document-state foundation:

| Existing capability | Location | Consequence for this feature |
|---|---|---|
| Immutable/copy-on-write document snapshots | `Broiler.Documents/Broiler.Documents.Model/RichTextDocument.cs` | Projection can safely key caches by snapshot and paragraph identity |
| Normalized maximal style runs | `Broiler.Documents/Broiler.Documents.Model/RichTextParagraph.cs`, `StyleRun.cs` | Project transitions between resolved states; do not invent stored code nodes |
| Opaque document positions/ranges | `Broiler.Documents/Broiler.Documents.Model/RichTextPosition.cs`, `RichTextRange.cs` | Keep indexing private; return opaque mappings from the projector |
| Transactional undo/redo | `Broiler.Documents/Broiler.Documents.Model/RichTextEditor.cs` | All pane edits must enter this history rather than replace the document directly |
| RichEdit events and commands | `Broiler.UI/src/Abstractions/Text/Broiler.UI.RichEdit/UiRichEdit.cs`, `RichEditCommand.cs` | Bind to `DocumentChanged`, `SelectionChanged`, and `CommandExecuted` |
| Desktop Writer layout | `Broiler.Writer/WriterApp.cs` | Insert pane between `_editor` and `_status` in `WriterContent` |
| Browser Writer mirror | `Broiler.Writer.WebAssembly/BrowserWriterDemo.cs` | Make the same integration and interaction changes |
| ScrollView and Label controls | `Broiler.UI/src/Abstractions/Layout/Broiler.UI.ScrollView`, `Broiler.UI/src/Abstractions/Content/Broiler.UI.Label` | Enough for a throwaway read-only prototype, not the durable interactive view |

Important constraints:

1. `RichTextPosition` internals are intentionally hidden. The projection
   assembly should receive `InternalsVisibleTo`, rather than making indexes
   public.
2. Assigning `UiRichEdit.Document` calls `RichTextEditor.LoadDocument`, which
   resets history. Pane edits must use model/control operations, not document
   assignment.
3. An empty-selection formatting command changes pending caret formatting
   without changing the document snapshot. The controller must also observe
   `CommandExecuted` and derive a transient pending-style overlay from
   `CaretInlineStyle`.
4. The current standard renderer records paragraph styles but does not visually
   implement alignment, list markers, indentation, or paragraph spacing. The
   pane may expose that model state, but the UI must not imply that unsupported
   rendering already works. Paragraph rendering should be completed in parallel
   or unsupported properties should carry an explanatory diagnostic.
5. Desktop and browser Writer shells duplicate much of their composition and
   palette code. Do not make a full shell refactor a feature blocker, but keep
   projection, view, and synchronization logic reusable so only small layout
   adapters are duplicated.

## 5. UX and interaction contract

### 5.1 Placement and chrome

- Add **View > Formatting Codes** as a checkable menu command.
- Use an original shortcut, provisionally `Ctrl+Shift+F3`; do not adopt the
  Alt+F3 shortcut used by WordPerfect as the default.
- Place the pane below the rich editor and above the status line.
- Default preview height: 144 logical pixels; minimum: 72; maximum: 45% of the
  content area.
- Provide a keyboard-operable splitter with a visible focus indicator.
- Remember visibility, height, wrap mode, and detail mode per user once Writer
  has a settings store; keep them session-local before then.
- Title the pane **Formatting Codes** and provide close, wrap, details, and
  follow-caret controls using Broiler's own icons and theme.

### 5.2 Visual language

- Use a monospaced font for both code and content so textual positions are
  predictable.
- Render code tokens with an original, low-contrast accent and code-token
  background; render document text with the normal pane foreground.
- The exported/copied value remains plain text. Coloring is presentation only.
- Do not use the red block cursor, exact token shapes, palette, or docking
  affordance used by WordPerfect.
- Respect light, dark, high-contrast, text-scale, and reduced-motion settings.

### 5.3 Navigation behavior

| Action | Result |
|---|---|
| Main editor caret moves | Pane scrolls nearest mapped text/code boundary into view; pane does not steal focus |
| Main editor selection changes | Corresponding projected text and boundary tokens highlight |
| User clicks projected text | Main editor selection becomes a caret at that source position |
| User clicks a code token | Main editor caret moves to its boundary; affected source range receives a secondary highlight |
| User double-clicks a code in structured-edit phase | Open the Broiler property editor for that token |
| `F6` / `Shift+F6` | Cycle focus between editor, codes pane, and other Writer regions |
| `Escape` in the pane | Return focus to the rich editor without hiding the pane |
| Copy in read-only pane | Copy canonical plain projection text; never copy hidden document data outside the selection |

The controller must use a reentrancy guard. A selection update originating in
the pane may update the main editor, whose event then refreshes the pane, but it
must not create an event loop or reverse the directional selection.

### 5.4 Read-only, structured, and raw modes

1. **Read-only mode (default):** navigation, selection, search, and copy.
2. **Structured-edit mode:** document text is editable; code tokens remain atomic.
   Context actions change or clear the mapped formatting through typed edit
   intents. Insert Code uses a palette, not free-typed brackets.
3. **Advanced source mode (optional):** edits occur in a draft projection. The
   main document updates only after the draft parses successfully and the user
   applies it as one transaction. Invalid syntax is retained in the draft with
   diagnostics and never partially corrupts the document.

## 6. Canonical text grammar

### 6.1 Grammar principles

- Command names and delimiters are printable ASCII; document content remains
  full Unicode and is never down-converted to ASCII.
- Keywords are serialized in invariant uppercase.
- Numeric values use invariant culture and the shortest round-trippable form.
- Colors serialize as `#RRGGBBAA` so alpha is unambiguous.
- Default document and paragraph state is implicit.
- Each paragraph starts from default paragraph and inline state for projection
  purposes. Non-default state is emitted deterministically.
- Inline state is closed before `[Paragraph Break]` and reopened after it. This
  makes every paragraph independently parseable and avoids formatting leakage.
- Adjacent identical resolved runs produce no redundant tokens.
- Imported source-code order and redundancy are intentionally not preserved.

The table below documents accepted canonical vocabulary. Default-valued
paragraph tokens are normally omitted, but remain valid parser reset tokens and
may appear in a future expanded-details mode.

### 6.2 First grammar

| Model concept | Canonical token(s) |
|---|---|
| Bold | `[Bold ON]`, `[Bold OFF]` |
| Italic | `[Italic ON]`, `[Italic OFF]` |
| Underline | `[Underline ON]`, `[Underline OFF]` |
| Strikethrough | `[Strike ON]`, `[Strike OFF]` |
| Font family | `[Font "Segoe UI"]`, `[Font DEFAULT]` |
| Font size | `[Size 17]`, `[Size DEFAULT]` |
| Foreground | `[Text Color #112233FF]`, `[Text Color DEFAULT]` |
| Highlight | `[Highlight #FFF2A8FF]`, `[Highlight NONE]` |
| Link | `[Link "https://example.test/"]`, `[Link OFF]` |
| Alignment | `[Align LEFT]`, `[Align CENTER]`, `[Align RIGHT]` |
| List | `[List NONE]`, `[List BULLET]`, `[List NUMBERED]` |
| Indent | `[Indent 2]` |
| Line spacing | `[Line Spacing 1.5]` |
| Paragraph spacing | `[Space Before 8]`, `[Space After 8]` |
| Tab | `[Tab]` |
| Soft line break (`U+2028`) | `[Line Break]` |
| Paragraph boundary | `[Paragraph Break]` followed by a display newline |

The exact names should be frozen by an ADR and golden tests before public
preview. Adding aliases later is easy; changing canonical output after users
begin copying or scripting against it is not.

### 6.3 Escaping

In Advanced source mode, literal syntax characters use backslash escapes:

| Content | Projection |
|---|---|
| `\` | `\\` |
| `[` | `\[` |
| `]` | `\]` |
| `"` inside quoted values | `\"` |
| Other non-printing controls | `\u{HEX}` or a named structural token |

The structured view must consume projector tokens directly. It must never
reparse the rendered bracket text to decide which spans are interactive.

### 6.4 Transition ordering

At a run boundary:

1. close/reset attributes that changed, in reverse canonical order;
2. open/set changed attributes in this order: Bold, Italic, Underline, Strike,
   Font, Size, Text Color, Highlight, Link; and
3. emit text.

Fixed ordering ensures snapshot stability even when several attributes change
at the same document boundary. Phase 1 freezes it with golden/property tests;
once the optional parser exists, a property-based test must additionally prove
that projecting, parsing, and projecting again produces byte-identical canonical
text for the supported subset.

## 7. Projection and mapping model

### 7.1 Proposed headless types

```text
FormatCodeProjector
  Project(RichTextDocument, FormatCodeProjectionOptions)
    -> FormatCodeProjection

FormatCodeProjection
  Text
  Tokens
  Diagnostics
  MapDocumentPosition(position, affinity)
  MapProjectedOffset(offset)

FormatCodeToken
  Kind
  DisplayText
  ProjectedStart / ProjectedLength
  SourceBefore / SourceAfter
  AffectedRange
  EditCapabilities

FormatCodeCaret
  TokenIndex
  OffsetWithinToken
  BoundaryAffinity
```

Token kinds should at least distinguish `Text`, `InlineCode`, `ParagraphCode`,
`StructureCode`, `Escape`, `PendingCode`, and `Diagnostic`.

### 7.2 Boundary affinity

Several zero-width transitions can occur at one document position. A
`RichTextPosition` alone cannot express a caret between those projected tokens.
The code view therefore owns a `FormatCodeCaret` with token identity and
before/after affinity. Mapping it back to RichEdit intentionally collapses to an
opaque document position; returning to the pane restores the nearest stable
token affinity where possible.

Selection mapping rules must be explicit:

- document text selection maps to all text tokens in the range;
- opening and closing codes whose affected ranges intersect the selection get a
  secondary boundary highlight, not the primary text-selection color;
- selecting only a code token maps to its boundary plus `AffectedRange` metadata;
- reverse selections preserve anchor/focus direction;
- empty paragraphs and end-of-document have addressable structural tokens; and
- grapheme/surrogate boundaries follow the model's opaque navigation contract,
  not UI arithmetic over public string indexes.

### 7.3 Pending caret formatting

Broiler can arm bold/italic/etc. at an empty selection without modifying the
document. Represent this as a visually distinct `PendingCode` overlay at the
caret, derived by comparing `Document.InlineStyleAt(Selection.Focus)` with
`CaretInlineStyle`.

Pending tokens:

- are not part of canonical copied/exported text by default;
- carry an accessible label such as “pending formatting”; and
- become ordinary persistent transitions when typed text enters the document.

## 8. Component architecture

### 8.1 Recommended assemblies

```text
Broiler.Documents.Model
        ^
        |
Broiler.Documents.FormatCodes        (projection, grammar, parser, edit intents)
        ^
        |
Broiler.UI.FormatCodeView            (platform-neutral control abstraction)
        ^
        |
Broiler.UI.FormatCodeView.Standard   (layout, drawing, input, semantics)

Broiler.UI.RichEdit + FormatCodeView
        ^
        |
WriterFormatCodesController          (application binding; no model ownership)
```

`Broiler.Documents.Model` should friend `Broiler.Documents.FormatCodes` so the
projector can map internal paragraph/run offsets without weakening the public
opaque-position design. No DOM, codec, native UI, or platform dependency belongs
in the projection assembly.

The control follows the approved Broiler.UI per-type assembly rule:

- `Broiler.UI/src/Abstractions/Text/Broiler.UI.FormatCodeView`
- `Broiler.UI/src/Implementations/Standard/Text/Broiler.UI.FormatCodeView.Standard`
- matching tests under `Broiler.UI/tests/Text`

### 8.2 Source of truth and edit flow

`UiRichEdit` remains the only source of mutable editor state:

```text
RichTextDocument snapshot
    -> FormatCodeProjector
    -> immutable projection
    -> StandardFormatCodeView

code-view action
    -> typed FormatCodeEditIntent
    -> WriterFormatCodesController
    -> UiRichEdit/RichTextEditor transaction
    -> DocumentChanged
    -> new projection
```

Do not mutate the document by parsing brackets from the rendered UI. Do not set
`UiRichEdit.Document` for ordinary pane actions. Structured operations need new
typed control/model entry points where current commands are insufficient, for
example:

- replace a text range;
- apply/clear an exact inline delta over an explicit range;
- apply an exact paragraph delta over an explicit range; and
- replace a whole document as one explicit undoable transaction only for an
  accepted Advanced-source draft.

### 8.3 Caching and large documents

- Cache the last projection by document reference and options.
- Cache per-paragraph token fragments by immutable paragraph identity.
- Reuse unchanged fragments after copy-on-write edits and recompute prefix
  offsets only from the first changed paragraph.
- Selection-only updates change highlights/scroll position without rebuilding
  token text.
- For small documents, project synchronously for immediate feedback.
- Above a benchmarked threshold, project an immutable snapshot off the UI path,
  discard stale results, and publish only if the editor still holds that snapshot.
- Virtualize visual lines/tokens; never create one UI child per code token.
- Provide a “current paragraph / selection / whole document” scope option if
  million-character documents cannot meet the interaction budget.

## 9. Phased implementation plan

Effort bands below assume one engineer familiar with Broiler, exclude legal fees,
and are estimates rather than commitments.

### Phase 0 - Decisions, ADR, baselines, and clearance brief

**Estimate:** 3-5 engineering days

**Status:** Complete (2026-07-12). Public evidence is recorded in the
[`Phase 0 implementation record`](formatting-codes-phase0/implementation-record.md),
with the machine-readable
[`benchmark baseline`](formatting-codes-phase0/benchmark-baseline.windows-x64.json)
and deterministic fixture manifest under
[`tests/formatting-codes-phase0`](../../tests/formatting-codes-phase0/README.md).

Tasks:

- [x] Approve **Formatting Codes** as the working public name and choose the final
  original shortcut.
- [x] Add ADRs freezing projection-vs-source semantics, grammar versioning,
  assembly boundaries, source-of-truth rules, and editable-mode scope.
- [x] Record the IP provenance and release-clearance checklist from section 2.
- [x] Decide whether paragraph properties are shown before their visual renderer is
  complete; recommended: show them with an “engine state” diagnostic and finish
  paragraph rendering in parallel.
- [x] Add representative benchmark fixtures: 1K, 100K, and 1M characters; low- and
  high-run-density; many empty paragraphs; Unicode-heavy text.
- [x] Capture existing RichEdit and Writer tests/builds as a regression baseline.

Exit gate:

- [x] product and architecture decisions are approved;
- [x] canonical sample output is signed off;
- [x] read-only MVP is explicitly separated from source editing; and
- [x] the legal brief is ready for later counsel review.

### Phase 1 - Headless canonical projector

**Estimate:** 5-8 engineering days

**Status:** Complete (2026-07-12). Public evidence is recorded in the
[`Phase 1 implementation record`](formatting-codes-phase1/implementation-record.md),
the frozen
[`grammar version 1`](../../Broiler.Documents/Broiler.Documents.FormatCodes/GRAMMAR.md),
and the repeatable
[`benchmark baseline`](formatting-codes-phase1/benchmark-baseline.windows-x64.json).

Tasks:

- [x] Create `Broiler.Documents.FormatCodes` and tests.
- [x] Add the narrow `InternalsVisibleTo` entry in `Broiler.Documents.Model`.
- [x] Implement token/data contracts, deterministic inline transitions, paragraph
  properties, structure tokens, escaping, and invariant formatting.
- [x] Emit mappings for every text span and zero-width boundary.
- [x] Add pending-style overlay support as a separate projection layer.
- [x] Implement exact and property-style randomized tests, including the user's example.
- [x] Establish benchmark baselines and a full-rebuild threshold.

Exit gate:

- [x] all supported model fields project deterministically;
- [x] equivalent normalized documents have identical output;
- [x] text/tokens cannot be confused through literal brackets or control characters;
- [x] every projected offset maps predictably to a document boundary/range; and
- [x] the projector has no UI, codec, DOM, or platform dependency.

### Phase 2 - Read-only FormatCodeView control

**Estimate:** 7-10 engineering days

**Status:** Complete (2026-07-12). Public evidence is recorded in the
[`Phase 2 implementation record`](formatting-codes-phase2/implementation-record.md)
and machine-readable
[`component boundary`](formatting-codes-phase2/phase2-boundary.json).

Tasks:

- [x] Add `UiFormatCodeView`, `StandardFormatCodeView`, and factories.
- [x] Implement text/code token layout, wrapping/no-wrap, clipping, scrollbars,
  selection painting, caret painting, hit testing, and theme properties.
- [x] Implement keyboard navigation, copy, select all, and search within projection.
- [x] Add semantic role/text information and focused-token accessible descriptions.
- [x] Add a resizable, keyboard-operable splitter abstraction because the layout
  layer lacks one; do not bury splitter logic in Writer.
- [x] Add deterministic render-list and input tests.

Exit gate:

- [x] the control displays large projections without one child per token;
- [x] all content is reachable by keyboard and pointer;
- [x] light/dark/high-contrast/text-scale render-list snapshots pass;
- [x] copy returns canonical plain text; and
- [x] no document mutation API exists yet.

### Phase 3 - Writer integration and synchronized navigation

**Estimate:** 5-8 engineering days

**Status:** Complete (2026-07-12). Public evidence is recorded in the
[`Phase 3 implementation record`](formatting-codes-phase3/implementation-record.md)
and the machine-readable
[`Phase 3 boundary record`](formatting-codes-phase3/phase3-boundary.json).

Tasks:

- [x] Add `WriterFormatCodesController` with subscription disposal and reentrancy
  guards.
- [x] Subscribe to `DocumentChanged`, `SelectionChanged`, and `CommandExecuted`.
- [x] Integrate View menu command, pane, splitter, layout reservation, status text,
  and palette tokens in desktop Writer.
- [x] Mirror the small shell changes in WebAssembly Writer while sharing all feature
  logic.
- [x] Implement caret follow, selection mapping, click-to-source, affected-range
  highlight, focus cycling, and pending-style overlay.
- [x] Preserve pane state through New/Open/Save and document replacement.
- [x] Add desktop/browser parity and smoke tests.

Exit gate - **read-only preview milestone**:

- [x] `[Bold ON]Hello World![Bold OFF]` is produced for the representative document;
- [x] edits in the main editor update the pane without manual refresh;
- [x] navigation works in both directions without loops or focus theft;
- [x] show/hide and resizing do not damage Writer layout;
- [x] desktop and WebAssembly behavior match; and
- [x] existing document codecs and RichEdit tests remain green.

### Phase 4 - Safe structured editing

**Estimate:** 10-15 engineering days

**Status:** Complete (2026-07-15). Public evidence is recorded in the
[`Phase 4 implementation record`](formatting-codes-phase4/implementation-record.md)
and the machine-readable
[`Phase 4 boundary record`](formatting-codes-phase4/phase4-boundary.json).

Tasks:

- [x] Define typed `FormatCodeEditIntent` operations and validation.
- [x] Add missing explicit-range operations to `RichTextEditor`/`UiRichEdit`, each as
  one undo transaction with before/after selection.
- [x] Permit ordinary text edits within text tokens.
- [x] Keep code tokens atomic. Backspace/Delete selects an intentional action rather
  than silently producing malformed bracket text.
- [x] Add code property editing, Clear Formatting, Insert Code palette, and link/color
  validation.
- [x] Define deletion semantics as semantic operations. Recommended first behavior:
  deleting a paired code offers “remove this formatting from its affected range”
  rather than imitating endpoint leakage.
- [x] Add undo/redo, IME, clipboard, read-only, malformed-intent, and selection tests.

Exit gate - **structured editable milestone**:

- [x] every pane action is one predictable undo unit;
- [x] undo/redo from either pane restores the same document and selections;
- [x] no edit bypasses read-only state or document limits;
- [x] token actions cannot create an unrepresentable document; and
- [x] projector output after every edit is canonical.

### Phase 5 - Advanced textual source editing (optional)

**Estimate:** 15-25 engineering days

This phase requires an explicit product go/no-go review. It is not necessary to
deliver the main user benefit.

Tasks if approved:

- Add a versioned parser with source spans, recovery, diagnostics, and limits.
- Edit a detached draft, not the live document, while syntax is invalid.
- Parse to a candidate `RichTextDocument`, validate all URLs/colors/numbers and
  size/run/paragraph limits, then apply atomically.
- Add an explicit undoable whole-document replacement transaction or compute a
  stable operation diff; do not call history-resetting `LoadDocument`.
- Preserve projection caret as well as possible after canonical reformatting.
- Add grammar version metadata for exported scripts/snippets.
- Fuzz parser, escape handling, pathological nesting, long tokens, and invalid
  Unicode/control input.

Exit gate:

- `parse(project(document))` is model-equivalent for the supported subset;
- `project(parse(text))` is stable canonical output;
- invalid drafts never partially change the live document;
- Apply/Cancel and errors are accessible and lossless; and
- counsel has reviewed the final interaction specification before commercial
  release if it becomes materially closer to WordPerfect.

### Phase 6 - Hardening, performance, and rollout

**Estimate:** 5-10 engineering days

Tasks:

- Tune incremental projection and virtualized layout against Phase 0 fixtures.
- Complete paragraph visual-rendering parity or document every remaining engine
  limitation.
- Run Windows, Linux where supported, and WebAssembly interaction passes.
- Add localization without localizing canonical keywords; localize descriptions
  and UI chrome only.
- Add privacy-safe diagnostics: timing, token/run counts, and failures only;
  never document text, links, selections, or copied projections.
- Release behind an opt-in preview flag, then enable read-only mode by default
  after crash/performance/accessibility gates pass.
- Complete the commercial IP clearance in section 2.4.

Exit gate:

- agreed performance budgets pass on representative hardware;
- no accessibility P0/P1 defect remains;
- no document content appears in logs/telemetry;
- desktop/browser parity tests are green; and
- the release checklist and legal clearance are signed off.

## 10. Test strategy

### 10.1 Projection golden cases

- Plain, empty, and multi-paragraph documents.
- Exact `[Bold ON]Hello World![Bold OFF]` case.
- Every inline property alone and in combination.
- Multiple attributes changing at one boundary.
- Adjacent equal runs and redundant-transition elimination.
- Every paragraph property, empty styled paragraphs, and trailing empty paragraph.
- Tabs, U+2028 soft breaks, paragraph breaks, brackets, slashes, quotes, controls.
- BMP text, combining sequences, emoji/surrogate pairs, bidi, and RTL text.
- Links with escaping and allowed/disallowed schemes.
- Default resets at paragraph and document boundaries.
- Pending caret styles, including multiple toggles before typing.

### 10.2 Mapping and interaction cases

- Every projected offset round-trips to a valid source mapping.
- Multiple zero-width codes at one boundary retain stable affinity.
- Forward/reverse selections across runs and paragraphs.
- Click on text, opening token, closing token, structure token, and EOF.
- Main-editor scroll/caret follow without stealing focus.
- Pane selection does not recursively oscillate with editor selection.
- Resize, collapse, restore, wrap/no-wrap, and high text scale.
- Mouse, keyboard-only, screen-reader semantics, IME, and clipboard.

### 10.3 Editing cases

- Text insertion/replacement/deletion at every token boundary.
- Apply/clear each style across single/multiple runs and paragraphs.
- Structured token delete/edit/insert semantics.
- Undo/redo from both panes and after focus changes.
- New/Open/codec import followed by edit and save.
- Read-only and disabled editor rejection.
- Parser recovery/fuzz/limits if Phase 5 is approved.

### 10.4 Performance gates

Set numeric budgets from measured Phase 0 baselines, then enforce at least:

- full projection latency and allocation for 1K/100K/1M-character fixtures;
- incremental single-character edit latency at the start/middle/end;
- selection-only update latency without reprojection;
- high run-density and many-paragraph worst cases;
- first paint, scroll, and hit-test latency in the virtualized view; and
- stale background projection cancellation.

Do not choose a headline millisecond target before measuring Broiler's existing
RichEdit and CI hardware. The user-visible goal is no dropped typing, caret, or
scroll frames in the supported full-document range.

## 11. Accessibility, security, and privacy requirements

- The pane is a real focusable text/code view, not a painted bitmap.
- Screen readers receive the visible/canonical value, selection, caret, editable
  state, and a concise description for the focused code token and affected range.
- Selection changes in the main editor do not cause the entire pane to be
  repeatedly announced.
- Code meaning never depends on color alone.
- The splitter and all header actions are keyboard operable.
- Canonical text remains Unicode even though commands are ASCII.
- Raw source parsing, if shipped, enforces the same document limits and safe-link
  policy as codecs; it never executes commands, macros, scripts, or external URLs.
- Telemetry and default logs include no document text, token text, URLs, file
  paths, clipboard content, or selection content.

## 12. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Projection is mistaken for the original file control stream | User distrust/data-loss assumptions | Call it canonical Broiler state; document import losses and unsupported properties |
| Free-form codes do not fit normalized runs | Ambiguous edits or model redesign | Ship read-only then token-aware operations; gate raw mode separately |
| Document assignment clears undo history | Lost undo | Add explicit transactional operations; never use `LoadDocument` for pane actions |
| Several codes share one source position | Caret jumps/selection ambiguity | Separate `FormatCodeCaret` with token identity and affinity |
| Pending formatting is invisible in snapshot | Pane looks stale | Observe `CommandExecuted`; show noncanonical pending overlay |
| Paragraph state is not yet rendered | Pane shows a code with no visual effect | Finish paragraph rendering or mark as engine state with a diagnostic |
| Large documents rebuild too often | Typing/scroll lag | Immutable paragraph cache, incremental offsets, background snapshots, virtualization, scope modes |
| Desktop/browser shells drift | Inconsistent feature | Shared projector/view/controller plus parity tests; duplicate only layout wiring |
| Accessibility becomes too verbose | Unusable screen-reader experience | Focused-token descriptions, throttled announcements, semantic tests |
| Product looks/names itself too much like WordPerfect | IP/brand confusion | Original name, shortcut, palette, token grammar, icons, and interaction design; clearance gate |

## 13. Effort and dependency summary

| Milestone | Included phases | Approximate effort | Depends on |
|---|---|---:|---|
| Technical proof | 0-1 | 2-3 engineer-weeks | ADR decisions |
| Read-only preview | 0-3 | 4-6 engineer-weeks | Projector, control, Writer wiring |
| Safe structured editing | 0-4 | 7-10 engineer-weeks | Explicit-range transaction APIs |
| Advanced raw source editing | 0-6 | 10-15 engineer-weeks | Product demand, parser, hardening, legal review |

Parallel work:

- paragraph-style visual rendering can proceed alongside Phases 1-3;
- legal counsel does not block an internal read-only prototype but must finish
  before material commercial release;
- desktop and WebAssembly shell wiring can proceed in parallel once the shared
  controller contract is stable; and
- UX visual design can proceed after Phase 0 while the headless projector is built.

## 14. Recommended open decisions

| Decision | Recommendation |
|---|---|
| Public name | **Formatting Codes**; run trademark clearance before launch |
| Default mode | Read-only |
| Default scope | Whole document for normal sizes; automatic virtualized/current-area mode above measured threshold |
| Default wrapping | On, with a no-wrap toggle |
| Shortcut | `Ctrl+Shift+F3`, subject to shortcut audit |
| Paragraph codes before renderer parity | Show with explicit engine-state diagnostic, then remove diagnostic as rendering lands |
| Editable endpoint deletion semantics | Do not emulate code leakage in v1; use explicit semantic actions |
| Raw source editing | Defer until preview usage supports the cost |
| Canonical grammar localization | Never localize keywords; localize UI and accessible descriptions |

## 15. Definition of done for the recommended v1

The feature is complete when:

1. the user can toggle and resize a bottom **Formatting Codes** pane in desktop
   and WebAssembly Writer;
2. it deterministically shows all supported Broiler inline, paragraph, and
   structural state as simple text tokens;
3. caret, selection, scrolling, and focus synchronize in both directions;
4. code inspection explains the mapped property and affected range;
5. structured text/format changes are atomic and use the existing undo/redo
   history;
6. Unicode, bidi, IME, accessibility, high contrast, large documents, and
   privacy gates pass;
7. import/export limitations are accurately documented;
8. all existing RichEdit, document-codec, desktop, and WebAssembly tests remain
   green; and
9. the final name, patent implementation, and visual design pass the targeted
   commercial-release clearance.

## 16. Source notes

Primary and authoritative references used for the IP assessment:

- [Corel virtual patent marking](https://www.corel.com/en/patent/)
- [WordPerfect official Reveal Codes overview](https://www.wordperfect.com/us/pages/reveal-codes/?pgid=15200051&storeKey=us)
- [Corel Reveal Codes tutorial](https://kb.corel.com/en/127364)
- [Library of Congress WordPerfect document family](https://www.loc.gov/preservation/digital/formats/fdd/fdd000621.shtml)
- [US 5,345,551](https://patents.google.com/patent/US5345551A/en)
- [US 7,043,689](https://patents.google.com/patent/US7043689B2/en)
- [USPTO patent basics](https://www.uspto.gov/patents/basics/patent-process-overview)
- [US Copyright Office, ideas/methods/systems](https://www.copyright.gov/circs/circ31.pdf)
- [EU Software Directive 2009/24/EC](https://eur-lex.europa.eu/eli/dir/2009/24/oj/eng)
- [German Patent and Trade Mark Office, patent protection](https://www.dpma.de/english/patents/patent_protection/index.html)
