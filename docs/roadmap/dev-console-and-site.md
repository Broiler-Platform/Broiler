# Development Console & Development Site Integration Roadmap

A comprehensive plan for adding an integrated development console and a
dedicated development site to **Broiler** (and, where required,
**HTML-Renderer**). The goal is to enable in-depth investigation and debugging
of rendering issues, provide developer tooling, and enhance the overall
developer experience.

---

## Table of Contents

1. [Motivation](#motivation)
2. [Scope](#scope)
3. [Feature Overview](#feature-overview)
4. [Integration Points](#integration-points)
5. [Phased Implementation](#phased-implementation)
   - [Phase 1 – Foundation](#phase-1--foundation)
   - [Phase 2 – Core Developer Console](#phase-2--core-developer-console)
   - [Phase 3 – Development Site](#phase-3--development-site)
   - [Phase 4 – Advanced Tooling](#phase-4--advanced-tooling)
6. [Developer Workflow](#developer-workflow)
7. [Extensibility](#extensibility)
8. [Documentation Requirements](#documentation-requirements)
9. [Deliverables & Milestones](#deliverables--milestones)

---

## Motivation

Broiler currently has a structured in-memory logger (`RenderLogger`) that
captures HTML-Renderer and JavaScript diagnostics, but there is no user-facing
surface for viewing those logs, inspecting the DOM tree, profiling layout
performance, or debugging rendering issues interactively. Developers diagnosing
CSS compliance gaps (e.g., during Acid2 work) or JavaScript integration
problems must rely on IDE debug output and manual code inspection.

An integrated **development console** within `Broiler.App` and a standalone
**development site** will:

- Shorten the feedback loop when investigating rendering bugs.
- Provide live visibility into the DOM, CSS box model, and JS execution.
- Offer a self-service diagnostic environment that does not require an IDE.
- Serve as a regression-testing and demo surface for contributors.

---

## Scope

| In Scope | Out of Scope (for now) |
|----------|------------------------|
| In-app developer console panel (Broiler.App) | Full Chromium-style DevTools reimplementation |
| Dedicated development/test website served locally | Network-level request interception/proxy |
| DOM tree inspection | GPU/compositor debugging |
| CSS computed-style viewer | Source-map support for external JS |
| JavaScript `console.*` API integration | Remote debugging protocol |
| Error overlay for rendering and script errors | |
| Performance profiling for layout/paint passes | |
| CLI diagnostic flags (`Broiler.Cli`) | |

---

## Feature Overview

### Development Console (in-app panel)

| Feature | Description |
|---------|-------------|
| **Log Viewer** | Display `RenderLogger` entries with level/category filtering, search, and auto-scroll. |
| **DOM Inspector** | Collapsible tree view of the current DOM. Selecting a node highlights it in the render surface and shows its computed styles and box-model metrics. |
| **Computed Styles Panel** | List resolved CSS property values for the selected element, grouped by category (layout, text, visual, etc.). |
| **Box Model Visualiser** | Interactive diagram showing margin, border, padding, and content dimensions for the selected element. |
| **JavaScript Console** | REPL powered by YantraJS. Supports `console.log/warn/error/info`, expression evaluation, and result display. |
| **Error Overlay** | Transparent overlay rendered on the page surface that highlights elements with rendering errors or unhandled exceptions. |
| **Performance Profiler** | Timeline view of layout and paint durations per frame. Flamegraph or bar-chart breakdown by CSS box. |

### Development Site (local web application)

| Feature | Description |
|---------|-------------|
| **Test Case Runner** | Execute Acid1, Acid2, and CSS2 chapter tests with pixel-diff results displayed inline. |
| **Side-by-Side Comparison** | Render a page in Broiler and show a reference screenshot (e.g., Chromium) side by side, with highlighted differences. |
| **Snippet Playground** | Editable HTML/CSS/JS pane with live Broiler rendering preview (similar to CodePen). |
| **Compliance Dashboard** | Aggregated CSS2 chapter checklist status pulled from the `css2/` Markdown files. |
| **API Documentation** | Auto-generated reference for the `DomBridge` DOM API surface. |

---

## Integration Points

### Broiler.App

- **MainWindow / Shell**: Add a toggleable console panel (e.g., docked bottom
  or side pane, toggled via `F12` or a menu item).
- **RenderLogger**: Expose an observable event stream (`IObservable<LogEntry>`
  or similar) so the console UI can subscribe to live log updates in addition
  to the existing in-memory list.
- **RenderingPipeline**: Instrument `PerformLayout` and `PerformPaint` calls
  with timing markers that feed into the performance profiler.
- **DomBridge**: Extend with `console.*` API registration on the `JSContext`
  so that JavaScript `console.log()` calls surface in the console panel.

### HTML-Renderer

- **CssBox / CssBoxProperties**: Expose read-only accessors or a visitor
  interface that the DOM Inspector can use to walk the box tree and read
  computed styles without modifying rendering state.
- **CssLayoutEngine**: Add optional timing hooks (start/end events per box)
  for performance profiling.
- **PaintWalker**: Add an optional highlight-overlay paint pass that draws
  selection rectangles and error indicators controlled by the console.

### Broiler.Cli

- Add `--diagnostics` flag to emit structured JSON log output suitable for
  piping to external tools or the development site.
- Add `--profile` flag to output layout/paint timing data.

### Development Site

- Implemented as a lightweight ASP.NET Core or static site project
  (`src/Broiler.DevSite` or similar).
- Communicates with a headless Broiler rendering service via HTTP or a shared
  library to render snippets and capture results.

---

## Phased Implementation

### Phase 1 – Foundation

**Objective**: Establish the infrastructure required by later phases.

- [ ] Create `Broiler.DevConsole` project (class library) for shared console
      logic (log subscription, DOM query helpers, profiling data model).
- [ ] Extend `RenderLogger` with an observable event API for live log
      streaming.
- [ ] Register `console.log`, `console.warn`, `console.error`, and
      `console.info` on the YantraJS `JSContext` via `DomBridge`, routing
      output to `RenderLogger` under the `JavaScript` category.
- [ ] Add `--diagnostics` flag to `Broiler.Cli` for structured JSON log
      output.
- [ ] Define a `BoxTreeVisitor` or read-only accessor API on `CssBox` for
      external tree traversal.

### Phase 2 – Core Developer Console

**Objective**: Ship an interactive in-app console panel in `Broiler.App`.

- [ ] Add a dockable console panel to the WPF shell (toggle with `F12`).
- [ ] **Log Viewer** tab: bind to `RenderLogger` observable stream with
      level/category filters and search.
- [ ] **DOM Inspector** tab: render the box tree using the visitor API from
      Phase 1. Highlight the selected element on the render surface.
- [ ] **Computed Styles** pane: display resolved CSS properties for the
      selected box.
- [ ] **Box Model** pane: visualise margin/border/padding/content dimensions.
- [ ] **JS Console** tab: REPL input field that evaluates expressions via
      `JSContext` and displays results and `console.*` output.
- [ ] **Error Overlay**: draw error indicators on the render surface for boxes
      that encountered layout or paint exceptions.

### Phase 3 – Development Site

**Objective**: Provide a browser-accessible test and demo environment.

- [ ] Create `src/Broiler.DevSite` project (ASP.NET Core Razor Pages or
      Blazor).
- [ ] **Test Case Runner**: execute Acid1/Acid2 tests headlessly and display
      pixel-diff results.
- [ ] **Side-by-Side Comparison**: upload or select an HTML file, render via
      Broiler, and compare with a reference image.
- [ ] **Snippet Playground**: editable pane with live preview using the
      headless rendering service.
- [ ] **Compliance Dashboard**: parse `css2/*.md` checklists and show
      aggregate completion percentages.
- [ ] **API Docs**: auto-generate `DomBridge` API reference from XML doc
      comments.

### Phase 4 – Advanced Tooling

**Objective**: Deliver performance profiling and power-user features.

- [ ] **Performance Profiler**: instrument `CssLayoutEngine` and
      `PaintWalker` with timing hooks; display timeline/flamegraph in the
      console panel.
- [ ] **Layout Diff**: compare box-tree snapshots before and after a code
      change to highlight layout regressions.
- [ ] **Export/Import**: export console state (logs, DOM snapshot, profile
      data) as JSON for sharing or attaching to issues.
- [ ] **Plugin API**: define an extension point so third-party or project-
      specific diagnostic panels can be loaded at runtime.

---

## Developer Workflow

The console and dev site should support the following workflows:

1. **Investigating a rendering bug**
   - Open the page in Broiler → press `F12` → select the problematic element
     in the DOM Inspector → review computed styles and box model → check the
     log viewer for warnings/errors.

2. **Debugging JavaScript interaction**
   - Open the JS Console tab → evaluate expressions against the live
     `JSContext` → view `console.log` output in the log viewer.

3. **Profiling layout performance**
   - Enable profiling → reload the page → open the Performance Profiler tab
     → identify slow layout passes by box.

4. **Testing CSS compliance**
   - Open the Development Site → navigate to the Test Case Runner → run Acid2
     → review pixel-diff overlay and compliance score.

5. **Prototyping a fix**
   - Open the Snippet Playground → paste minimal HTML/CSS → iterate until
     rendering matches expectations → copy the fix into the engine.

---

## Extensibility

- **Panel registration API**: allow new console panels to be registered via a
  simple interface (`IConsolePanel` or similar) so internal and external
  contributors can add diagnostics.
- **Event hooks**: expose lifecycle events (page-loaded, layout-complete,
  paint-complete) that panels can subscribe to.
- **Theming**: support light and dark themes for the console panel to match
  the host application.

---

## Documentation Requirements

| Document | Location | Description |
|----------|----------|-------------|
| This roadmap | `docs/roadmap/dev-console-and-site.md` | Overall plan and milestones |
| ADR: Dev Console Architecture | `docs/adr/` (new) | Records key architectural decisions (panel hosting, communication, APIs) |
| Console User Guide | `docs/dev-console-guide.md` (new) | End-user guide for the in-app console |
| Dev Site README | `src/Broiler.DevSite/README.md` (new) | Setup and usage for the development site |
| API Reference | Auto-generated | `DomBridge` and `BoxTreeVisitor` API surface |

---

## Deliverables & Milestones

| Phase | Key Deliverable | Target |
|-------|----------------|--------|
| **1 – Foundation** | Observable logger, `console.*` JS API, CLI `--diagnostics`, box-tree visitor | First |
| **2 – Core Console** | In-app console with Log Viewer, DOM Inspector, Computed Styles, JS REPL, Error Overlay | Second |
| **3 – Dev Site** | Browser-accessible test runner, snippet playground, compliance dashboard | Third |
| **4 – Advanced** | Performance profiler, layout diff, export/import, plugin API | Fourth |

Each phase should be delivered incrementally with its own set of issues,
reviewed in pull requests, and accompanied by the documentation listed above.
