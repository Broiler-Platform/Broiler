# Broiler Office web — W0 baseline

Date: 2026-10-28. Scope: published Writer and Cell artifacts on Windows,
without public deployment. Reproduction instructions and the pinned toolchain
are in [tests/office-web](../tests/office-web/README.md).

## Changes

Cell's HTML now uses the SDK-generated import map and fingerprint placeholder,
matching the publishing contract used by Writer. The old `main.js` reference
pointed to an absent file after publication. The published entry is resolved
from generated HTML rather than assuming a particular fingerprint filename.

Cell also publishes its managed caret and forwards committed textarea input.
The hidden textarea follows the caret and receives focus when a text control is
active. Space is allowed through the native text-input path. This establishes a
basic editable surface through the formula bar; it does not complete Cell's
browser input adapter. W4 still owns the composition lifecycle, clipboard,
pointer cancellation, keyboard-entry behavior across the grid and document IO.
Writer had the same Space-key suppression defect; its browser adapter now lets
the textarea deliver spaces too. The harness asserts the complete committed
input string, including spaces, in addition to checking focus.

SDK 10.0.403, wasm-tools manifest 10.0.114/10.0.100, Playwright 1.64.1 and
resolved NuGet package graphs are pinned for the baseline. The publisher records
SDK/workload details and resolved assets, rejects toolchain drift, and performs
locked restore. It leaves the desktop SDK policy unchanged. This machine reports
workload inventory `10.0.400-manifests.899c35d8`; the Mono manifest selects runtime
tooling 10.0.14. Provisioning that exact workload on clean CI is still a W6
producer setup task; an unqualified workload update is not a reproducible setup.

Both artifacts require trimming and currently use the interpreter with native
relinking, not full AOT. Shared package versions include Graphics preview.14,
UI preview.23 and Media preview.16. Writer uses Documents preview.27 for its UI
binary compatibility; Cell uses Documents preview.28. They must not be upgraded
as one assumed synchronized cohort.

## Browser evidence

The harness tests exact publish outputs beneath versioned prefixes, with MIME
types and Brotli compression. The matrix includes Chromium 156.0.8078.4,
Firefox 157.0 and Playwright WebKit 27.2. All six app/engine combinations reach
managed ready and accept basic committed text through a focused textarea.
Cold and warm runs produce twelve screenshots. Cell's Chromium screenshot was
visually checked: `W0 baseline` appears in A1 and the formula bar, including its
space. Screenshots and machine-readable results are generated in
`artifacts/web-w0/browser/`.
Writer's Chromium screenshot was also checked after the fix: the rendered text
and format-code view both contain `W0 baseline` with its space.

Observed reference startup is approximately 0.3–0.9 seconds on this Windows
machine over unthrottled loopback. Cold resource transfer is approximately
2.84 MB for Writer and 2.27 MB for Cell. These include Resource Timing overhead
and exclude the HTML navigation. Warm runs use the same context/cache in a new
tab; Chromium/Firefox normally avoid resource transfer, while WebKit may fetch
again. This is not a supported-device or production performance budget.

The missing-entry negative fixture must prevent ready in every engine. Positive
tests reject missing requests, fatal console errors, page exceptions and managed
error panels. Entry/import-map target existence and SDK integrity hashes are
validated before browser startup. The replay module stays at its stable URL
inside each app's version prefix. Both artifacts currently use SHA-256
`b8ebbb1db889811a36e99d3dc44e17891e542ccd088a6c9e6ff3ae6c87e684f1`.
That hash and successful rendering establish this tested pairing, not a general
ABI guarantee for later Graphics packages; W1/W6 must preserve version coupling.

An earlier test that reused an edited Writer page timed out navigating in
Firefox, even with an accepted leave-page dialog. Fresh-tab warm startup passes.
Keep edited-document reload/unload behavior in W3/W7 regression testing; it has
not been declared fixed by changing the timing method.

## Platform audit and remaining gates

The browser Cell project builds successfully with `-p:NoWarn=` and no CA1416
suppression. This is a useful compile-time check, not complete transitive
reachability analysis of precompiled NuGet packages.

| Reachable path | Finding | Follow-up |
| --- | --- | --- |
| Cell File/Open and Save commands | Shared `StandardFileDialog`, `File.Exists`, path-based CSV load/save remain reachable. The browser open-file callback is empty. Browser filesystem paths do not select user files. | W1 document port; W4 CSV adapter |
| Cell website navigation | Browser supplies JS navigation. Desktop fallback checks Windows/Linux/macOS before `Process.Start`. | Retain guard and browser callback in W1 |
| Cell About rendering | Existing browser guard suppresses a rotated close button to avoid costly software rendering fallback. | W7 rendering/performance coverage |
| Native hosting/input packages in package graph | Some managed dependency graphs contain desktop-related packages; merely restoring them does not prove native code executes in the browser. Startup exercises the managed canvas path successfully. | W1 boundary audit; W7 broader command coverage |
| Writer edited reload | Firefox timeout observed in the initial reload probe. | W3/W7 regression case |
| Save/recovery | No durable browser recovery established here. Downloads are not recovery or confirmed durable writes. | W3/W4/W5 |

W0 provides a reproducible published-artifact baseline and basic editable-surface
evidence. Broader editing semantics and production browser readiness remain gated
by W1–W7. The harness does not assert exported document values or full round-trip
correctness, and no Safari, mobile, accessibility, full-AOT or throttled-network
support claim follows from this run. Ratify performance budgets on an agreed
reference device/network in W7 using this measurement harness as the starting point.
