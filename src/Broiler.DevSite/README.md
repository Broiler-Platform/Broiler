# Broiler.DevSite

A browser-accessible development, test, and demo environment for the
**Broiler** HTML rendering engine. This ASP.NET Core Razor Pages application
provides tools for testing, comparing, and exploring Broiler's rendering
capabilities.

## Features

| Feature | Description |
|---------|-------------|
| **Test Case Runner** | Execute Acid1 and Acid2 rendering tests headlessly and view pixel-diff results inline. |
| **Side-by-Side Comparison** | Upload an HTML file and an optional reference image, render via Broiler, and compare the results with highlighted differences. |
| **Snippet Playground** | Editable HTML/CSS pane with live Broiler rendering preview (similar to CodePen). |
| **Compliance Dashboard** | Aggregated CSS 2.1 chapter checklist status parsed from the `css2/` Markdown files with overall completion percentages. |
| **API Documentation** | Auto-generated reference for the `DomBridge` DOM API surface extracted from XML doc comments. |

## Prerequisites

- .NET 8.0 SDK or later
- The Broiler solution built (or at least `HtmlRenderer.Image` available)

## Getting Started

```bash
# From the repository root
cd src/Broiler.DevSite
dotnet run
```

The site starts at `http://localhost:5000` by default. In development mode
(`ASPNETCORE_ENVIRONMENT=Development`), it also listens on a random port shown
in the console output.

## Project Structure

```
src/Broiler.DevSite/
├── Pages/
│   ├── Index.cshtml          # Home page with feature cards
│   ├── TestRunner.cshtml     # Acid1/Acid2 test runner
│   ├── Compare.cshtml        # Side-by-side comparison
│   ├── Playground.cshtml     # Snippet playground
│   ├── Compliance.cshtml     # CSS 2.1 compliance dashboard
│   └── ApiDocs.cshtml        # DomBridge API reference
├── Services/
│   ├── RenderingService.cs   # Headless rendering via HtmlRenderer.Image
│   ├── ComplianceService.cs  # CSS2 checklist parser
│   └── ApiDocService.cs      # DomBridge source-level doc extractor
├── Program.cs                # App configuration and API endpoints
└── README.md                 # This file
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/render` | POST | Renders HTML body content to a PNG image. Accepts `width` and `height` query parameters (100–4096). |

## Configuration

The site expects the following directory layout relative to its project root:

- `../../acid/` — Acid1 and Acid2 test files and reference images
- `../../css2/` — CSS 2.1 compliance checklist Markdown files
- `../Broiler.App/Rendering/DomBridge.cs` — Source file for API doc generation

These paths are resolved automatically when running from the standard
repository layout.
