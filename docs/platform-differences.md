# Platform Differences – WPF vs Avalonia

This document records known rendering and behaviour differences between the
WPF (`Broiler.App`) and Avalonia (`Broiler.Avalonia`) desktop applications.
It should be kept up-to-date as new discrepancies are discovered or resolved.

---

## Font Metrics

| Area | Notes |
|------|-------|
| **Text measurement** | Avalonia uses Skia-based text shaping while WPF uses DirectWrite. Line heights and character widths may differ by 1–2 pixels at common sizes. |
| **Generic family mapping** | Both adapters map CSS generic families (`sans-serif`, `serif`, `monospace`) to the first available system font. The resolved font may differ across OSes (e.g., "DejaVu Sans" on Linux vs "Arial" on Windows). |
| **Acceptable variance** | Differential tests should allow a per-pixel colour tolerance of **5 RGB units** and an overall content-area match threshold of **≥ 89 %** when comparing across platforms. |

## Clipboard

| Feature | WPF | Avalonia |
|---------|-----|----------|
| Plain-text copy | ✅ Supported | ✅ Supported |
| HTML fragment copy (CF_HTML) | ✅ Windows-native format with byte-accurate offsets | ❌ Not supported cross-platform; falls back to plain text |
| Image copy | ✅ `Clipboard.SetImage()` | ❌ Not supported; no-op |

The CF_HTML clipboard format is a Windows-specific convention. Avalonia's
`IClipboard` API exposes only text operations cross-platform, so the HTML
fragment format is not available on Linux or macOS.

## Context Menu

Both platforms now implement `RContextMenu` using native menu controls.

| Feature | WPF | Avalonia |
|---------|-----|----------|
| Menu items | `System.Windows.Controls.MenuItem` | `Avalonia.Controls.MenuItem` |
| Dividers | `System.Windows.Controls.Separator` | `Avalonia.Controls.Separator` |
| Positioning | `PlacementRectangle` for exact pixel placement | Automatic positioning relative to the target control |

## Scrollbar Behaviour

Both `HtmlPanel` implementations manage their own `ScrollBar` children rather
than wrapping the content in a `ScrollViewer`. Scrolling behaviour is
equivalent; however, the scrollbar chrome may look different depending on the
Avalonia theme in use.

## Timer / Dispatcher

| Aspect | WPF | Avalonia |
|--------|-----|----------|
| Timer class | `System.Windows.Threading.DispatcherTimer` | `Avalonia.Threading.DispatcherTimer` |
| Default interval | 16 ms (~60 fps) | 16 ms (~60 fps) |
| `DispatcherPriority` | `Render` | `Render` |

`InteractiveSession` and `DomBridge.FlushTimerStep()` are platform-neutral;
they have been verified to produce equivalent frame-stepping behaviour under
both dispatchers.

## Image Loading

| Source | WPF | Avalonia |
|--------|-----|----------|
| HTTP / HTTPS | ✅ `HttpClient` | ✅ `HttpClient` |
| `data:` URI | ✅ Decoded in `ImageDownloader` | ✅ Same path; Avalonia `Bitmap(Stream)` |
| `file://` | ✅ Loaded via `BitmapImage` | ✅ Loaded via `Bitmap(Stream)` |

Both adapters use the shared `ImageDownloader` which is platform-agnostic.

## Drag and Drop

Avalonia's drag-drop API is asynchronous, so `ControlAdapter.DoDragDropCopy()`
is currently a no-op. Users cannot drag selected content out of the panel
in the Avalonia build.

## XAML Dialect

| Aspect | WPF | Avalonia |
|--------|-----|----------|
| File extension | `.xaml` | `.axaml` |
| Default namespace | `http://schemas.microsoft.com/winfx/2006/xaml/presentation` | `https://github.com/avaloniaui` |
| `x:Name` on `RowDefinition` | Generates code-behind field | **Does not** generate a field; access by index |
| `Background` on `Control` | Built-in property | Not on base `Control`; use a `StyledProperty` |
| Event handler binding | Loose `EventHandler` delegate match | Strict signature match required |
