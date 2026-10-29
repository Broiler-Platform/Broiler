# Launcher WebAssembly head (W2)

`Broiler.WebAssembly.Launcher.slnx` contains only the browser service assembly and
the WebAssembly head. It does not reference native `Launcher.Core`. The graphical
view uses the existing Broiler.UI Standard window, labels and buttons with the
Graphics Canvas renderer, plus W1's packaged browser host adapter.

## Build dependencies

The W1 Hosting packages were not yet on NuGet when this head was implemented;
Graphics WebAssembly on NuGet still ended at preview.14. Therefore an ordinary
build intentionally requires **explicit released** `BroilerWebHostingVersion`
and `BroilerWebGraphicsVersion` properties. No guessed future package version
or unpublished source-reference fallback is used by the production project.
The head has its own central package file so the desktop pins remain unchanged.

After publishing the W1 changes through their normal release workflows, run:

```powershell
cd D:\Broiler\tests\office-web
./publish-w2.ps1 -HostingVersion '<released Hosting version>' -GraphicsVersion '<released Graphics version>'
```

Replace the placeholders with actual versions. The script verifies NuGet
availability, then validates the published replay manifest's package version,
protocol and hash. An older Graphics release without the W1 assets is rejected.
Publishing here produces local static files; it does not deploy them.

For explicit pre-release integration verification:

```powershell
./publish-w2.ps1 -LocalPackages
./publish.ps1
npm ci
npx playwright install chromium firefox webkit
npm run test:launcher
```

This packs the four changed Graphics/Hosting projects into a unique local
`0.1.0-w2check.*` package cohort and restores through an isolated cache under
`artifacts/web-w2/`. The web head still consumes **packages**, not their source
projects. UI preview.24 comes from NuGet. These generated versions must not be
published or promoted to a production deployment. `build-info.json` and the
resolved assets record the tested cohort.

Use the pinned SDK/workload documented in `tests/office-web`. Generated static
files are in `artifacts/web-w2/launcher/wwwroot/`. You can preview the launcher
locally with any static server providing JS/WASM MIME types; for example:

```powershell
python -m http.server 8080 --bind 127.0.0.1 --directory D:\Broiler\artifacts\web-w2\launcher\wwwroot
```

Its real product links always target the public product origins. They need the
W6 DNS/hosting deployment to work outside the test harness. Local browser tests
intercept those exact HTTPS origins and serve the published artifacts instead;
they do not prove that the public sites have been deployed.

## Behavior and navigation

- Writer opens `https://writer.broiler.app/`; Cell opens `https://cell.broiler.app/`.
  Neither destination is resolved from query parameters or deployment metadata.
- Browser and Plate remain visible as disabled desktop-only cards. No native
  install, update, BOSS, administrator or executable-path action is registered.
- Same-tab navigation is the default. New tabs are opt-in, have no opener, and a
  denied popup exposes an ordinary same-tab link instead of losing the action.
- Canvas mouse, managed keyboard navigation and Ctrl/Cmd+2/3 launch the apps.
  Ctrl/Cmd+1/4 do nothing. Escape moves focus to the accessible HTML app links.
- Theme and new-tab preferences use only `office.web.*` browser-storage keys.
  Storage failures leave working in-memory preferences for the current visit.
- Writer and Cell have static **Return to Office** links, available independently
  of their runtime. Their canvas layout reserves the navigation strip.
- Returning from a browser history cache reloads a disposed launcher runtime.

The HTML app links work before WASM initializes, without JavaScript, and after
entry/runtime/replay failures. A small independent bootstrap script exposes a
retry button. Optional metadata has a three-second timeout; launcher startup has
a sixty-second recovery deadline. These deadlines are recovery behavior, not a
startup-performance promise.

## Deployment metadata

`wwwroot/office-web.json` is bundled same-origin metadata, not a GitHub API lookup
or a cross-origin app-readiness probe. The default is an unapproved local preview
with no fabricated product version. The approved W6 deployment artifact can use:

```json
{
  "schemaVersion": 1,
  "status": "approved",
  "channel": "preview",
  "catalogVersion": "office-release-id",
  "products": [
    { "id": "writer", "url": "https://writer.broiler.app/", "version": "actual-writer-release" },
    { "id": "cell", "url": "https://cell.broiler.app/", "version": "actual-cell-release" }
  ]
}
```

Malformed, unsupported, duplicate, null or redirected product metadata falls
back to local-preview labels. Display versions never imply native installation
or a running process. The approval field is a deployment-pipeline convention,
not a signature or an authenticity guarantee. W6 must generate this file from
the approved catalog and publish it with the tested artifact; release signing
remains the separately deferred roadmap gate.

## Validation scope

The browser harness serves the exact published launcher, Writer and Cell bytes
through intercepted HTTPS origins and also tests a versioned launcher prefix.
It checks navigation/return, direct visits, disabled actions, preferences, storage
denial, popup success/denial, approved metadata, narrow layout, startup failures,
retry and JavaScript-disabled links in Chromium, Firefox and WebKit. It verifies
the package-owned replay hash and rejects native launcher/host libraries in the
resolved dependency graph. Generated reports/screenshots are under
`artifacts/web-w2/browser/`.

Real Safari, physical touch, assistive-technology acceptance, production CDN
headers, deployment/rollback and Internet performance remain W6/W7 work. Native
Windows/Linux heads remain on their existing build solutions and package pins.
