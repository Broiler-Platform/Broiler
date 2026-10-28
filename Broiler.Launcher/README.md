# Broiler Launcher

Unified workspace launcher application for the Broiler Platform suite: **Broiler Browser**, **Broiler Writer**, **Broiler Cell**, and **Broiler Plate**.

Built entirely on Broiler platform components (`Broiler.UI`, `Broiler.Hosting`, `Broiler.Graphics`, and `Broiler.Input`) with a multi-target architecture supporting **Windows** and **Linux**.

---

## Supported Applications

| Application | Role | Component / Executable |
| :--- | :--- | :--- |
| **Broiler Browser** | Fast web browser powered by the native HTML, CSS, DOM, and JS engine | `Broiler.Browser.Windows.exe` / `Broiler.Browser.Linux` |
| **Broiler Writer** | Distraction-free word processor with pagination and multi-format export | `Broiler.Writer.Windows.exe` / `Broiler.Writer.Linux` |
| **Broiler Cell** | Grid-based spreadsheet editor for tabular data, calculations, and CSV files | `Broiler.Cells.Windows.exe` / `Broiler.Cells.Windows.dll` |
| **Broiler Plate** | Hardware-accelerated document and graphics viewer (PDF, Markdown, Images) | `Broiler.Plate.Windows.exe` / `Broiler.Plate.Windows.dll` |

For an in-depth reference on how and where the launcher discovers binaries, see the [Application Discovery Guide](docs/application-discovery.md).

---

## Architecture & Multi-Target Design

The project is structured into modular layers following Broiler platform conventions:

```
Broiler.Launcher/
├── Directory.Build.props          # Compiler, target frameworks, and configuration settings
├── Directory.Packages.props       # Central Package Management (Broiler.UI, Broiler.Hosting, etc.)
├── NuGet.config                   # NuGet package sources configuration
├── Broiler.Launcher.slnx          # Combined solution
├── Broiler.Windows.Launcher.slnx  # Windows desktop head solution
├── Broiler.Linux.Launcher.slnx    # Linux desktop head solution
├── src/
│   ├── Broiler.Launcher.Core/     # Shared models, discovery service, Broiler.UI dashboard, CLI parser
│   ├── Broiler.Launcher.Windows/  # Windows Win32 + Direct2D host using Broiler.Hosting.Windows
│   └── Broiler.Launcher.Linux/    # Linux X11/OpenGL host using Broiler.Hosting.Linux & evdev input
└── tests/
    └── Broiler.Launcher.Tests/    # Unit tests for discovery, metadata, and CLI commands
```

### Components Used

- **`Broiler.UI` / `Broiler.UI.Standard`**: Retained-mode UI toolkit providing `UiSession`, `StandardWindow`, `StandardMenu`, `StandardButton`, `StandardLabel`, `StandardAboutDialog`, and `StandardThemeController`.
- **`Broiler.Hosting.Windows`**: Win32 window hosting, `WindowsInputBridge`, `WindowsAutomationBridge`, DPI awareness (`PER_MONITOR_AWARE_V2`), and sizing.
- **`Broiler.Hosting.Linux`**: Linux desktop hosting, `LinuxUiHost`, `LinuxX11Clipboard`, and `LinuxInputCoordinator`.
- **`Broiler.Graphics.Windows`**: `Direct2DWindow` hardware-accelerated Direct2D rendering.
- **`Broiler.Graphics.Linux.OpenGL`**: `LinuxOpenGlRenderer` and `LinuxOpenGlX11WindowSurface` for hardware-accelerated OpenGL desktop rendering.

---

## Build & Run

### Prerequisites

- .NET 10.0 SDK

### Building

```shell
# Build all launcher projects
dotnet build Broiler.Launcher/Broiler.Launcher.slnx

# Or build platform-specific solutions
dotnet build Broiler.Launcher/Broiler.Windows.Launcher.slnx
dotnet build Broiler.Launcher/Broiler.Linux.Launcher.slnx
```

### Running on Windows

```shell
# Run graphical launcher window
dotnet run --project Broiler.Launcher/src/Broiler.Launcher.Windows

# Or launch directly from command line
dotnet run --project Broiler.Launcher/src/Broiler.Launcher.Windows -- --app browser
dotnet run --project Broiler.Launcher/src/Broiler.Launcher.Windows -- --list
```

### Running on Linux

```shell
# Run interactive X11 window
dotnet run --project Broiler.Launcher/src/Broiler.Launcher.Linux -- --window

# Run offscreen / headless (Linux defaults to an interactive window)
dotnet run --project Broiler.Launcher/src/Broiler.Launcher.Linux -- --offscreen

# Direct CLI launch
dotnet run --project Broiler.Launcher/src/Broiler.Launcher.Linux -- --app writer
```

### Running Unit Tests

```shell
dotnet test Broiler.Launcher/tests/Broiler.Launcher.Tests
```

---

## Customization & Settings Page

The launcher provides a dedicated Settings page to configure executable paths and command-line arguments:
- **Auto-Detect**: Automatically probes local directories, the workspace repository tree, and the system `PATH`.
- **Custom Executable Paths**: Set explicit paths manually or pick files via a native Broiler file dialog (`StandardFileDialog`).
- **Custom Launch Arguments**: Configure application-specific startup arguments (e.g. `--profile dev`, `--url ...`).
- **Persistent Storage**: Settings are persisted to JSON (`launcher.settings.json` in user `%APPDATA%\Broiler` or portable root directory).

To open Settings:
- Click the `"⚙ Settings"` button on the dashboard header.
- Choose `Tools` → `Settings...` in the menu.
- Press <kbd>Ctrl</kbd> + <kbd>,</kbd>.

---

## Keyboard Shortcuts

- <kbd>Ctrl</kbd> + <kbd>1</kbd>: Launch Broiler Browser
- <kbd>Ctrl</kbd> + <kbd>2</kbd>: Launch Broiler Writer
- <kbd>Ctrl</kbd> + <kbd>3</kbd>: Launch Broiler Cell
- <kbd>Ctrl</kbd> + <kbd>4</kbd>: Launch Broiler Plate
- <kbd>Ctrl</kbd> + <kbd>,</kbd>: Open Settings
- <kbd>F5</kbd>: Refresh status of installed applications
- <kbd>Escape</kbd>: Return from Settings to Dashboard
- <kbd>Alt</kbd> + <kbd>F4</kbd>: Exit launcher

## Platform and argument contracts

Linux supports Browser and Writer. Cell and Plate currently have Windows heads only,
including when a custom executable is configured. Discovery uses custom override,
local directory, ordered workspace ancestors (base folder first, then current
folder), then PATH. Managed user/machine discovery is reserved for P1.

CLI argument tokens are preserved verbatim. Settings arguments use whitespace
separators and double quotes for grouping, including empty arguments. Backslashes
are literal except before double quotes, where pairs encode a backslash and an odd
final backslash escapes the quote. Single quotes are literal; shell expansion is
never performed. Unclosed double quotes are rejected on save and launch.

Settings writes atomically replace the file, keeping its previous bytes in `.bak`.
Load errors appear on refresh; the original file remains available for recovery.
See [P0 status and P1 contracts](docs/p0-status.md).
