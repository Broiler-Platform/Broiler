# Broiler.UI WebAssembly Demo

A standalone .NET 10 Browser App that hosts the **same Standard control gallery as
`Broiler.UI.Win32.Demo`**, running on the real managed `UiSession` and presented through the
Phase 5 direct-Canvas 2D backend (`Broiler.Graphics.WebAssembly.BrowserCanvasRenderer`).

The gallery composes window, panel, toolbar, label, button, edit, check box, radio button,
toggle button, slider, progress bar, image view, combo box, list view, tab view, scroll view,
tooltip, dialog, and menu — plus the custom sidebar/card chrome — exactly like the Win32 sample.
Input is routed through the `Broiler.Input` contracts: browser Pointer Events, wheel, keyboard
with modifiers, committed/composition text from a hidden native editor, and trusted-event
clipboard. `Ctrl+D` toggles light/dark across the whole session; the progress bar is driven by the
browser animation-frame tick.

## Functional differences from the Win32 sample

The Win32 gallery's **camera and microphone live preview** uses the Windows-only
`Broiler.Input.Camera.Windows` / `Broiler.Input.Microphone.Windows` providers, which have no
browser equivalent in the referenced libraries. That card is preserved in the same layout but its
status labels and buttons show **"Media capture is not available in the browser build."**

Everything else — every control, the theming, the dialog, the tooltip, the animated progress bar,
scrolling, and text editing — behaves as in the Win32 sample.

## Architecture

- `Program` awaits `BrowserGalleryApp.StartAsync`, which imports the direct-Canvas replay module,
  binds `BrowserCanvasRenderer` to `#gallery-canvas`, and builds `BrowserGalleryDemo`.
- `BrowserGalleryDemo` is the browser counterpart of `Win32DemoWindow`: it builds the identical
  control tree, hosts it on a `StandardUiSessionBuilder` session, runs a `StandardAnimationScheduler`,
  and dispatches input through the `Broiler.Input` event contracts.
- `BrowserCanvasUiHost` implements `IUiHost` (+ text/clipboard/cursor/system-settings) and presents
  each `BRenderList` straight through the Canvas 2D backend — no CPU pixel copy across the boundary.
- `main.js` bridges input, animation-frame scheduling, `ResizeObserver`, cursor, caret-driven text,
  and clipboard. The managed side owns pixel presentation, so the page never touches `ImageData`.

## Publish and serve

```powershell
dotnet workload install wasm-tools

dotnet publish Broiler.UI/samples/WebAssembly/Broiler.UI.WebAssembly.Demo/Broiler.UI.WebAssembly.Demo.csproj `
  -c Release -p:PublishTrimmed=true -p:RunAOTCompilation=false `
  -o artifacts/wasm-gallery

python -m http.server 8766 --bind 127.0.0.1 `
  --directory artifacts/wasm-gallery/wwwroot
```

Open `http://127.0.0.1:8766/`.

## Publish modes

- interpreted/untrimmed: `-p:PublishTrimmed=false -p:RunAOTCompilation=false`
- interpreted/trimmed: `-p:PublishTrimmed=true -p:RunAOTCompilation=false`
- full AOT: `-p:PublishTrimmed=true -p:RunAOTCompilation=true`
