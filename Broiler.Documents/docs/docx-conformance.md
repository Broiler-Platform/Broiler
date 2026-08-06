# DOCX Conformance

`Broiler.Documents.Docx` reads and writes a dependency-free DOCX subset using
Open XML WordprocessingML package parts.

## Supported Read/Write Subset

- Main document package discovery through `_rels/.rels` with fallback to
  `word/document.xml`.
- Paragraphs, empty paragraphs, tabs, and soft line breaks.
- Block-level containers, walked recursively so their paragraphs are read rather
  than dropped: tables (`w:tbl`/`w:tr`/`w:tc`, including nested tables),
  structured document tags (`w:sdt`), accepted revisions (`w:ins`, `w:moveTo`),
  `w:customXml`/`w:smartTag` wrappers, and `mc:AlternateContent` (first
  `mc:Choice`, else `mc:Fallback`). Tables are flattened into their cell
  paragraphs in row-major order, with a `docx.table.flattened` diagnostic —
  `RichTextDocument` has no table shape, and layout tables are how CV and
  letterhead templates hold their entire text.
- Direct inline formatting: bold, italic, underline, strikethrough, font
  family, font size, foreground color, and background shading.
- Paragraph formatting: left/center/right alignment, line spacing, spacing
  before/after, indentation, bullet lists, and numbered lists.
- External hyperlinks for `http`, `https`, and `mailto`, plus internal anchor
  links.

## Intentional Limits

- Styles, themes, tracked deletions, embedded objects, images, fields, comments,
  headers, footers, footnotes, table geometry, and section layout are skipped or
  approximated with diagnostics where applicable.
- Table structure (grid, spans, borders, cell shading) is not represented; only
  the cell text survives flattening.
- Block nesting deeper than `DocumentLimits.MaxGroupDepth` is abandoned with a
  `docx.limit.depth` diagnostic.
- DOCX packages above `DocumentLimits.MaxDocumentBytes` are not parsed.
- XML parts above `DocumentLimits.MaxBinBytes` are skipped.
- Color alpha is not represented by DOCX; RGB channels are written with a
  diagnostic.

## Read Diagnostics

Every read ends with a `docx.read.summary` info diagnostic carrying the
paragraph, flattened-table, and skipped-block counts. It exists so a document
that opens blank can be told apart from a document that *is* blank:

| Code | Severity | Meaning |
| --- | --- | --- |
| `docx.read.summary` | Info | Paragraph, table, and skipped-block counts for the read. |
| `docx.document.empty` | Warning | The body held block-level content but produced no paragraphs — a reader gap, not an empty file. |
| `docx.table.flattened` | Warning | At least one table was flattened into its cell paragraphs. |
| `docx.block.unsupported` | Warning | A block-level element was not understood; the message names the element. Reported once per distinct name. |
| `docx.limit.depth` | Warning | Block nesting hit `MaxGroupDepth`; the deepest content was skipped. |
| `docx.part.headerfooter` | Info | The package has headers or footers, which are not part of the body. |

`Broiler.Cli --convert-doc <in> --output <out>` prints all of them, which is the
quickest way to see what a problem document lost. In the Writer, set
`BROILER_WRITER_DOCUMENT_LOG=1` to have the same list written to stderr on every
open; the status bar always reports a read that produced no text.

## Probe Policy

DOCX probing is conservative because DOCX is a ZIP-based OPC package:

- ZIP signature plus DOCX filename/MIME hint is high confidence.
- A visible `word/document.xml` local ZIP entry is high confidence.
- Generic ZIP files are not claimed without a DOCX hint or WordprocessingML
  package evidence.
