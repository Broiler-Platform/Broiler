# Broiler

A WPF-based web browser built with [HTML-Renderer](https://github.com/ArtOfSettling/HTML-Renderer) for HTML/CSS rendering and [YantraJS](https://github.com/yantrajs/yantra) for JavaScript execution.

## Overview

Broiler is a lightweight, extensible web browser for Windows built entirely in managed C#. It combines:

- **HTML-Renderer** — a high-performance, 100% managed HTML/CSS rendering engine for WPF
- **YantraJS** — a .NET Standard JavaScript engine supporting ES2020+ features

## Architecture

```
┌─────────────────────────────────────────────┐
│              Broiler WPF Shell              │
│  ┌───────────────────────────────────────┐  │
│  │     Navigation Bar (URL, Controls)    │  │
│  ├───────────────────────────────────────┤  │
│  │                                       │  │
│  │          HtmlPanel (Renderer)         │  │
│  │      ┌─────────────────────────┐      │  │
│  │      │    Broiler.HTML         │      │  │
│  │      │    (HTML/CSS Engine)    │      │  │
│  │      └──────────┬──────────────┘      │  │
│  │                 │                     │  │
│  │      ┌──────────▼──────────────┐      │  │
│  │      │  Broiler.HtmlBridge    │      │  │
│  │      │  (DOM ↔ JS Bridge)     │      │  │
│  │      └──────────┬──────────────┘      │  │
│  │                 │                     │  │
│  │      ┌──────────▼──────────────┐      │  │
│  │      │  Broiler.JavaScript    │      │  │
│  │      │  (JavaScript Engine)   │      │  │
│  │      └─────────────────────────┘      │  │
│  │                                       │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

### Key Components

| Component | Description |
|-----------|-------------|
| `Broiler.App` | WPF application entry point and main window |
| `Broiler.HtmlBridge` | Bridge component connecting HTML rendering with JavaScript execution (DomBridge, ScriptEngine, shared utilities) |
| `Broiler.HTML.Dom` | Shared HTML parsing and DOM utilities (WHATWG tokenizer, serialization) |
| `Broiler.HTML` | Cross-platform HTML/CSS parsing and rendering engine |
| `Broiler.JavaScript` | JavaScript engine with ES2020+ support |

## Building

### Prerequisites

- .NET 8.0 SDK or later
- Windows (WPF requires Windows)

### Build

```bash
dotnet build Broiler.slnx
```

### Run

```bash
dotnet run --project src/Broiler.App
```

## Project Structure

```
Broiler/
├── src/
│   ├── Broiler.App/              # WPF browser application
│   │   └── Rendering/            # Modular rendering pipeline
│   └── Broiler.App.Tests/        # Unit tests
├── docs/
│   └── adr/                      # Architecture Decision Records
├── HTML-Renderer-1.5.2/          # HTML/CSS rendering engine
├── yantra-1.2.295/                # JavaScript engine
└── Broiler.slnx                   # Solution file
```

## Roadmap

See [Issue #1](https://github.com/MaiRat/Broiler/issues/1) for the full development roadmap.

### HTML & JavaScript Engine

A comprehensive plan covering milestones from Enhanced MVP through to a
production-grade, standards-compliant HTML and JavaScript engine.
See the [HTML & JS Engine Roadmap](docs/roadmap/html-js-engine.md) for details.

### Development Console & Development Site

A roadmap for integrating an in-app developer console and a dedicated
development site to aid rendering investigation, debugging, and testing.
See the [Dev Console & Site Roadmap](docs/roadmap/dev-console-and-site.md)
for details.

### AvaloniaUI Support

A roadmap for integrating AvaloniaUI to enable cross-platform desktop rendering
on Windows, macOS, and Linux.
See the [AvaloniaUI Support Roadmap](docs/roadmap/avalonia-ui-support.md) for
details.

### CLI Website Capture Tool

A cross-platform command-line tool for capturing website screenshots.
See the [CLI Roadmap](docs/roadmap/cli-website-capture.md) and
[ADR-004](docs/adr/004-os-independent-cli-capture-tool.md) for details.

#### CI Website Capture

The CI workflow (`.github/workflows/build.yml`) automatically captures a
screenshot of `https://www.heise.de/` after every successful build and test run.
The screenshot is uploaded as a build artifact named `website-capture`. This
verifies the rendering pipeline remains functional on every change.

### Log Analyzer Tool

A command-line tool for analysing Apache access log files. It supports single
files, directories, rotated logs (`access.log.1`, `.2`, …), and
gzip-compressed logs (`.gz`).

```bash
# Analyse a single file (default: top 10 results)
dotnet run --project src/Broiler.LogAnalyzer.Cli -- access.log

# Show the top 20 results
dotnet run --project src/Broiler.LogAnalyzer.Cli -- --file /var/log/apache2/ --top 20

# Show all results (no limit)
dotnet run --project src/Broiler.LogAnalyzer.Cli -- --file /var/log/apache2/ --top 0
```

The report includes:

| Section | Description |
|---------|-------------|
| Summary | Total requests, unique IPs, bytes transferred |
| Status Code Distribution | Breakdown of HTTP status codes |
| HTTP Methods | Request method distribution |
| Top Endpoints | Most-requested endpoints |
| Top IPs | Most-active IP addresses |
| Top 404 Endpoints | Endpoints returning 404 — useful for detecting suspicious access patterns |

Use `--top 0` to remove the default top-10 limit and display all entries for
deeper investigation.

### Current Phase: Project Initialization

- [x] Define project goals and design requirements
- [x] Establish project directory structure
- [x] Set up solution and source control
- [x] Document architectural decisions (ADR)
- [x] Create initial WPF project skeleton
- [x] Integrate html-renderer and yantra as project references
- [x] Implement navigation history (back/forward/refresh)
- [x] Implement rendering pipeline
- [x] Enable DOM interaction via yantra
- [x] Support advanced HTML/CSS features

### Testing

Run the full test suite:

```bash
dotnet test Broiler.slnx
```

Run the Acid1 CSS1 conformance tests:

```bash
dotnet test src/Broiler.Cli.Tests/ --filter "FullyQualifiedName~Acid1"
```

See [docs/acid1-testing.md](docs/acid1-testing.md) for detailed Acid1 test
documentation and [docs/testing-guide.md](docs/testing-guide.md) for the
complete testing guide.

## DOM Interaction

Broiler exposes a `document` object to JavaScript executed via YantraJS,
enabling scripts embedded in HTML pages to interact with the DOM.

### Available APIs

#### Document methods

| API | Description |
|-----|-------------|
| `document.title` | Read or write the page title |
| `document.getElementById(id)` | Find an element by its `id` attribute |
| `document.getElementsByTagName(tag)` | Find all elements with the given tag name |
| `document.getElementsByClassName(name)` | Find all elements that carry the given class name |
| `document.querySelector(selector)` | Return the first element matching a CSS selector |
| `document.querySelectorAll(selector)` | Return all elements matching a CSS selector |
| `document.createElement(tag)` | Create a new element |

`querySelector` / `querySelectorAll` support tag type (`div`), `#id`, `.class`
(multiple), `[attr]`, and `[attr=value]` tokens, including compound selectors
such as `div.card#hero[data-active=true]`.

#### Element properties and methods

| API | Description |
|-----|-------------|
| `el.tagName` | Tag name in upper-case (read-only) |
| `el.id` | Element `id` attribute (read-only) |
| `el.className` | Space-separated class string (read/write) |
| `el.innerHTML` | Inner HTML content (read/write) |
| `el.style.setProperty(prop, value)` | Set a CSS property on the element |
| `el.style.getPropertyValue(prop)` | Get the value of a CSS property |
| `el.style.removeProperty(prop)` | Remove a CSS property; returns the old value |
| `el.style.cssText` | Get or set the full inline style string (read/write) |
| `el.classList.contains(cls)` | Returns `true` if the element has the class |
| `el.classList.add(...cls)` | Add one or more class names |
| `el.classList.remove(...cls)` | Remove one or more class names |
| `el.classList.toggle(cls[, force])` | Toggle a class; returns `true` if added |
| `el.setAttribute(name, value)` | Set an attribute value |
| `el.getAttribute(name)` | Get an attribute value, or `null` if absent |

### Example

Given the following HTML page:

```html
<html>
<head><title>Demo</title></head>
<body>
  <div id="greeting" class="box" style="color: blue">Hello</div>
  <script>
    var el = document.getElementById('greeting');
    // el.tagName   → "DIV"
    // el.id        → "greeting"
    // el.className → "box"
    // el.innerHTML → "Hello"
    var t = document.title; // "Demo"

    // Modern selector
    var same = document.querySelector('#greeting');

    // CSS style manipulation
    el.style.setProperty('color', 'red');
    el.style.cssText = 'font-size: 18px; font-weight: bold';

    // Class manipulation
    el.classList.add('highlight');
    el.classList.remove('box');
    el.classList.toggle('active');     // → true (added)
    el.classList.contains('highlight'); // → true

    // Attribute access
    el.setAttribute('data-count', '3');
    el.getAttribute('data-count');     // → "3"
  </script>
</body>
</html>
```

### Architecture

The `DomBridge` class (in `Broiler.HtmlBridge`) parses the page HTML and
registers a `document` global on the YantraJS `JSContext` before scripts
execute.  This enables bidirectional communication: JavaScript can query the
DOM, and property changes (e.g. setting `document.title`) are reflected back
to the bridge.

```
PageContent (HTML + Scripts)
       │
       ▼
┌────────────────────────────────────────┐
│         Broiler.HtmlBridge             │
│  ┌──────────┐   ┌──────────────────┐  │
│  │DomBridge │──▶│ HtmlTreeBuilder  │  │  Parses HTML → DomElement tree
│  └──────────┘   └──────────────────┘  │
│  ┌──────────┐   ┌──────────────────┐  │
│  │Script    │──▶│ JSContext         │  │  Executes scripts with DOM
│  │Engine    │   │ (Broiler.JS)     │  │
│  └──────────┘   └──────────────────┘  │
└────────────────────────────────────────┘
```

### Shared Components (Broiler.HtmlBridge ↔ Broiler.HTML)

The WHATWG-aligned HTML tokenizer and serialization utilities are shared between
the HtmlBridge and the Broiler.HTML rendering engine:

```
Broiler.HTML.Dom (shared layer)
├── Core/Parse/HtmlTokenizer    ← WHATWG §13.2.5 tokenizer
├── Core/Parse/HtmlParser       ← CSS box-tree parser (uses HtmlTokenizer)
└── Core/Utils/HtmlSerializer   ← HtmlEncode, VoidTags, shorthand helpers
       │
       ├──▶ Broiler.HTML rendering pipeline
       │    (HtmlParser → CssBox tree → layout → paint)
       │
       └──▶ Broiler.HtmlBridge
            (HtmlTreeBuilder → DomElement tree → JS bridge)
```

| Shared Component | Location | Used By |
|------------------|----------|---------|
| `HtmlTokenizer` | `Broiler.HTML.Dom/Core/Parse/` | `HtmlParser` (CSS rendering), `HtmlTreeBuilder` (HtmlBridge) |
| `HtmlSerializer` | `Broiler.HTML.Dom/Core/Utils/` | `DomBridge.Serialization` (DOM → HTML) |

### Broiler.HtmlBridge Contents

The `Broiler.HtmlBridge` project is a standalone class library (net8.0) that
bridges `Broiler.HTML` and `Broiler.JavaScript`.  It contains:

| Component | Description |
|-----------|-------------|
| `DomBridge` (10 partial files) | DOM ↔ JavaScript bridge: element conversion, event dispatch, CSS, selectors, traversal, serialization |
| `ScriptEngine` / `IScriptEngine` | Orchestrates JS execution with DOM interaction |
| `HtmlTreeBuilder` | WHATWG-aligned tree builder: `HtmlToken` → `DomElement` tree |
| `ScriptExtractor` / `IScriptExtractor` | Extracts `<script>` tags from HTML |
| `InteractiveSession` | Step-through timer/animation REPL |
| `MicroTaskQueue` | Promise/microtask queue per HTML Living Standard |
| `ContentSecurityPolicy` | CSP Level 3 script-src enforcement |
| `RenderLogger` | Diagnostic logging (console.log bridge) |
| Rendering utilities | `HtmlPostProcessor`, `CssBoxModel`, `RenderingStages`, `ImagePipeline`, `CssTextProperties` |

## License

See individual component licenses:
- HTML-Renderer: BSD License
- YantraJS: Apache-2.0 License