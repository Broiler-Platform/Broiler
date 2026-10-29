# Broiler Office web — W1 host/service contracts

Date: 2026-10-28. The W1 foundation is implemented and validated locally.
No package publication, public deployment, commit or push was performed.

## Launcher boundaries

`Broiler.Launcher.Browser` is a separate, platform-neutral assembly with no
reference to native `Launcher.Core`, process or filesystem services. It owns the
explicit Writer/Cell URLs, disables Browser/Plate through their capability data,
and exposes no install, BOSS, native update or executable-path capabilities.
Navigation and namespaced browser preferences are injected. It does not claim
that an HTTP app is installed or running locally. All launcher solutions include
the new project.

`NativeLauncherServices` now owns desktop factory composition. The dashboard,
settings view and shell obtain their dependencies there. The composition root,
native application discovery constructor, default install factory and default
BOSS factory reject browser execution before constructing native defaults. The
browser head must use the browser assembly, rather than passing a web launch
service into a desktop view that silently creates native installation services.

This boundary intentionally does not add the graphical web launcher—that is W2.
The existing native views and their constructor compatibility remain available
to Windows/Linux hosts and test fakes.

## Shared host packages

Two projects were added to the normal Hosting solution and packaging discovery:

- `Broiler.Hosting.Contracts`: bytes/name/MIME document port and explicit outcome,
  bounded single-thread frame dispatch and transient clipboard event scope.
- `Broiler.Hosting.WebAssembly`: browser document transport adapter, reusable
  Canvas input/lifecycle module and transitive browser asset targets.

The [host contract](../Broiler.Hosting/docs/browser-host.md) documents event
shapes, coordinates, composition ownership, synchronous trusted clipboard rules,
buffer lifetime, cancellation and download semantics. No shared package depends
on native hosting, UI controls, a desktop filesystem or process launching.

The browser adapter coalesces frames; observes container size and DPR; positions
carets in CSS pixels; handles committed text/IME event sequencing; releases held
keys, pointer capture and composition on blur/disposal; and filters synthetic
clipboard events. Clipboard accelerators remain available to trusted browser
events. Product-specific mapping to `UiInputEvent` stays in the product adapter.

The document port separates open bytes from a filesystem path and distinguishes
`DownloadInitiated` from confirmed `Saved`. Picker cancellation, concurrent
picker rejection, size limits, filename normalization and Blob export are part
of the browser transport. This does not implement application dirty-state policy
or recovery; those are still W3/W4/W5 work.

## Renderer and replay coupling

The Graphics browser package now owns its replay JS and generated manifest
(package version, protocol and SHA-256). Its transitive targets supply browser
publish assets, validate their packaged digest and reject stale app-owned replay
copies. Managed startup requires `broiler.canvas/1` before renderer initialization.
An incompatible protocol or missing export fails startup.

This compatibility mechanism does not replace release signatures. Existing
published Graphics preview.14 and the W0 editor artifacts remain unchanged. Their
old replay copies must be removed when upgrading to the new package generation.
See [Graphics packaging guidance](../Broiler.Graphics/docs/packaging.md).

## Validation

Run [the repeatable W1 verification script](../tests/office-web/verify-w1.ps1)
after the W0 SDK/workload and browser setup:

```powershell
cd D:\Broiler\tests\office-web
./verify-w1.ps1
```

| Check | Local result |
| --- | --- |
| Hosting fake-provider contracts | 10 tests passed |
| Launcher desktop/browser service tests | 184 tests passed |
| Windows launcher Release build | Passed, zero warnings/errors |
| Graphics planner/oracle suite | 32 cases passed |
| Hosting Contracts/WebAssembly and Graphics.WebAssembly packages | Local pack succeeded |
| Package positive case | Digest valid; SDK publish contains all three package-owned browser assets |
| Package negative fixtures | Corrupt replay digest and stale app replay copy both rejected |
| Managed browser sample | Passed Chromium 156.0.8078.4, Firefox 157.0, WebKit 27.2 |
| Replay negative startup | Incompatible ABI rejected in all three engines |
| Existing published Writer/Cell regression matrix | 12 positive and 3 missing-entry negative cases passed |

The browser sample asserts actual managed Unicode text state, exactly-once
composition commit, frame coalescing, caret coordinates, blur release, pointer
cancel and DPR=2 resize. File-picker bytes cross the .NET `BrowserDocumentPort`
boundary; exported file bytes and filename are checked. It rejects oversized
files and concurrent pickers, checks cancellation cleanup and disposal, and
requires no fatal console/network failures. Browser reports/screenshots are in
`artifacts/web-w1/contracts/`; W0 editor regression results remain in
`artifacts/web-w0/browser/`.

The sample uses bounded, sample-specific interop encoding. Its synthetic
composition/cancellation tests do not certify physical IME, native picker dialogs,
mobile touch, assistive technology or Safari. Those remain W7 gates. The earlier
Firefox edited-Writer reload finding from W0 is still open for W3/W7.

## Adoption order

1. Commit/review the root/Launcher, Hosting and Graphics changes through their
   normal repository process. Local pack default versions are test artifacts;
   use the existing release version resolver for the next valid previews.
2. Publish `Broiler.Hosting.Contracts` and `Broiler.Hosting.WebAssembly` together
   through Hosting's existing release workflow. Publish the Graphics package
   changes through its existing workflow.
3. W2 adds the graphical Launcher WASM head using `Broiler.Launcher.Browser` and
   pins those released host/renderer packages. Do not reintroduce native Core as
   its composition root.
4. W3/W4 pin the same tested package generation in their respective producers,
   remove app-owned replay copies, map document commands to `IDocumentPort` and
   replace duplicated page glue with the shared event adapter. Preserve each
   product's independent Documents/UI compatibility pins.

Production Writer/Cell heads are deliberately not switched to unpublished shared
source projects. The isolated contract sample uses source references solely to
make the new package behavior reviewable before publication. W1 is the reusable
foundation; complete editor input/document behavior is still W3/W4 acceptance.
