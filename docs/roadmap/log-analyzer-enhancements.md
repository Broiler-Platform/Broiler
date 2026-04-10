# Roadmap: Broiler.LogAnalyzer Enhancements

> **Status**: Active — created 2026-04-10 | Phase 1–5 complete  
> **Tracking issue**: Create Roadmap Document for Broiler.LogAnalyzer Enhancements

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current State](#2-current-state)
3. [WPF ListView Control Enhancements](#3-wpf-listview-control-enhancements)
4. [Expanded Log Analysis Capabilities](#4-expanded-log-analysis-capabilities)
5. [Diagram and Visualization Integration](#5-diagram-and-visualization-integration)
6. [Unified and Per-IP Statistical Summaries](#6-unified-and-per-ip-statistical-summaries)
7. [Export Options and Data Portability](#7-export-options-and-data-portability)
8. [CLI Improvements](#8-cli-improvements)
9. [Performance and Scalability](#9-performance-and-scalability)
10. [UI/UX Refinements](#10-uiux-refinements)
11. [Prioritized TODO List](#11-prioritized-todo-list)
12. [Milestones](#12-milestones)

---

## 1. Executive Summary

Broiler.LogAnalyzer is a three-component tool suite for analyzing Apache access
logs:

| Component                     | Role                                                      |
|-------------------------------|-----------------------------------------------------------|
| **Broiler.LogAnalyzer** (Lib) | Core library — parsing, metrics, filtering, export        |
| **Broiler.LogAnalyzer.Cli**   | Command-line interface — text-based reports and exports    |
| **Broiler.LogAnalyzer.Wpf**   | WPF desktop application — GUI for file selection and analysis |

The current implementation covers fundamental metrics (request counts, status
code distributions, top endpoints/IPs, hourly distributions, error summaries,
per-host statistics) and supports CSV/JSON export plus flexible filtering.

This roadmap outlines enhancements across all three components to improve
usability, deepen analysis capabilities, and introduce visual data
representations.

---

## 2. Current State

### 2.1 Library (`Broiler.LogAnalyzer`)

The core library provides:

- **`LogEntry`** — immutable record for a parsed log line (Common and Combined
  formats).
- **`LogParser`** — regex-based parser supporting both Common and Combined
  Apache log formats.
- **`LogFileDiscovery`** — file/directory resolution with support for rotated
  logs (`access.log.1`, `.2`, …) and gzip-compressed files (`.gz`).
- **`LogAnalyzerService`** — metrics engine offering:
  - Total requests and unique IP count
  - Status code distribution
  - Top endpoints (configurable N, 0 = all)
  - Top IPs (configurable N, 0 = all)
  - Top 404 endpoints
  - HTTP method distribution
  - Hourly request distribution (0–23)
  - Total bytes transferred
  - Error summary (4xx / 5xx categories)
  - Per-host statistics (requests, bytes, error count, error rate)
  - Multi-criteria filtering (status range, IP, endpoint pattern, time range)
  - CSV and JSON export

### 2.2 CLI (`Broiler.LogAnalyzer.Cli`)

The CLI supports:

- Positional or `--file` path argument (file or directory)
- `--top N` to control number of results (0 = all)
- `--filter-status`, `--filter-ip`, `--from`, `--to` filtering
- `--export-csv`, `--export-json` export
- `--help` usage text
- Formatted text report to stdout

### 2.3 WPF (`Broiler.LogAnalyzer.Wpf`)

The WPF application provides:

- File and folder browsing dialogs
- Configurable "Top N results" input
- Async background analysis
- Plain-text results display in a `TextBox` (monospace, read-only)
- Status bar feedback

### 2.4 Test Coverage

Tests reside in `Broiler.LogAnalyzer.Tests` and cover:

- `LogParserTests` — parsing valid and malformed lines
- `LogAnalyzerServiceTests` — all metric methods
- `LogAnalyzerServiceEnhancedTests` — filtering, export, per-host stats
- `LogFileDiscoveryTests` — file resolution patterns
- `ProgramTests` / `ProgramEnhancedTests` — CLI argument handling and report output

---

## 3. WPF ListView Control Enhancements

The current WPF interface displays analysis results as plain text in a `TextBox`.
Replacing this with structured, interactive controls will significantly improve
the log browsing experience.

### 3.1 Replace TextBox with ListView/DataGrid for Log Entries

- **Add a `DataGrid`** (or `ListView` with `GridView`) to display individual log
  entries in a tabular format with columns for: Remote Host, Timestamp, Method,
  Endpoint, Status Code, Response Size, User Agent.
- **Enable column sorting** — clicking a column header sorts entries by that
  field (ascending/descending toggle).
- **Enable column reordering and resizing** — let users customize the view.
- **Virtual scrolling** — use `VirtualizingStackPanel` for efficient rendering
  of large log files (100k+ entries).

### 3.2 Filtering and Search within the UI

- **Quick-filter toolbar** — text box + dropdowns for filtering by status code
  range, IP, method, endpoint substring, and time range.
- **Live search** — highlight matching rows as the user types.
- **Saved filter presets** — let users save commonly used filter combinations.

### 3.3 Row Details and Context

- **Row detail expansion** — expand a row to show the full log entry including
  Referer, User Agent, and raw log line.
- **Color-coded status** — apply background colors to rows based on status code
  class (green for 2xx, yellow for 3xx, orange for 4xx, red for 5xx).
- **Right-click context menu** — options to copy row, filter by this IP, filter
  by this endpoint, etc.

### 3.4 Tabbed Results View

- **Tab control** — separate tabs for: Raw Log Entries, Summary Statistics,
  Charts/Diagrams, Per-Host Details.
- **Lazy tab loading** — only compute and render tab content when the tab is
  first selected.

---

## 4. Expanded Log Analysis Capabilities

### 4.1 Pattern Detection

- **Bot/crawler detection** — identify requests from known bots (Googlebot,
  Bingbot, etc.) by User-Agent patterns and report bot vs. human traffic ratio.
- **Brute-force / scan detection** — flag IPs that generate a high volume of
  4xx errors in a short time window (potential brute-force or vulnerability
  scanning).
- **Slow endpoints** — if response time is available (extended log format),
  identify endpoints with the highest average response time.
- **Hotlink detection** — identify external referers that link directly to
  static assets.

### 4.2 Automated Summaries

- **Natural-language summary** — generate a human-readable paragraph summarizing
  key findings (e.g., "Traffic peaked at 14:00 with 1,234 requests. The top IP
  10.0.0.5 generated 23% of all traffic. 12% of requests returned errors.").
- **Anomaly highlights** — flag statistical outliers (e.g., an IP generating
  10× the average request count, or an endpoint with an unusually high error
  rate).

### 4.3 Time-Series Analysis

- **Request rate over time** — compute requests-per-minute or
  requests-per-second for the analysis window.
- **Traffic trend detection** — identify increasing, decreasing, or stable
  traffic patterns across the log time range.
- **Peak detection** — identify time windows with the highest request
  concentration.

### 4.4 Security Analysis

- **Suspicious request detection** — flag requests with common attack patterns
  in endpoints (SQL injection fragments, path traversal sequences like `../`,
  shell command injection patterns).
- **Geographic analysis** — if a GeoIP database is available, map IPs to
  countries and report geographic distribution.

---

## 5. Diagram and Visualization Integration

### 5.1 Chart Types for the WPF Application

Integrate charting into the WPF application to visualize analysis results.
Candidate libraries include LiveCharts2, OxyPlot, or ScottPlot (all support
WPF).

| Chart Type          | Data Source                          | Purpose                                  |
|---------------------|--------------------------------------|------------------------------------------|
| **Bar chart**       | Status code distribution             | Compare request counts per status code   |
| **Pie/Donut chart** | HTTP method distribution             | Show method proportions                  |
| **Line chart**      | Hourly distribution                  | Visualize traffic patterns over 24 hours |
| **Horizontal bar**  | Top endpoints, top IPs               | Rank items by request count              |
| **Stacked bar**     | Per-host error vs. success breakdown | Compare host behavior side by side       |
| **Heatmap**         | Requests by hour × day-of-week       | Identify recurring traffic patterns      |
| **Timeline**        | Request rate over time               | Spot traffic spikes and anomalies        |

### 5.2 CLI ASCII Charts

For the CLI component, add optional ASCII-art chart output:

- **Horizontal bar charts** for top endpoints and top IPs.
- **Sparkline-style** hourly distribution using block characters (▁▂▃▄▅▆▇█).
- Enabled via a `--chart` flag.

### 5.3 Chart Export

- **Export charts as PNG/SVG** from the WPF application.
- **Include charts in HTML report export** (see Section 7).

---

## 6. Unified and Per-IP Statistical Summaries

### 6.1 Unified Summary Dashboard

Extend the existing summary with additional aggregate metrics:

- **Average response size** — total bytes / total requests.
- **Requests per second** — total requests / time span of log.
- **Error rate** — percentage of 4xx + 5xx responses.
- **Success rate** — percentage of 2xx responses.
- **Top referers** — most common `Referer` values.
- **Top user agents** — most common `User-Agent` strings (grouped by
  browser/bot family where possible).
- **Response size percentiles** — P50, P90, P95, P99 of response sizes.

### 6.2 Per-IP Statistics Enhancements

Extend the existing `PerHostStatistics` with:

- **Per-IP method distribution** — which HTTP methods each IP uses.
- **Per-IP endpoint list** — which endpoints each IP accessed (top N).
- **Per-IP time range** — first and last request timestamp per IP.
- **Per-IP status breakdown** — status code distribution per IP.
- **Per-IP bandwidth** — bytes transferred per IP (already available; surface
  more prominently).
- **Per-IP session estimation** — approximate session count using a
  configurable inactivity gap (e.g., 30 minutes).

### 6.3 Comparison Mode

- **Compare two log files** — produce a side-by-side diff of metrics between
  two log files or time periods.
- **Before/after analysis** — useful for measuring the impact of a deployment
  or configuration change.

---

## 7. Export Options and Data Portability

### 7.1 Additional Export Formats

Extend beyond the current CSV and JSON exports:

| Format          | Description                                                    |
|-----------------|----------------------------------------------------------------|
| **HTML report** | Self-contained HTML file with styled tables and embedded charts |
| **Markdown**    | Report formatted as Markdown (suitable for issue trackers, wikis) |
| **TSV**         | Tab-separated values (for spreadsheet import)                  |

### 7.2 Selective Export

- **Export filtered results** — export only the entries matching the current
  filter criteria (already partially supported; expose in WPF UI).
- **Export summary only** — export just the computed metrics without raw
  entries.
- **Export per-host report** — generate a separate section (or file) for each
  IP address.

### 7.3 WPF Export Integration

- **"Export" menu/button** in the WPF toolbar with format selection.
- **Copy to clipboard** — copy the summary or selected rows to clipboard in
  various formats.

---

## 8. CLI Improvements

### 8.1 Output Formatting

- **`--format` flag** — choose output format: `text` (default), `json`,
  `csv`, `markdown`, `html`.
- **`--no-color`** flag — disable ANSI color codes for piping to files.
- **`--quiet`** flag — output only the summary numbers (machine-parseable).
- **`--verbose`** flag — include additional detail (per-host tables, extended
  metrics).

### 8.2 Additional Analysis Flags

- **`--filter-method <METHOD>`** — filter by HTTP method.
- **`--filter-endpoint <PATTERN>`** — filter by endpoint (already supported in
  the Lib layer; expose in CLI).
- **`--compare <PATH>`** — compare two log files or directories side by side.
- **`--detect-bots`** — enable bot detection and separate bot vs. human stats.

### 8.3 Streaming Mode

- **`--follow` / `--tail`** — monitor a log file in real time and update
  statistics as new entries are appended (similar to `tail -f`).

---

## 9. Performance and Scalability

### 9.1 Large File Handling

- **Streaming parser** — process entries without loading the entire file into
  memory; yield entries one at a time (already partially done with
  `ReadLines`; extend to the analysis pipeline).
- **Parallel parsing** — for multi-file scenarios, parse files in parallel.
- **Memory-mapped files** — for very large single files, use memory-mapped I/O.

### 9.2 Incremental Analysis

- **Cached state** — save intermediate analysis state so that appending new
  entries does not require re-parsing the entire file.
- **Resume support** — remember the last-read position in a file for
  incremental updates.

### 9.3 Benchmarks

- **Add BenchmarkDotNet project** — track parsing and analysis performance
  across releases.
- **Target metrics**: parse rate (lines/sec), analysis time for 1M entries,
  memory footprint.

---

## 10. UI/UX Refinements

### 10.1 WPF Application

- **Dark/light theme support** — follow system theme or allow manual toggle.
- **Drag-and-drop** — drop log files directly onto the window to start
  analysis.
- **Recent files list** — remember and surface recently analyzed paths.
- **Progress bar** — show a determinate progress bar during analysis (based on
  files processed / total files).
- **Keyboard shortcuts** — Ctrl+O for open, Ctrl+E for export, F5 to
  re-analyze.
- **Resizable panels** — use a `GridSplitter` to let users resize the
  results area.

### 10.2 Accessibility

- **Screen reader support** — ensure all controls have proper
  `AutomationProperties.Name` labels.
- **High-contrast mode** — respect Windows high-contrast settings.
- **Keyboard navigation** — ensure all features are accessible without a
  mouse.

---

## 11. Prioritized TODO List

Items are ordered by estimated impact and implementation complexity.

| #  | Enhancement                                   | Component | Impact | Effort | Section | Status |
|----|-----------------------------------------------|-----------|--------|--------|---------|--------|
| 1  | DataGrid for log entries with sorting          | WPF       | High   | Medium | 3.1     | ✅ Done |
| 2  | Color-coded status rows                        | WPF       | High   | Low    | 3.3     | ✅ Done |
| 3  | Quick-filter toolbar                           | WPF       | High   | Medium | 3.2     | ✅ Done |
| 4  | Bot/crawler detection                          | Lib       | High   | Medium | 4.1     | ✅ Done |
| 5  | Automated natural-language summary             | Lib       | High   | Medium | 4.2     | ✅ Done |
| 6  | Bar chart for status codes                     | WPF       | High   | Medium | 5.1     | ✅ Done |
| 7  | Line chart for hourly distribution             | WPF       | High   | Medium | 5.1     | ✅ Done |
| 8  | HTML report export                             | Lib       | Medium | Medium | 7.1     | ✅ Done |
| 9  | `--format` flag for CLI                        | CLI       | Medium | Low    | 8.1     | ✅ Done |
| 10 | `--filter-endpoint` in CLI                     | CLI       | Medium | Low    | 8.2     | ✅ Done |
| 11 | Per-IP method and endpoint distribution        | Lib       | Medium | Medium | 6.2     | Planned |
| 12 | Top referers and user agents                   | Lib       | Medium | Low    | 6.1     | ✅ Done |
| 13 | Average response size and requests/sec         | Lib       | Medium | Low    | 6.1     | ✅ Done |
| 14 | Tabbed results view                            | WPF       | Medium | Medium | 3.4     | ✅ Done |
| 15 | ASCII bar charts in CLI                        | CLI       | Medium | Low    | 5.2     | ✅ Done |
| 16 | Suspicious request detection                   | Lib       | Medium | Medium | 4.4     | ✅ Done |
| 17 | Drag-and-drop file support                     | WPF       | Medium | Low    | 10.1    | ✅ Done |
| 18 | Parallel file parsing                          | Lib       | Medium | Medium | 9.1     | ✅ Done |
| 19 | Dark/light theme support                       | WPF       | Low    | Medium | 10.1    | ✅ Done |
| 20 | Comparison mode                                | Lib+CLI   | Low    | High   | 6.3     | ✅ Done |
| 21 | Chart export (PNG/SVG)                         | WPF       | Low    | Medium | 5.3     | ✅ Done |
| 22 | `--follow` / live-tail mode                    | CLI       | Low    | High   | 8.3     | ✅ Done |
| 23 | BenchmarkDotNet project                        | Lib       | Low    | Low    | 9.3     | ✅ Done |
| 24 | Heatmap (hour × day-of-week)                   | WPF       | Low    | High   | 5.1     | ✅ Done |
| 25 | GeoIP integration                              | Lib       | Low    | High   | 4.4     | ✅ Done |

---

## 12. Milestones

### Phase 1 — Interactive WPF & Core Metrics (TODOs 1–3, 12–14) ✅ Complete

Replaced the plain-text output with a `DataGrid`, added color-coded status rows
(`StatusCodeToBrushConverter`: green 2xx, yellow 3xx, orange 4xx, red 5xx),
a quick-filter toolbar (status class, method, IP substring, endpoint substring),
and a tabbed layout (Log Entries + Summary). Extended the Lib with
`TopReferers(top)`, `TopUserAgents(top)`, `AverageResponseSize`, and
`RequestsPerSecond`. CLI report now includes these new metrics. 20 new unit
tests added in `LogAnalyzerServicePhase1Tests`.

### Phase 2 — Visualization & Charting (TODOs 6–7, 14–15) ✅ Complete

Integrated ScottPlot (v5) WPF charting library with a bar chart for status code
distribution and a line chart for hourly request distribution, displayed in a
new "Charts" tab with lazy loading. Extended the tabbed results view to four
tabs: Log Entries, Summary, Charts, and Per-Host Details (also lazily loaded).
Added `AsciiChartService` to the core library providing horizontal bar charts
and sparkline-style hourly distributions using Unicode block characters
(▁▂▃▄▅▆▇█). CLI now supports a `--chart` flag that outputs ASCII charts for
top endpoints, top IPs, and hourly distribution. 15 new unit tests added in
`Phase2VisualizationTests`.

### Phase 3 — Deeper Analysis (TODOs 4–5, 16) ✅ Complete

Added bot/crawler detection via User-Agent pattern matching (34 known bot
patterns) with `IsBotUserAgent`, `DetectBots`, and `BotTrafficSummary`.
Implemented suspicious request detection scanning for SQL injection, path
traversal, shell injection, and XSS patterns via `DetectSuspiciousRequests`.
Added automated natural-language summary generation via `GenerateSummary`
covering traffic overview, peak hours, top contributors, error rates, bot
traffic, and suspicious requests. All three features surfaced in both CLI
(`PrintReport`) and WPF (`FormatReport`). 33 new unit tests added in
`LogAnalyzerServicePhase3Tests`.

### Phase 4 — Export & CLI (TODOs 8–10) ✅ Complete

Added HTML report export via `ExportHtml(top)` producing self-contained HTML with
styled tables, metric cards, and an embedded hourly distribution bar chart.
Added Markdown report export via `ExportMarkdown(top)` with structured tables for
all metrics sections. CLI now supports `--format` flag accepting `text` (default),
`json`, `csv`, `markdown`, and `html` to output the report in the chosen format.
CLI now supports `--filter-endpoint <PATTERN>` for case-insensitive endpoint
substring filtering (wiring the existing `Filter(endpointPattern:)` method).
Updated help text with new options and examples. 33 new unit and integration tests
added in `Phase4ExportCliTests`.

### Phase 5 — Advanced Features (TODOs 17–25) ✅ Complete

Added drag-and-drop file support in WPF (`AllowDrop`, `DragOver`, `Drop`
handlers accepting files and directories). Implemented parallel file parsing
via `LogParser.ParseFilesParallel` using `Parallel.ForEach` with
`ConcurrentBag` for thread-safe result collection. Added dark/light theme
toggle in WPF with background/foreground switching. Implemented comparison
mode with `LogAnalyzerService.Compare()` producing side-by-side metrics,
`FormatComparison` (text) and `FormatComparisonMarkdown` (markdown) reports;
CLI supports `--compare <PATH>` flag with format-aware output. Added chart
export (PNG/SVG) via ScottPlot's `SavePng`/`SaveSvg` with export buttons
in the Charts tab. CLI now supports `--follow` live-tail mode that monitors
a file for new entries using `FileStream` seek-based polling. Added
BenchmarkDotNet project (`Broiler.LogAnalyzer.Benchmarks`) with parser and
service benchmarks covering parse rate, metric computation, and memory
diagnostics. Added heatmap (hour × day-of-week) visualization in WPF using
a color-coded `Grid` with GitHub-style green gradient and legend. Implemented
GeoIP integration with `GeoIpService` featuring a pluggable `ILookupProvider`
interface, a built-in RFC 1918/reserved IP classifier, and
`GeographicDistribution()` method for country-level traffic analysis.
29 new unit tests added in `Phase5AdvancedFeaturesTests`.
