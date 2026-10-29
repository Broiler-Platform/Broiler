# W4 — Cells browser integration

Implementation: 2026-10-29. Local published-artifact verification; no public
domain, deployment, NuGet publication or signing configuration was changed.

## Application behavior

The existing `CellsApp`, `CellsView`, `CellControl`, `SheetModel` and
`SheetCsvSerializer` remain the implementation used by desktop and browser.
An optional file-action callback routes the shared menu, toolbar and keyboard
commands to the browser head before desktop file dialogs or path IO are reached.
Native defaults retain their existing behavior.

New, Close, Open and Return/Exit protect modified content. The HTML confirmation
offers Export and keep open, Discard and Cancel. Export does not continue the
destructive action: a browser download cannot establish that the user retained a
copy. The browser's navigation/close warning is attached while modified. It is
best-effort protection, **not automatic recovery**. W5 owns persistent checkpoints.

CSV opening uses the selected file's bounded bytes and the shared stream
serializer. Parsing, diagnostics, dimensions and text budgets must succeed before
the new sheet replaces the current model. Cancellation, unsupported extensions,
malformed/partially read input and limit failures retain the original sheet.
The serializer's new optional `rejectDataLoss` mode refuses codec warnings or
partial results and dimensions that would otherwise be clipped. Existing native
callers keep their default mode.

Export uses the shared stream serializer and a real `text/csv` Blob download.
The filename dialog is asynchronous HTML, not a desktop filesystem dialog or
`window.prompt`. `ExportInitiated`, `Canceled` and `Failed` are visible outcomes.
There is no file-handle write in this edition, so it never reports `SavedToHandle`
or marks modified content clean after downloading. Opening a successfully parsed
sheet or explicitly creating/discarding to a new sheet establishes a new clean
generation. Model changes raise `SheetModel.Changed`; pending text input also
keeps the browser warning active. This deliberately favors retaining modified
state if an edit was subsequently canceled or undone.

## Input and clipboard

The hidden textarea follows the managed caret in CSS pixels, accepts committed
Unicode and forwards composition start/update/commit/cancel. Duplicate input
echoes following composition are suppressed. A composition starting in the grid
opens the existing text editor; cancellation restores the original cell. These
automated composition events verify the adapter contract, not a physical IME.

Keyboard navigation, Enter/F2 editing and active-text undo use shared controls.
There is no sheet-wide undo/redo history. Clipboard semantics currently cover the
selected **single cell**, including literal multiline/tab text in that cell;
rectangular TSV paste is not advertised. Copy/cut provide a real textarea
selection for native browser events. Clipboard processing drains queued managed
input first, commits pending cell edits, and uses a bounded trusted-event scope.
Synthetic clipboard events cannot modify a sheet. Menu/toolbar clipboard actions
explain the Ctrl/Cmd keyboard shortcut when no trusted clipboard event is active;
Cut cannot clear a cell in that case.

Pointer capture/cancellation, blur cleanup, wheel units and resize are wired to
the managed session. Frame requests raised during rendering are retained for the
next animation frame. Frame index/caret state is published; per-input debug spam
has been removed. The browser releases listeners, file inputs, composition timers
and model subscriptions on teardown.

## Explicit limits and fidelity

| Capability | Browser edition |
| --- | --- |
| File format | CSV only; UTF-8 export, codec-supported CSV input dialect detection |
| Sheet dimensions | At most 100 rows × 100 columns; oversized files rejected, never clipped |
| Input/output bytes | 2 MiB per CSV file |
| Text budgets | 16,384 characters per cell; 250,000 total characters checked at import/export |
| Values | Strings; `=1+2` remains that literal string, without calculation |
| CSV representation | Quoted delimiters/quotes, multiline fields, Unicode and empty cells |
| Exported formula-like strings | Preserved exactly; another spreadsheet program may interpret them as formulas |
| Formatting, workbook sheets, charts, XLSX/ODS | Not supported by this CSV edition |
| Undo | Active text edit only; no sheet-wide undo or redo |
| Clipboard | One selected cell; keyboard browser clipboard events |
| Persistence | Explicit download; no automatic durable draft until W5 |
| Printing/PDF/file-handle save | Not advertised |

The app displays these limits and the latest operation outcome. CSV reserialization
normalizes quoting, encoding and line endings: fidelity means model values and
sheet dimensions, not identical source bytes. Fonts, cell widths and row heights
are not CSV data. Editing and rendering a grid do not imply formula evaluation.

## Reproduction and evidence

From `D:\Broiler\tests\office-web`, provision the pinned W0 SDK/workload and
Playwright binaries, run `npm ci`, then `./verify-w4.ps1`. The script:

1. Records SDK/workload inventory and runs the Cells xUnit v3 executable suite,
   including the native Windows project dependency.
2. Publishes Release, trimmed, non-AOT WASM with locked released NuGet dependencies
   (`UseBroilerUiSource=false`, `RestoreLockedMode=true`).
3. Serves the exact output on loopback and runs `npm run test:cells` against
   Chromium, Firefox and WebKit.

Evidence is generated beneath `artifacts/web-w4`: published output, resolved
assets/inventory, browser JSON report/screenshots and actual CSV downloads.
Browser tests mutate through keyboard, composition, clipboard and file chooser
events; a read-only managed snapshot checks the model. Exported bytes are reopened
through the same published application and compared cell-for-cell.

Validated on 2026-10-29: **140 shared/native tests passed**, a locked trimmed WASM
publish succeeded, and the Cells flow suite passed Chromium **156.0.8078.4**,
Firefox **157.0** and WebKit **27.2**. The existing editor baseline also passed
12 cold/warm cases plus three missing-entry negative cases with the new Cells
artifact; all three launcher integration runs passed with the same Cells output.
These are local automated results, not a deployed-domain or Safari support claim.

The suite covers typing/navigation/commit, active-text undo, trusted clipboard,
composition/echo, cancellation/discard, dirty download semantics, quoted/multiline
CSV, oversized row/column/file/cell rejection, unsupported extensions, resize and
pointer/wheel cancellation. This is a bounded fixture matrix, not CSV conformance
or mobile/touchscreen usability certification. Real Safari, physical IMEs,
assistive technology and performance budgets remain W7.

## Remaining dependencies

The formal W1 `IDocumentPort`/shared-JS migration remains pending publication of
`Broiler.Hosting.Contracts` and `Broiler.Hosting.WebAssembly`. NuGet's public
flat-container endpoints returned 404 for both on 2026-10-29. Production Cells
therefore retains released packages and its app-owned browser command/transport
adapter; it does not silently reference unpublished Hosting source. This is an
explicit remaining integration item in W4, not a claim that shared-host adoption
is complete. The new callbacks make that replacement possible without changing
the CSV engine or shared commands.

W5 must connect persistent recovery to these generation/revision/change hooks.
W6 still owns producer/deployment promotion. Signing remains deferred as requested.

The preceding W3 verification is also still open: its latest full run passed
Chromium but timed out on Firefox's first RTF download. W4 does not declare W3's
five-format/browser acceptance complete.
