# Roadmap: OS-Independent Command-Line Tool for Website Capture

## Goal

Deliver a cross-platform command-line utility that captures website content
from any URL using the local rendering engines (HTML-Renderer and YantraJS),
and integrates with the Copilot agent workflow for automated verification
after issue resolution.

---

## Phase 1 — Project Scaffolding

- [x] Create `src/Broiler.Cli/Broiler.Cli.csproj` targeting `net8.0`
- [x] Add the new project to `Broiler.slnx`
- [x] Reference HTML-Renderer and YantraJS local engines
- [x] Implement a minimal `Program.cs` that accepts `--url` and `--output` arguments
- [x] Verify the project builds on Windows, macOS, and Linux

## Phase 2 — Website Capture Implementation

- [x] Implement `CaptureService` that uses local engines to:
  - Fetch HTML via HttpClient
  - Process CSS using HTML-Renderer
  - Execute inline scripts using YantraJS
  - Save the captured content to the output path
- [x] Add robust error handling:
  - Invalid URL validation
  - Navigation timeout with configurable duration
  - File I/O errors (permissions, disk space)
  - HTTP request failures with helpful messages
- [x] Support output formats: HTML (default), TXT
- [x] Add `--full-page` flag for full content capture
- [x] Add `--timeout` option (default: 30 seconds)

## Phase 3 — Engine Testing Integration

- [x] Reference rendering assemblies from `Broiler.Cli`
- [x] Add a `--test-engines` command that:
  - Runs a basic HTML-Renderer parse/render cycle
  - Runs a basic YantraJS script execution cycle
  - Reports pass/fail for each engine
- [x] Create `src/Broiler.Cli.Tests/` with xUnit tests for:
  - Successful capture of a local HTML file
  - Error handling for invalid URLs
  - Engine test command output

## Phase 4 — Copilot Workflow Integration

- [x] Update `.github/copilot-instructions.md` to document:
  - Post-issue-resolution capture step
  - Expected error handling behavior
  - Reference URL (`https://www.heise.de/`)
- [x] Add CI workflow step that runs after tests:
  - `dotnet run --project src/Broiler.Cli -- --url https://www.heise.de/ --output capture.html`
  - Upload the capture as a build artifact
- [x] Document the workflow in the project README

## Phase 5 — Distribution & Packaging

- [ ] Publish as a .NET global tool (`dotnet tool install -g broiler-capture`)
- [ ] Provide self-contained builds for users without .NET SDK:
  - `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`
- [ ] Add installation instructions to README
- [ ] Write user-facing documentation with usage examples

---

## Usage Examples (Target CLI)

```bash
# Capture website content
broiler-capture --url https://www.heise.de/ --output heise.html

# Full content capture with custom timeout
broiler-capture --url https://example.com --output page.html --full-page --timeout 60

# Test embedded engines
broiler-capture --test-engines

# Show help
broiler-capture --help
```

---

## Acceptance Criteria

1. The CLI tool runs on Windows, macOS, and Linux without modification.
2. Given a valid URL, the tool produces captured HTML content of the website.
3. Errors (invalid URL, timeout, HTTP failure) are reported with clear,
   actionable messages.
4. The Copilot instruction file documents the post-issue capture workflow.
5. CI automatically captures `https://www.heise.de/` after each successful
   build and test run.

---

## Related Tests

| Test File | Project | Validates |
|-----------|---------|-----------|
| `ProgramTests.cs` | `Broiler.Cli.Tests` | CLI argument parsing, option handling |
| `CaptureIntegrationTests.cs` | `Broiler.Cli.Tests` | End-to-end capture producing files on disk |
| `CliOutputValidationTests.cs` | `Broiler.Cli.Tests` | HTML/PNG/JPEG output format validation |
| `ImageCaptureTests.cs` | `Broiler.Cli.Tests` | Image capture format and dimension tests |
| `HeiseCaptureTests.cs` | `Broiler.Cli.Tests` | Live-site capture with retry (heise.de) |
| `EngineTestServiceTests.cs` | `Broiler.Cli.Tests` | `--test-engines` command smoke tests |
