# Printing roadmap

- **Status:** First vertical slice implemented on component branches (`claude/printing`), not yet
  published; later phases planned
- **Scope:** Printing and print preview across the platform: the page model, the print dialog,
  the platform print hosts, and printing in the applications
- **Last verified:** 2026-10-12
- **Primary owner:** Broiler.UI (print contract and dialog) with Broiler.Hosting (platform hosts)
- **Contributing owners:** Broiler.Graphics, Broiler.Native, Broiler.Documents, Broiler.HTML, and
  the Writer, Plate and Browser applications
- **Sequencing authority:** This document for cross-component printing work; Broiler.UI ADR 0035
  and the owning component roadmaps for component-local detail

## The design in one paragraph

Every content source lays itself out as **pages**: fixed-size `Broiler.Graphics.Paging.BPage`
recordings in points that carry their own image pixels. Every output consumes the same pages:
Broiler.UI's print preview replays them at any zoom, a platform print host rasterizes or replays
them for its printer, and a PDF writer serializes them. A document that can be laid out again
for any paper is an `IBPagedDocument`; the print dialog re-paginates it whenever the paper or
orientation changes, and the job prints exactly the pages that were previewed.

```
Writer/Plate → Documents.Pagination ┐                         ┌→ UI print preview (replay)
Browser      → HTML print re-layout ├→ BPage (points) ────────┼→ Windows spooler: banded raster → GDI DC
Plate pictures → fitted page        ┘   [Broiler.Graphics]    ├→ CUPS: raster PDF spool file
                                                              └→ Android: vector into PrintedPdfDocument
```

## Decisions

| Decision | Choice | Why |
|---|---|---|
| Where the page model lives | `Broiler.Graphics.Paging` | Every producer and every consumer already references Graphics; nothing else is common to Documents, Layout/HTML, UI and Hosting. |
| Print dialog | Broiler's own owner-drawn dialog on every desktop platform | Matches the file and font dialogs, is needed on Linux and WebAssembly anyway, and can preview the real pages. |
| Platform print UI | Used where it is the only UI (Android) | `IUiPrintHost.ShowsSystemPrintDialog`; `UiPrintDialog.PrintAsync` then shows no Broiler dialog. |
| PDF for spoolers | Written by an `IBPageDocumentWriter` the application head composes (`PdfRasterPageWriter`) | Keeps Broiler.Hosting free of a document codec; PDF serialization stays in Broiler.Documents.Pdf as its roadmap assigns. |
| Linux backend | libcups (`cupsPrintFile2`), probed | Available on every desktop distribution; the xdg-desktop-portal print interface is the later option for sandboxed packaging. |
| Windows backend | Banded raster into a GDI printer DC first | Prints on every driver; a vector Direct2D/XPS path is a later phase behind the same host. |

## What is implemented

| Component | Branch commit(s) | What it adds |
|---|---|---|
| Broiler.Graphics | `Add the page model printing and PDF export share` | `BPaperSize` (one catalogue in points), `BPageSetup`, `BPage`/`BPageBuilder`, `IBPageSource`, `IBPagedDocument`, `IBPageDocumentWriter`, `BPageImageCache`, banded `BPageRasterizer`; `BRenderList.Append`. |
| Broiler.Native | `Add the printing bindings Hosting's print hosts need` | winspool and gdi32 print bindings with `DEVMODEW`, `DOCINFOW`, `PRINTER_INFO_4W`; libcups bindings with `cups_dest_t`, `cups_option_t`, `cups_size_t`. ABI tests; assurance regenerated, `check` clean. |
| Broiler.Documents | `Start Broiler.Documents.Pagination from the CLI's layout` | New `Broiler.Documents.Pagination` package (the CLI layout moved unchanged behind `DocumentPaginator`, `PaginatedDocument`, `RichTextPagedDocument`); `PdfRasterPageWriter` spool writer in Broiler.Documents.Pdf. |
| Broiler.UI | `Add printing: a host capability, a print dialog and a print preview`; `Keep the print job past the dialog's close` | `IUiPrintHost`, `UiPrintSettings`, `UiPageRange`, `UiPrinterCapabilities`, `UiPrintJob`; `Broiler.UI.PrintPreview(.Standard)` and `Broiler.UI.PrintDialog(.Standard)`; ADR 0035. |
| Broiler.HTML | `Let a caller lay a document out on pages`; `Print a leading heading on the first page, and report the document mode` | Public `HtmlContainer.PageSize` and `IsQuirksMode`; the CSS 2.1 sample-sheet `h1 { page-break-before: always }` print rule removed (no browser has it). |
| Broiler.Hosting | `Add print hosts for Windows, Linux and Android` | `WindowsPrintHost` (spooler, device mode, banded `StretchDIBits`, `PrintToFileAsync`), `LinuxCupsPrintHost`, `AndroidPrintHost` (`PrintDocumentAdapter` drawing vector pages through `AndroidCanvasRenderer.Draw`). |
| Broiler.Writer | `Print from File, Print and Ctrl+P` | File ▸ Print and Ctrl+P on all heads; WebAssembly has no host and offers no Print. |
| Broiler.Plate | `Print the open document or picture` | Documents and pictures; an opened PDF's permissions are honoured (refused without Print, 150 dpi without high-quality print). |
| Broiler.Browser | `Print the page with Ctrl+P` | `BrowserPrintDocument`: print-media re-layout with pagination, one vector render list sliced into pages. |

### Evidence

- Unit and integration suites pass on every touched component: Graphics 180, Native 13, Documents
  (all 13 projects), UI (11 projects, 1,365 tests), Hosting 225, Writer 275, Plate 13,
  HTML 140, Browser 332 + 227.
- A two-copy job reached **Microsoft Print to PDF** through the real spooler and came back as four
  landscape-Letter pages with the content where it was laid out (`WindowsPrintHostTests`).
- The print dialog was rendered over the Writer window and a printed browser page was rasterized
  and inspected: `@media print` applied, table, borders and text intact, lines broken between
  pages.
- Not verified on hardware: CUPS (built and unit-tested on Windows only) and Android (built for
  `net10.0-android36.0`; the adapter has not run on a device).

## Release order

The components consume each other as nuget.org packages, so they must be published in dependency
order before their consumers' branches build in CI:

1. Broiler.Graphics 0.1.0-preview.13
2. Broiler.Native 0.1.0-preview.9
3. Broiler.Documents 0.1.0-preview.23 (needs Graphics .13)
4. Broiler.UI 0.1.0-preview.21 (needs Graphics .13)
5. Broiler.HTML 0.1.0-preview.29 (independent of the above)
6. Broiler.Hosting 0.1.0-preview.10 (needs UI .21, Graphics .13, Native .9)
7. Broiler.Writer, Broiler.Plate, Broiler.Browser

Broiler.Browser's `main` already fails to build against the published Broiler.HtmlBridge
0.1.0-preview.28, which lacks `DocumentFetchTiming.RecordResponseVersion`; an HtmlBridge release
from current source is needed for the Browser regardless of printing. The printing work was
verified against a local pack of that source.

Then this repository bumps the submodule pointers.

## Next phases

### P2 — Vector output and PDF export

- **Graphics:** positioned glyph runs with font identity and raw font bytes; paths, gradients and
  opacity groups in `BRenderList` (also removes what the HTML renderer drops on screen).
- **Documents.Pdf:** a page-scene PDF writer with TrueType subsetting and embedded images,
  replacing the raster spool writer for export; then Export as PDF in Writer, Plate and Browser.
- **Graphics.Windows / Hosting.Windows:** a Direct2D print-control path (command list per page into
  an XPS print job) behind `WindowsPrintHost`.
- **Exit gate:** a printed and an exported page are vector, text selectable in the PDF, and pixel
  comparable with the preview.

### P3 — The web platform

- Move `BrowserPrintDocument` into Broiler.HTML.Graphics as a public HTML print document, so Mail
  prints messages through the same path.
- `window.print()`, `beforeprint`/`afterprint` and `matchMedia` change events through
  Broiler.HtmlBridge.
- `@page` size, margins and margin boxes from the cascade rather than the dialog alone; running
  headers and footers; iframe contents.
- Lift the WPT runner's paged renderer onto the same path, so its paged reftests gate printing.

### P4 — Remaining applications and platforms

- Broiler.Code: a source printer (monospace pages, line numbers, a header).
- Writer for WebAssembly: a PDF handed to the browser's own print.
- Printing an opened PDF faithfully needs `Broiler.Documents.Pdf.Rendering` (PDF roadmap item 11);
  until then Plate prints the reflowed document.
- CUPS through xdg-desktop-portal for sandboxed Linux packages.

## Known limits of the first slice

- Pagination runs on the UI thread when the page setup changes.
- Broiler.UI has no key-gesture binding; each application reads Ctrl+P itself.
- The Windows path is raster (at most 600 dpi unless the job asks), so text is not selectable in a
  PDF printed through Microsoft Print to PDF.
- The browser prints backgrounds; there is no "background graphics" switch yet.
