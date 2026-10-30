# Broiler NuGet dependency refresh — 2026-10-29

Checked NuGet.org V3 package version indexes directly, including preview releases. Scanned 46 source NuGet configuration files (excluding generated outputs and unrelated agent worktrees). Feed configuration files contain no Broiler version pins; the actual dependency pins are centrally managed.

Updated 167 dependency pins/default version properties in 13 central package files. Non-Broiler direct package pins and feed settings are unchanged.

| Central package file | Updated values |
| --- | --- |
| `Broiler.Browser/Directory.Packages.props` | 14 |
| `Broiler.Cells/Directory.Packages.props` | 13 |
| `Broiler.Code/Directory.Packages.props` | 27 |
| `Broiler.Fond/Directory.Packages.props` | 2 |
| `Broiler.Hosting/Directory.Packages.props` | 21 |
| `Broiler.HtmlBridge/Directory.Packages.props` | 4 |
| `Broiler.JS/Directory.Packages.props` | 1 |
| `Broiler.Launcher/Directory.Packages.props` | 3 |
| `Broiler.Launcher/src/Broiler.Launcher.WebAssembly/Directory.Packages.props` | 1 |
| `Broiler.Mail/Directory.Packages.props` | 32 |
| `Broiler.Plate/Directory.Packages.props` | 6 |
| `Broiler.UI/Directory.Packages.props` | 20 |
| `Broiler.Writer/Directory.Packages.props` | 23 |

All resolvable published Broiler package references in the scanned central files match their latest NuGet.org versions. Hosting.Contracts and Hosting.WebAssembly still return HTTP 404; their explicit unpublished placeholder remains. The WebAssembly Graphics default now names the actual latest published version; this does not imply it supplies the unpublished W1 generation.

The JSeal package-consumer fixture takes a caller-selected package version. Unicode.ReferenceArms preserves exact historical engine dependencies for its comparison evidence. These test inputs were retained.

Writer and Cell dependency locks were regenerated using SDK 10.0.403 with published UI packages and locked mode disabled only during lock generation. Their normal release workflows retain locked restore.

Detailed before/after values and fetched NuGet version indexes are recorded locally under `artifacts/nuget-update/`. No commits or package publication were performed.

Validation: Writer and Cell Release WebAssembly builds passed with zero warnings/errors; 195 Launcher Release tests passed.
