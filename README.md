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
| `Broiler.App.Graphics` | Win32/Direct2D preview browser application |
| `Broiler.Cli`, `Broiler.Wpt` | Rendering and web-platform-test tooling |
| `Broiler.DevConsole`, `Broiler.DevSite` | Development and diagnostics tools |

The solution also consumes the nested `Broiler.DateTime`, `Broiler.Regex`, and
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

Current implementation and compliance work is tracked in the
[public-preview roadmap](docs/roadmap/public-preview-roadmap.md),
[engine standards roadmap](docs/roadmap/engines-standards-and-performance-roadmap.md),
[refactor gap register](docs/roadmap/refactor-gap.md),
[DOM plan](docs/roadmap/broiler-dom-component.md),
[CSS plan](docs/roadmap/broiler-css-component.md), and
[layout plan](docs/roadmap/broiler-layout-component.md).

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
- Node.js only for the Broiler.HTML JavaScript-based WPT tooling

Build and test the main solution:

```bash
dotnet build Broiler.slnx
dotnet test Broiler.slnx
```

Run the WPF application on Windows:

```bash
dotnet run --project src/Broiler.App/Broiler.App.csproj
```

Run the Win32/Direct2D application on Windows:

```bash
dotnet run --project src/Broiler.App.Graphics/Broiler.App.Graphics.csproj
```

Each submodule README contains its standalone build and test commands. Broiler.HTML also
has repository-specific WPT tooling, while Broiler.JS documents its test262 workflow.

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
└── Broiler.slnx
```

## AI-assisted development

AI coding tools have produced or modified substantial portions of Broiler. Maintainers
direct the work, run automated checks, and decide what to merge. That workflow can
increase implementation throughput, but it does not make AI output trustworthy by
default. Human reviewers are expected to read the relevant source and validate the exact
release commit rather than approving an AI-generated summary.

Component READMEs disclose AI assistance and link to commit-scoped human review records.

## License

Except where a file or third-party notice says otherwise, Broiler's current project work
is licensed under the [Apache License 2.0](LICENSE).

Inherited HTML Renderer material remains subject to its BSD 3-Clause License. Yantra JS
material remains subject to Apache-2.0, and Unicode-provided data retains its own terms.
Redistributions must preserve the applicable notices; see
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
