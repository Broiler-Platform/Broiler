# Broiler

Broiler is an experimental managed-code browser stack for .NET, currently being prepared
for its first preview. Its rendering lineage comes from
[HTML Renderer](https://github.com/ArthurHub/HTML-Renderer), and its JavaScript-engine
lineage comes from [Yantra JS](https://github.com/yantrajs/yantra).

> **Preview status:** APIs and behavior are unstable. Substantial portions of the project
> were developed with AI assistance. No component is human-approved for preview use until
> its linked `HUMAN_REVIEW.md` names a human reviewer, reviewed commit, evidence, and
> approval decision. The current review records are intentionally **PENDING**.

## What Broiler contains

| Component | Purpose |
|---|---|
| `Broiler.DOM` | Canonical DOM, HTML tokenization, parsing, mutation, traversal, and serialization |
| `Broiler.CSS` | CSS parsing, selectors, cascade, computed values, and serialization |
| `Broiler.Layout` | Graphics-independent CSS box-model and layout engine |
| `Broiler.Graphics` | Managed bitmap/codecs/raster core plus a Windows Direct2D backend |
| `Broiler.HTML` | Modular HTML/CSS renderer; derived in part from HTML Renderer |
| `Broiler.JS` | JavaScript parser, compiler, runtime, built-ins, and host integration; derived in part from Yantra JS |
| `Broiler.HtmlBridge` | DOM, renderer, and JavaScript integration |
| `Broiler.App` | WPF browser application |
| `Broiler.Browser.Windows` / `Broiler.Browser.Linux` / `Broiler.Browser.Android` | Platform-specific browser applications sharing `Broiler.Browser.Core` |
| `Broiler.Writer.Windows` / `Broiler.Writer.Linux` / `Broiler.Writer.WebAssembly` / `Broiler.Writer.Android` | Platform-specific Writer applications sharing `Broiler.Writer.Core` |
| `Broiler.Cli`, `Broiler.Wpt` | Rendering and web-platform-test tooling |
| `Broiler.DevConsole`, `Broiler.DevSite` | Development and diagnostics tools |

The application and integration solutions also consume the nested `Broiler.DateTime`, `Broiler.Regex`, and
`Broiler.Unicode` components through `Broiler.JS`.

## Foundations and independence

Broiler would not exist without the work of the HTML Renderer and Yantra JS contributors:

- [HTML Renderer](https://github.com/ArthurHub/HTML-Renderer) is the foundation of
  `Broiler.HTML`. Inherited material remains subject to the BSD 3-Clause License and its
  retained copyright notices.
- [Yantra JS](https://github.com/yantrajs/yantra) is the foundation of `Broiler.JS`.
  Yantra JS and Broiler.JS are distributed under the Apache License 2.0, with upstream
  history and attribution retained.

Broiler has diverged substantially and is maintained independently. It is not a
continuation, official edition, or release of either upstream project. Neither upstream
team is affiliated with, responsible for, or implied to endorse Broiler. See
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for complete provenance and license
references.

## First-preview limits

This preview is intended for evaluation, testing, and contribution—not production,
security-critical, or safety-critical use.

- The public APIs, repository layout, and persisted formats may change without notice.
- Broiler.JS is **not a security sandbox**. CLR and host capabilities must be restricted
  by the embedding application before running untrusted scripts.
- HTML, CSS, JavaScript, font, image, and Unicode-data inputs cross complex parser or
  native-interop boundaries that require focused security review.
- Passing unit, test262, WPT, or visual-comparison tests is useful evidence but does not
  prove that a component is secure or defect-free.
- Each license supplies the covered software on an “AS IS” basis, without warranties or
  conditions.

Current cross-repository work is tracked in the
[root roadmap](docs/ROADMAP.md). The [documentation index](docs/README.md)
links the current architecture notes and component-owned roadmaps.

## Human review status

Every release-facing component has a review record. A record is valid only for the exact
commit named by a human reviewer; **PENDING is not an approval**.

| Component | Review record |
|---|---|
| Broiler integration and applications | [Review](HUMAN_REVIEW.md) |
| Broiler.DOM | [Review](Broiler.DOM/HUMAN_REVIEW.md) |
| Broiler.CSS | [Review](Broiler.CSS/HUMAN_REVIEW.md) |
| Broiler.Layout | [Review](Broiler.Layout/HUMAN_REVIEW.md) |
| Broiler.Graphics | [Review](Broiler.Graphics/HUMAN_REVIEW.md) |
| Broiler.HTML | [Review](Broiler.HTML/HUMAN_REVIEW.md) |
| Broiler.JS | [Review](Broiler.JS/HUMAN_REVIEW.md) |
| Broiler.DateTime | [Review](Broiler.JS/Broiler.DateTime/HUMAN_REVIEW.md) |
| Broiler.Regex | [Review](Broiler.JS/Broiler.Regex/HUMAN_REVIEW.md) |
| Broiler.Unicode | [Review](Broiler.JS/Broiler.Unicode/HUMAN_REVIEW.md) |

The review files deliberately require a real developer to provide an identity, exact
commit, test and analysis evidence, findings, intended-use scope, decision, and
attestation. AI tools must not select or sign the decision.

## Get the source

Broiler uses recursive Git submodules:

```bash
git clone --recurse-submodules https://github.com/MaiRat/Broiler.git
cd Broiler
```

For an existing checkout:

```bash
git submodule update --init --recursive
```

## Build and test

Requirements:

- .NET 10 SDK
- Git
- Windows for WPF and Direct2D applications
- .NET Android workload, JDK 21, and Android SDK API 36 for Android applications
- Node.js only for the Broiler.HTML JavaScript-based WPT tooling

Choose the solution for the product, platform, or validation slice you are working on:

```bash
dotnet build Broiler.Windows.Browser.slnx
dotnet build Broiler.Linux.Writer.slnx
dotnet build Broiler.Android.Writer.slnx -p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
dotnet test Broiler.Tests.slnx
```

The root workspaces are deliberately split so an IDE does not load unrelated applications,
platform backends, tests, samples, generators, and benchmarks:

| Scope | Solution |
|---|---|
| Browser applications | `Broiler.Windows.Browser.slnx`, `Broiler.Linux.Browser.slnx`, `Broiler.Android.Browser.slnx` |
| Writer applications | `Broiler.Windows.Writer.slnx`, `Broiler.Linux.Writer.slnx`, `Broiler.WebAssembly.Writer.slnx`, `Broiler.Android.Writer.slnx` |
| Hosted Writer | `Broiler.Office.Server.slnx` |
| Rendering and WPT tools | `Broiler.Cli.slnx`, `Broiler.Wpt.slnx` |
| Developer tooling | `Broiler.Tooling.slnx` |
| Integration tests | `Broiler.Tests.slnx` |
| Platform tests | `Broiler.Windows.Tests.slnx`, `Broiler.Linux.Tests.slnx`, `Broiler.WebAssembly.Tests.slnx`, `Broiler.Android.Tests.slnx` |
| Performance harnesses | `Broiler.Benchmarks.slnx` |

Each root solution contains only its declared entry points and their transitive
`ProjectReference` closure. Component-owned unit tests remain in the existing component
solutions such as `Broiler.Documents/Broiler.Documents.slnx` and
`Broiler.UI/Broiler.UI.slnx`.

Solution entry points are declared in `eng/solutions.json`. Regenerate and verify the
dependency closures after changing project references:

```powershell
./scripts/update-solutions.ps1
./scripts/verify-solution-projects.ps1
```

Run the Win32/Direct2D application on Windows:

```bash
dotnet run --project src/Broiler.Browser.Windows/Broiler.Browser.Windows.csproj --configuration Debug-Windows
```

Android is currently an emulator-verified preview; physical-device validation is
still pending. Writer and Browser target API 36 with a minimum of API 24, produce
self-contained debug APKs and Release AABs, and ship `android-arm64` plus
`android-x64` for emulator use. See
[the Android architecture and remaining device gates](docs/architecture/android.md).

Each submodule README contains its standalone build and test commands. Broiler.HTML also
has repository-specific WPT tooling, while Broiler.JS documents its test262 workflow.
The [real-world website render suite](docs/real-world-render-tests.md) complements those
standards tests with visual comparisons against a small allowlist of public websites, and
the [privacy test page suite](docs/privacy-test-pages.md) tracks how much of the DuckDuckGo
privacy test corpus — storage, request paths, fingerprinting surface, HTTPS upgrades —
Broiler is able to carry out at all.

## Repository layout

```text
Broiler/
├── src/                       # integration libraries, applications, and tools
├── tests/                     # integration and WPT assets
├── docs/                      # architecture notes and roadmaps
├── Broiler.DOM/               # Git submodule
├── Broiler.CSS/               # Git submodule; contains a nested DOM checkout
├── Broiler.Layout/            # in-tree layout component
├── Broiler.Graphics/          # Git submodule
├── Broiler.HTML/              # Git submodule; contains a nested graphics checkout
├── Broiler.JS/                # Git submodule; contains DateTime, Regex, and Unicode
├── eng/solutions.json         # focused-solution entry points and platform boundaries
└── Broiler.*.slnx             # generated product/platform/test workspaces
```

## AI-assisted development

AI coding tools have produced or modified substantial portions of Broiler. Maintainers
direct the work, run automated checks, and decide what to merge. That workflow can
increase implementation throughput, but it does not make AI output trustworthy by
default. Human reviewers are expected to read the relevant source and validate the exact
release commit rather than approving an AI-generated summary.

Component READMEs disclose AI assistance and link to commit-scoped human review records.

## NuGet packages

The reusable component libraries are published as NuGet packages (the
`Broiler.Writer` and browser applications are not). Packages are versioned in
lockstep during the preview (currently `0.1.0-preview.1`, a prerelease) and are
licensed under Apache-2.0.

Install a component — for example the layout engine or the full UI toolkit:

```bash
dotnet add package Broiler.Layout --prerelease
dotnet add package Broiler.UI.All --prerelease      # meta-package: the whole UI toolkit
```

Meta-packages `Broiler.Media.All`, `Broiler.Input.All`, and `Broiler.UI.All`
pull in an entire family; individual packages (e.g. `Broiler.UI.Button`,
`Broiler.Media.Image.Managed`) are available for a leaner dependency set.
Platform-specific backends ship as their own packages (`*.Windows`, `*.Linux`,
`Broiler.Media.Video.MediaFoundation`).

Packaging is built from a single vendored `eng/Broiler.Packaging.props` per
component; see the
[release and distribution roadmap](docs/ROADMAP.md#release-and-distribution)
and [CHANGELOG.md](CHANGELOG.md). Public release is gated on the per-component
`HUMAN_REVIEW.md` records.

## License

Except where a file or third-party notice says otherwise, Broiler's current project work
is licensed under the [Apache License 2.0](LICENSE).

Inherited HTML Renderer material remains subject to its BSD 3-Clause License. Yantra JS
material remains subject to Apache-2.0, and Unicode-provided data retains its own terms.
Redistributions must preserve the applicable notices; see
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
