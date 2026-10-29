# W5 — browser-local recovery and preferences

Implemented 2026-10-29 in the Writer and Cells WASM heads. This is local browser
storage and recovery, not cloud synchronization, deployment or a replacement for
exported backups. Existing released NuGet dependencies remain pinned.

## User flow

Both editors now expose **Recovery** in the Office navigation bar. Its visible
summary reports ready, pending, saving, saved, warning, unavailable or paused state. The
panel explains retention and browser-storage limitations, lists local drafts, and
provides:

- Automatic-checkpoint preference and an explicit Save checkpoint now action.
- Refresh, Restore, Export bundle and confirmed Delete for individual drafts.
- Direct Export current recovery, including when IndexedDB writes are denied.
- Import recovery bundle after storage loss or explicit deletion.
- Best-effort Request persistent storage and confirmed Clear local data.

Restore never runs automatically. It checkpoints current modified content first;
if that fails, restoration refuses to replace the document. A successful restore
creates a separate document identity, marks the document modified and retains the
source draft. Downloads do not confirm persistence and never clear modified state.
Deleting a draft does not delete the open document or an exported file. Clearing
local data removes this app's drafts, migration backups and recovery preferences,
leaves the open document intact, and pauses automatic checkpoints for that page.
It does not clear another app/origin's data, browser caches or native files.
Other open pages retain their own settings and may create new checkpoints.

Drafts remain until explicitly deleted, browser storage is cleared/evicted, or the
browser/profile is removed. They are not synchronized, not an independent backup
and not encrypted by application code. Other code running on the same origin has
the browser's normal access to that origin's data. Private browsing, quota,
browser policy or user settings may prevent persistence. Missing data is reported
as unavailable/absent rather than reconstructed from cached application assets.

## Storage and identities

IndexedDB databases are `broiler.writer.recovery` and `broiler.cells.recovery`,
within each application's origin. Database schema 1 contains `drafts` and
`backups` object stores. Record schema 2 stores app, document/tab identity,
generation, revision, name, format, payload, timestamps, producer path and codec
notes. The databases deliberately have no app-release/version URL in their name:
deploying a new version prefix does not move documents into an empty database.

Each page load receives a fresh cryptographic tab ID. Each document generation
and restored copy receives a fresh document/draft ID. No shared localStorage
"current document" slot and no cloned sessionStorage identifier can cause two
tabs to overwrite one another. Concurrent tabs do not merge edits; their drafts
are independent. Refresh the panel to see other pages' latest committed drafts.

Small schema-versioned localStorage preferences hold only the checkpoint-enabled
setting for the current page and future page loads. Already-open pages retain
their own setting. Preference failure leaves an in-page setting and visible explanation;
document content is never diverted into localStorage. Launcher theme/navigation
preferences retain the W2 behavior on their own origin.

Automatic checkpoints debounce changes for 500 ms, with a 2-second maximum wait
under continuous changes, subject to main-thread and storage availability.
Visibility-hidden/pagehide request an additional flush. They are useful signals,
not a guarantee that shutdown permits a final write. **Saved** is shown only
after the IndexedDB transaction completes. A crash can lose edits newer than the
last committed checkpoint. The beforeunload warning remains supplementary.

Writes are serialized. A failed transaction retains the previous committed
record. At most 30 drafts are accepted per app; additional drafts are refused with
an explanation and export/delete actions. Existing drafts can still be updated.
There is no automatic eviction of old drafts. A payload is bounded to 16 Mi
JavaScript characters, with smaller application-specific budgets enforced before
capture/restore. This cap does not promise that the browser grants that quota.

## Application representations

**Cells** stores a versioned envelope containing a rectangular JSON array of
string values. A pending grid/edit-line value is overlaid into the snapshot
without committing the editor or stealing focus. The existing 100 × 100,
16,384 characters/cell and 250,000 total-character budgets apply. Capture and
restore use the existing sheet model; there is no second CSV parser. Restoring
validates every row/value before installing the new sheet. Row/column display
sizes, cursor/selection and undo history are not part of the recovered value
model. Formula-looking strings remain literal strings.

**Writer** captures a bounded RTF representation using the existing codec and
resource context. It re-reads that RTF and refuses a checkpoint if text does not
round-trip or browser limits are exceeded. Codec notes remain in the record and
are shown before restoration; a conversion with notes receives warning status.
Restore validates the RTF, text/paragraph limits and image budgets before replacing
the current document. It restores modified editing content, not a native file
handle or a claim that the original file was saved.

RTF recovery is a supported-format representation, **not a lossless private
serialization of every possible document-model feature**. The automated fidelity
claim is bounded to tested text/formatting fixtures. Complex imported layout or
features unsupported by RTF may have conversion notes or refuse checkpointing;
retain an export of the original format when needed. Oversized/unrepresentable
documents remain open with a visible recovery failure and normal document-export
commands available. Recovery does not preserve undo history or active selection.

Recovery bundles contain the record and payload as JSON. They can be exported
without a successful database write and imported into a fresh identity later.
Writer/Cells formats are checked before restore; a bundle from another app cannot
silently become the current document. Imported metadata and app payloads are
bounded and validated. A bundle download remains an initiated download, not a
verified disk backup.

## Migrations and application updates

The defined schema-1-to-2 migration reads the latest source record and atomically
writes an untouched schema-1 backup plus the new record. Neither becomes committed
until the transaction completes. An abort rolls both back and retains the old
draft. Individual deletion removes its migration backup; explicit clear removes
all backups. Future/unknown record schemas are retained and exportable, but are
not restored by an older adapter. No migration deletes drafts as a startup task.
An IndexedDB version change closes the old connection; incompatible database
access fails visibly without deleting/recreating the database.

Migration backups are also available as separate export links. Importing an older
backup creates a new draft rather than rolling the live document back implicitly.

The same storage module is vendored byte-for-byte into the two independent
producer projects. W5 verification checks parity in source and published output.
This avoids a production dependency on unpublished W1 Hosting packages or on files
outside a standalone producer checkout. Moving it into a released shared browser
host remains part of the outstanding W1/W4 package-adoption follow-up.

## Verification

From `D:\Broiler\tests\office-web`, with the pinned W0 SDK/workload and Playwright
binaries installed, run `npm ci` and `./verify-w5.ps1`. It runs the existing Writer
and Cells test suites, publishes both trimmed Release WASM heads with locked
released dependencies, and tests the exact published output in Chromium,
Firefox and WebKit in fresh **normal persistent profiles**. It then independently
reads recovered Writer RTF bundles with
the native codec inspector and compares Cells JSON values.

Validated on 2026-10-29:

| Check | Result |
| --- | --- |
| Writer / Cells existing suites | 286 + 140 = **426 passed** |
| Locked trimmed Release WASM publish | Both heads succeeded; released dependencies |
| Recovery/failure/crash matrix | Chromium **156.0.8078.4**, Firefox **157.0**, WebKit **27.2** passed in normal profiles |
| Independent recovered-data inspection | Three Writer RTF text/bold results and three Cells pending-value results passed |
| Cells file/edit regressions against W5 | All three engines passed in normal profiles |
| Startup baseline against W5 | Twelve cold/warm cases and three expected missing-entry failures passed |
| Launcher integration against W5 | All three engines passed in normal profiles |

These are bounded fixture results. They do not certify every document feature,
physical browser/private mode, operating-system crash or device configuration.

`artifacts/web-w5/` contains SDK/workload inventories, resolved dependencies,
published outputs, browser report/screenshots, exported bundles and fidelity
results. Persistent test profiles are generated beneath its browser directory;
they contain fixtures only. The crash test verifies the direct child process's
parent PID and Playwright executable, then forcibly terminates only that test
browser's process tree. It does not call pagehide, beforeunload, context.close or
browser.close before termination. The profile is reopened and the last committed
Writer and uncommitted Cells-editor values are restored through the actual UI.

Coverage includes concurrent tabs, a version-prefix change, real migration commit
and rollback after successful write requests, unknown-schema export/refusal,
draft-count exhaustion, bounded debounce, lifecycle flushes, injected quota and
storage-policy failures, preference persistence, confirmed deletion/clear, and
bundle export/import after local deletion. Quota/policy failures are controlled
injections; tests do not fill the user's disk or force browser quota eviction.
Clearing/importing simulates absent storage; real eviction cannot be undone if no
separate backup remains. The automated process-termination harness currently
targets Windows. Physical Safari/mobile, OS power loss and broader document
fidelity remain W7/device acceptance work.

**Firefox non-persistent-context limitation:** during validation the pinned
Playwright/Firefox combination intermittently stalled download activation after IndexedDB stress in
non-persistent browser contexts, while fresh persistent-profile counterparts
succeeded. This was reproduced in JS-only recovery fixtures without the managed
editors. The separate diagnostic can also pass; this is not a deterministic
every-run failure. The acceptance suite
therefore uses task-created normal profiles for contracts, failure injections and
restart flows; it does not claim private/incognito download acceptance. Run
`node recovery-firefox-contexts.mjs` for the separate diagnostic and its
`artifacts/web-w5/firefox-contexts.json` report. This identifies the observed
automation/context boundary, not a proven diagnosis of every physical Firefox
private session. Real private-session acceptance remains open for W7.

For existing Cells and Launcher flow regressions against W5 artifacts, set
`OFFICE_WEB_PERSISTENT_PROFILES=1` and point `CELL_WWWROOT`/`WRITER_WWWROOT` at
`artifacts/web-w5/cell/wwwroot` and `artifacts/web-w5/writer/wwwroot`. The W0
startup baseline can still use its ordinary isolated contexts. These profile
options create fresh fixture profiles, not user profiles.

W3's outstanding Firefox RTF-download verification remains separate. W5 recovery
bundles use their own tested browser-native JSON download flow. W6 still owns
deployment/artifact promotion; signing remains deferred as requested.
