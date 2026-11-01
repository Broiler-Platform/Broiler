# Local WebAssembly static-asset audit

The package Content/Link pattern diagnosed in Writer also affects Cells,
Launcher and the Office web contract sample. Published archives can work while
local DevServer fails: its manifest resolves the external files under the app's
wwwroot, producing empty responses and import-map integrity errors.

| Consumer | Result | Correction |
| --- | --- | --- |
| Writer | Hosting and Graphics NuGet assets | Existing BrowserPackageAssets.targets compatibility registration |
| Cells | Graphics NuGet assets | Register the real NuGet content root, with generated package path property |
| Launcher | Hosting and Graphics NuGet assets | Same compatibility registration; supports early Graphics packages without replay metadata |
| Office contract sample | Linked Hosting and Graphics source modules | Register the actual source directories as computed asset roots |
| UI WebAssembly demo | Module copied into its own wwwroot | No matching external-root defect found in source inspection |
| VM WebAssembly libraries/compositions | No served browser module content | No matching static-web-asset registration |
| Office Server | Serves the published Writer bundle, not the app DevServer manifest | Not the same path; Writer publish validation retained |

The Hosting and Graphics producer targets were corrected separately so future
NuGet consumers receive computed asset registrations automatically. App fixes
use package path properties rather than absolute machine paths, retain module
fingerprinting and SRI, and do not copy package modules into tracked wwwroot.
Cells' unrelated pending dependency/clipboard changes were preserved.

Validation: Cells, Launcher (Hosting preview.13 / Graphics preview.16) and the
contract sample built in Debug with zero warnings/errors and booted in Chromium
against their real local DevServer. Module responses had status 200, JavaScript
MIME types, nonempty bodies and hashes matching the generated import maps.
Other rows above are source inspection, not full platform acceptance tests.
Stop and restart existing debug sessions after rebuilding; their asset manifests
are loaded at server startup. No release was published during this audit.
