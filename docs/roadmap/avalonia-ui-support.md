# AvaloniaUI Support Roadmap

A comprehensive plan for integrating [AvaloniaUI](https://avaloniaui.net/)
support into **Broiler**, enabling cross-platform desktop rendering on Windows,
macOS, and Linux while preserving the existing WPF-based application.

---

## Table of Contents

1. [Motivation](#motivation)
2. [Scope](#scope)
3. [Current Architecture](#current-architecture)
4. [Required Architectural Changes](#required-architectural-changes)
5. [Dependencies & Potential Conflicts](#dependencies--potential-conflicts)
6. [Phased Implementation](#phased-implementation)
   - [Phase 1 – Foundation & Adapter Layer](#phase-1--foundation--adapter-layer)
   - [Phase 2 – Avalonia Application Shell](#phase-2--avalonia-application-shell)
   - [Phase 3 – Feature Parity & Testing](#phase-3--feature-parity--testing)
   - [Phase 4 – Polish & Release](#phase-4--polish--release)
7. [Resource & Skill Requirements](#resource--skill-requirements)
8. [Stakeholders](#stakeholders)
9. [Documentation Requirements](#documentation-requirements)
10. [Deliverables & Milestones](#deliverables--milestones)

---

## Motivation

Broiler currently targets **WPF** (`net8.0-windows`), which limits the desktop
application to Windows. AvaloniaUI is a mature, cross-platform .NET UI
framework with XAML syntax familiar to WPF developers and support for Windows,
macOS, and Linux. Adopting AvaloniaUI will:

- **Enable cross-platform use** — contributors and users on macOS/Linux can run
  Broiler without a Windows VM.
- **Broaden the contributor base** — developers on non-Windows platforms can
  build, test, and debug the full GUI application.
- **Leverage existing abstractions** — the HtmlRenderer adapter layer already
  defines platform-agnostic rendering interfaces (`RGraphics`, `RFont`,
  `RBrush`, etc.) that map naturally to an Avalonia implementation.
- **Future-proof the project** — AvaloniaUI has growing community adoption and
  active development, providing a sustainable cross-platform UI path.

---

## Scope

| In Scope | Out of Scope (for now) |
|----------|------------------------|
| AvaloniaUI adapter for HtmlRenderer (`HtmlRenderer.Avalonia`) | Removing or deprecating the existing WPF application |
| Cross-platform Avalonia application shell (`Broiler.Avalonia`) | Mobile (iOS/Android) or WebAssembly targets |
| Porting MainWindow navigation and rendering surface | Full DevTools rewrite (parity with Phase 2+ of dev-console roadmap) |
| Porting DevConsole panel to Avalonia | GPU-accelerated custom rendering backend |
| CI builds and test runs on Linux and macOS | Browser-engine-level rewrite of HtmlRenderer internals |
| Updated documentation and build instructions | |

---

## Current Architecture

Broiler's rendering pipeline is already split into platform-agnostic and
platform-specific layers:

```
┌──────────────────────────────────────────────────────────┐
│                    Broiler.App (WPF)                     │
│  MainWindow.xaml ─► HtmlPanel ─► HtmlRenderer.WPF       │
│                                    │                     │
│  DevConsolePanel.xaml               │                     │
│                                    │                     │
│  Rendering Pipeline ◄──────────────┘                     │
│  (platform-agnostic)                                     │
│    RenderingPipeline, DomBridge, ScriptEngine,            │
│    PageLoader, ScriptExtractor, HtmlPostProcessor         │
└───────────────────────────┬──────────────────────────────┘
                            │
              ┌─────────────▼──────────────┐
              │    HtmlRenderer.Adapters   │
              │  (abstract: RGraphics,     │
              │   RFont, RBrush, RPen,     │
              │   RImage, RGraphicsPath)   │
              └─────────────┬──────────────┘
                            │
              ┌─────────────▼──────────────┐
              │    HtmlRenderer.WPF        │
              │  (WPF implementations:     │
              │   GraphicsAdapter,         │
              │   FontAdapter, etc.)       │
              └────────────────────────────┘
```

### Key Observations

1. **Rendering pipeline is platform-neutral** — `src/Broiler.App/Rendering/`
   (33 files) contains no `System.Windows` references. It operates on HTML
   strings and DOM trees via YantraJS.

2. **Adapter abstractions exist** — `HtmlRenderer.Adapters` defines abstract
   classes (`RGraphics`, `RFont`, `RFontFamily`, `RBrush`, `RPen`, `RImage`,
   `RGraphicsPath`, `IResourceFactory`) that decouple rendering from any
   specific UI framework.

3. **WPF coupling is localised** — approximately 1,500 lines of WPF-specific
   code spread across:
   - `HtmlRenderer.WPF/` (18 files, ~715 lines) — adapter implementations and
     controls (`HtmlPanel`, `HtmlControl`, `HtmlLabel`).
   - `MainWindow.xaml` / `MainWindow.xaml.cs` — application shell and
     navigation.
   - `DevConsolePanel.xaml` / `DevConsolePanel.xaml.cs` — developer console UI.

4. **Broiler.Cli already runs cross-platform** — the CLI project targets
   `net8.0` without WPF and shares rendering code via `<Compile Include>`
   links, proving the core pipeline works outside WPF.

---

## Required Architectural Changes

### 1. HtmlRenderer.Avalonia adapter project

Create a new project `HtmlRenderer.Avalonia` that implements the existing
adapter abstractions using Avalonia's rendering API:

| Abstract Class | WPF Implementation | Avalonia Equivalent |
|----------------|-------------------|---------------------|
| `RGraphics` | `GraphicsAdapter` (DrawingContext) | `DrawingContext` from `Avalonia.Media` |
| `RFont` | `FontAdapter` (Typeface + FormattedText) | `Typeface` + `FormattedText` / `GlyphRun` |
| `RFontFamily` | `FontFamilyAdapter` | `Avalonia.Media.FontFamily` |
| `RBrush` | `BrushAdapter` (SolidColorBrush) | `Avalonia.Media.ISolidColorBrush` |
| `RPen` | `PenAdapter` | `Avalonia.Media.Pen` |
| `RImage` | `ImageAdapter` (BitmapSource) | `Avalonia.Media.Imaging.Bitmap` |
| `RGraphicsPath` | `GraphicsPathAdapter` (PathGeometry) | `Avalonia.Media.PathGeometry` / `StreamGeometry` |
| `IResourceFactory` | via `WpfAdapter` | via `AvaloniaAdapter` |

### 2. Avalonia HtmlPanel control

Port `HtmlControl` / `HtmlPanel` to an Avalonia `Control`. Key differences:

- Replace `DependencyProperty` with Avalonia `StyledProperty` or
  `DirectProperty`.
- Replace `OnRender(DrawingContext)` with Avalonia's `Render(DrawingContext)`.
- Replace WPF `MeasureOverride` / `ArrangeOverride` with Avalonia's equivalents.
- Replace WPF `ScrollViewer` integration with Avalonia's `ScrollViewer`.

### 3. Broiler.Avalonia application project

Create `src/Broiler.Avalonia/` targeting `net8.0` (no `-windows` TFM):

- Port `MainWindow.xaml` to Avalonia `.axaml`.
- Replace `DispatcherTimer` (WPF) with `Avalonia.Threading.DispatcherTimer`.
- Reference `HtmlRenderer.Avalonia` instead of `HtmlRenderer.WPF`.
- Share rendering pipeline code using the same `<Compile Include>` link
  pattern used by `Broiler.Cli`.

### 4. DevConsole Avalonia port

Port `DevConsolePanel.xaml` / `.xaml.cs` to Avalonia:

- Replace WPF `UserControl` with Avalonia `UserControl`.
- Replace `Dispatcher.BeginInvoke` with `Avalonia.Threading.Dispatcher`.
- Replace WPF `Canvas` / `Shapes` with Avalonia equivalents.

### 5. Solution and build updates

- Add `HtmlRenderer.Avalonia` and `Broiler.Avalonia` projects to
  `Broiler.slnx`.
- Update `Directory.Build.props` if shared properties need adjustment.
- Add CI matrix entries for Linux and macOS builds (Avalonia project only).

---

## Dependencies & Potential Conflicts

### New Dependencies

| Dependency | Version | Purpose |
|------------|---------|---------|
| `Avalonia` | 11.x | Core UI framework |
| `Avalonia.Desktop` | 11.x | Desktop platform support |
| `Avalonia.Themes.Fluent` | 11.x | Default theme (Fluent design) |

### Potential Conflicts

| Area | Risk | Mitigation |
|------|------|------------|
| **System.Drawing.Common** | `HtmlRenderer.Adapters` depends on `System.Drawing.Common` for `Color`, `SizeF`, `PointF`, `RectangleF`. On non-Windows platforms, `System.Drawing.Common` has limited support (deprecated on Linux/macOS in .NET 7+). | Introduce platform-neutral primitive types or use Avalonia's built-in `Color`, `Size`, `Point`, `Rect` in the Avalonia adapter layer. Where `System.Drawing` types cross the adapter boundary, add conversion helpers. |
| **Font metrics** | WPF and Avalonia may produce different text measurements for the same font, leading to layout differences. | Establish reference images per platform; accept minor pixel-level variance in cross-platform tests. |
| **Clipboard / context menu** | WPF clipboard and context menu helpers are Windows-specific. | Avalonia provides its own clipboard and context menu APIs. Implement Avalonia-specific versions. |
| **DispatcherTimer behaviour** | WPF `DispatcherTimer` with `DispatcherPriority.Render` may have slightly different timing semantics than Avalonia's `DispatcherTimer`. | Verify animation frame-stepping (InteractiveSession) works correctly under Avalonia's dispatcher. |
| **Image formats** | WPF uses `BitmapSource` / `BitmapImage`; Avalonia uses `Avalonia.Media.Imaging.Bitmap`. | Implement `ImageAdapter` around Avalonia's bitmap types. Support the same HTTP-based image loading in `ImageDownloader`. |
| **XAML dialect** | WPF XAML and Avalonia `.axaml` are similar but not identical (namespace URIs, attached properties, styling syntax differ). | Port XAML manually; do not attempt automated conversion. |

---

## Phased Implementation

### Phase 1 – Foundation & Adapter Layer

**Objective**: Build the HtmlRenderer adapter for Avalonia and validate basic
HTML/CSS rendering.

- [x] Create `HtmlRenderer.Avalonia` project targeting `net8.0`.
- [x] Implement `AvaloniaAdapter` (top-level adapter factory).
- [x] Implement `GraphicsAdapter` using Avalonia `DrawingContext`.
- [x] Implement `FontAdapter` and `FontFamilyAdapter` using Avalonia typography.
- [x] Implement `BrushAdapter`, `PenAdapter`, and `ImageAdapter`.
- [x] Implement `GraphicsPathAdapter` using Avalonia `StreamGeometry`.
- [x] Create a minimal Avalonia `HtmlControl` that renders static HTML.
- [x] Verify rendering of Acid1 reference page in an Avalonia test harness.
- [x] Resolve any `System.Drawing.Common` cross-platform issues.

### Phase 2 – Avalonia Application Shell

**Objective**: Port the Broiler application shell to Avalonia with navigation
and script execution.

- [x] Create `src/Broiler.Avalonia/` project targeting `net8.0`.
- [x] Port `MainWindow.axaml` with navigation bar (Back, Forward, Refresh, URL
      bar, Go).
- [x] Implement `HtmlPanel` Avalonia control with scroll support.
- [x] Link rendering pipeline files (`RenderingPipeline`, `DomBridge`,
      `ScriptEngine`, etc.) via `<Compile Include>`.
- [x] Implement `DispatcherTimer`-based animation frame stepping.
- [x] Verify basic page loading and JavaScript execution.
- [x] Port `DevConsolePanel` to Avalonia (log viewer, DOM inspector, JS
      console).
- [x] Add application entry point (`Program.cs` / `App.axaml`).

### Phase 3 – Feature Parity & Testing

**Objective**: Achieve feature parity with the WPF application and establish
cross-platform CI.

- [x] Run Acid1 and Acid2 compliance tests against the Avalonia renderer.
- [x] Establish platform-specific reference images for differential tests.
- [x] Add CI matrix entries for Linux and macOS (GitHub Actions).
- [x] Verify `InteractiveSession` / timer-driven rendering works on all
      platforms.
- [x] Test image loading (HTTP, data-URI, file://) across platforms.
- [x] Port clipboard and context menu functionality.
- [x] Address font-metric differences (document acceptable variance).
- [x] Run the full `Broiler.Cli.Tests` suite on Linux/macOS to confirm
      rendering pipeline cross-platform correctness.

### Phase 4 – Polish & Release

**Objective**: Finalize the Avalonia application for production use.

- [ ] Align keyboard shortcuts and accelerators with platform conventions
      (Cmd vs. Ctrl on macOS).
- [ ] Add platform-native window chrome / title bar styling.
- [ ] Publish platform-specific builds (self-contained for Windows, macOS,
      Linux).
- [ ] Update README and documentation with Avalonia build/run instructions.
- [ ] Create installer/package definitions (`.deb`, `.dmg`, `.msix`).
- [ ] Performance benchmarking: compare Avalonia vs. WPF rendering on Windows.

---

## Resource & Skill Requirements

| Skill | Required For | Priority |
|-------|-------------|----------|
| AvaloniaUI development (`.axaml`, controls, styling) | Application shell and DevConsole port | High |
| Avalonia `DrawingContext` / custom rendering | HtmlRenderer adapter layer | High |
| Cross-platform .NET (runtime differences, font handling) | CI and testing on Linux/macOS | Medium |
| WPF knowledge (existing codebase familiarity) | Understanding current implementation for porting | Medium |
| GitHub Actions CI/CD | Multi-platform build matrix | Medium |
| Typography / font metrics | Cross-platform text measurement consistency | Low–Medium |

### Estimated Effort

| Phase | Estimated Effort |
|-------|-----------------|
| Phase 1 – Foundation & Adapter Layer | Medium (adapter implementations are straightforward given existing abstractions) |
| Phase 2 – Avalonia Application Shell | Medium–Large (XAML porting, control lifecycle differences) |
| Phase 3 – Feature Parity & Testing | Medium (mostly testing and platform-specific fixes) |
| Phase 4 – Polish & Release | Small–Medium (packaging, documentation) |

---

## Stakeholders

| Stakeholder | Role | Interest |
|-------------|------|----------|
| **Core maintainers** | Architecture review, merge approval | Ensure Avalonia integration does not regress WPF functionality |
| **HtmlRenderer contributors** | Adapter API review | Validate that the adapter abstraction layer is sufficient |
| **Cross-platform users / contributors** | Early testing, feedback | Provide real-world usage on macOS and Linux |
| **AvaloniaUI community** | Guidance on best practices | Rendering API usage, performance recommendations |
| **CI / DevOps** | Build pipeline updates | Multi-platform GitHub Actions matrix |

---

## Documentation Requirements

| Document | Location | Description |
|----------|----------|-------------|
| This roadmap | `docs/roadmap/avalonia-ui-support.md` | Overall plan and milestones |
| ADR: AvaloniaUI Adoption | `docs/adr/` (new) | Records the decision to adopt Avalonia, alternatives considered, and trade-offs |
| Avalonia Build Guide | README (updated) | Cross-platform build and run instructions |
| Platform Differences | `docs/` (new) | Documents known rendering differences between WPF and Avalonia (font metrics, pixel variance) |
| Adapter Implementation Guide | `HTML-Renderer-1.5.2/Source/HtmlRenderer.Avalonia/README.md` (new) | Guide for implementing and extending HtmlRenderer adapters |

---

## Deliverables & Milestones

| Phase | Key Deliverable | Success Criteria |
|-------|----------------|------------------|
| **1 – Foundation** | `HtmlRenderer.Avalonia` project with full adapter implementations | Acid1 page renders correctly in an Avalonia test harness |
| **2 – Application Shell** | `Broiler.Avalonia` application with navigation and script execution | Can browse to a URL, render HTML/CSS, and execute JavaScript |
| **3 – Feature Parity** | Cross-platform CI and compliance tests | Acid1/Acid2 tests pass on Windows, Linux, and macOS |
| **4 – Polish** | Production-ready cross-platform builds | Installable packages for all three platforms with documentation |

Each phase should be delivered incrementally with its own set of issues,
reviewed in pull requests, and accompanied by the documentation listed above.
The existing WPF application (`Broiler.App`) will continue to be maintained
alongside the Avalonia application until the community decides on a
consolidation strategy.
