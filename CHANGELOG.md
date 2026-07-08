# Changelog

All notable changes to the Broiler component packages are documented here. The
format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the
packages use [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Packages
are versioned in lockstep during the preview.

## [Unreleased]

## [0.1.0-preview.1] — first preview

First packaged preview of the Broiler component libraries. **APIs are unstable**
and this preview is for evaluation, testing, and contribution — not production.

### Added

- NuGet packaging for the reusable component libraries (the `Broiler.Writer` and
  browser applications are not packaged):
  - `Broiler.DOM` — `Broiler.Dom`, `Broiler.Dom.Html`
  - `Broiler.CSS` — `Broiler.CSS`, `Broiler.CSS.Dom`
  - `Broiler.Layout`
  - `Broiler.Graphics` — core plus Direct2D / Linux / OpenGL / Vulkan backends
  - `Broiler.HTML` — renderer libraries
  - `Broiler.JS` — engine plus `Broiler.DateTime`, `Broiler.Regex`, Unicode data
  - `Broiler.Media` — core, audio, image, video (+ managed implementations)
  - `Broiler.Input` — device contracts and platform backends
  - `Broiler.UI` — retained-mode toolkit (control contracts + Standard implementations)
  - `Broiler.Documents` — model, RTF, DOCX, HTML, Markdown codecs
- Convenience meta-packages: `Broiler.Media.All`, `Broiler.Input.All`, `Broiler.UI.All`.
- Each package ships an icon, README, Apache-2.0 license expression, XML
  documentation, a symbol package (`.snupkg`), and SourceLink metadata.

### Notes

- Packages are licensed under Apache-2.0. `Broiler.HTML` and `Broiler.JS` include
  `THIRD_PARTY_NOTICES.md` (HTML Renderer BSD-3-Clause; Yantra JS Apache-2.0).
- Public release to NuGet.org is gated on the per-component `HUMAN_REVIEW.md`
  records; `Broiler.JS` review is still pending.

[Unreleased]: https://github.com/Broiler-Platform/Broiler/compare/nuget-v0.1.0-preview.1...HEAD
[0.1.0-preview.1]: https://github.com/Broiler-Platform/Broiler/releases/tag/nuget-v0.1.0-preview.1
