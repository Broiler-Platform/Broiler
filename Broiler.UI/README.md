# Broiler.UI

Broiler.UI is the platform-neutral retained-mode UI component for Broiler
application chrome and general-purpose widgets.

This component is currently at Phase 7 of
[`docs/roadmap/broiler-ui-component.md`](../docs/roadmap/broiler-ui-component.md):
dialogs and host-service parity. It defines the platform-neutral root, shared
Standard infrastructure, and type-specific control pairs through Window, Panel,
Label, Button, Edit, CheckBox, RadioButton, ToggleButton, Slider, ProgressBar,
ImageView, ScrollView, ListView, ComboBox, TabView, Menu, Tooltip, Dialog,
FileDialog, FontDialog, and Toolbar.

## Projects

```text
src/Foundation
  Broiler.UI
  Broiler.UI.Standard

src/Abstractions/Shell
  Broiler.UI.Window
  Broiler.UI.Dialog
  Broiler.UI.Tooltip
  Broiler.UI.FileDialog
  Broiler.UI.FontDialog

src/Abstractions/Layout
  Broiler.UI.Panel
  Broiler.UI.ScrollView
  Broiler.UI.TabView

src/Abstractions/Content
  Broiler.UI.Label
  Broiler.UI.ImageView
  Broiler.UI.ProgressBar

src/Abstractions/Commands
  Broiler.UI.Button
  Broiler.UI.ToggleButton
  Broiler.UI.Toolbar
  Broiler.UI.Menu

src/Abstractions/ValueAndSelection
  Broiler.UI.CheckBox
  Broiler.UI.RadioButton
  Broiler.UI.Slider
  Broiler.UI.ListView
  Broiler.UI.ComboBox

src/Abstractions/Text
  Broiler.UI.Edit
  Broiler.UI.RichEdit

src/Implementations/Standard/Shell
  Broiler.UI.Window.Standard
  Broiler.UI.Dialog.Standard
  Broiler.UI.Tooltip.Standard
  Broiler.UI.FileDialog.Standard
  Broiler.UI.FontDialog.Standard

src/Implementations/Standard/Layout
  Broiler.UI.Panel.Standard
  Broiler.UI.ScrollView.Standard
  Broiler.UI.TabView.Standard

src/Implementations/Standard/Content
  Broiler.UI.Label.Standard
  Broiler.UI.ImageView.Standard
  Broiler.UI.ProgressBar.Standard

src/Implementations/Standard/Commands
  Broiler.UI.Button.Standard
  Broiler.UI.ToggleButton.Standard
  Broiler.UI.Toolbar.Standard
  Broiler.UI.Menu.Standard

src/Implementations/Standard/ValueAndSelection
  Broiler.UI.CheckBox.Standard
  Broiler.UI.RadioButton.Standard
  Broiler.UI.Slider.Standard
  Broiler.UI.ListView.Standard
  Broiler.UI.ComboBox.Standard

src/Implementations/Standard/Text
  Broiler.UI.Edit.Standard
  Broiler.UI.RichEdit.Standard

src/Integrations/RichEdit
  Broiler.UI.RichEdit.Rtf

src/Bundles
  Broiler.UI.All

samples
  Broiler.UI.Win32.Demo
  Broiler.UI.Linux.Demo
  Broiler.UI.RichEdit.Win32.Demo

tests
  Broiler.UI.Tests
  Broiler.UI.Standard.Tests
  Broiler.UI.Toolbar.Tests
  Broiler.UI.RichEdit.Tests
  Broiler.UI.RichEdit.Standard.Tests
  Broiler.UI.RichEdit.Rtf.Tests
```

`Broiler.UI` references only the platform-neutral `Broiler.Graphics` core and
platform-neutral `Broiler.Input` abstractions for keyboard, mouse, touch, pen,
text, and composition. `Broiler.UI.Standard` contains shared standard-control
infrastructure only; it does not contain public concrete controls. Type-specific
standard controls live in their own `.Standard` assemblies.

## Phase 0 records

- [`docs/phase0/decisions.md`](docs/phase0/decisions.md) freezes the approved
  ownership split, initial assembly names, and first control matrix.
- [`docs/phase0/current-state-inventory.md`](docs/phase0/current-state-inventory.md)
  records the current `BWindow`, `BControl`, input callback, timer, and native
  control consumers.
- [`docs/phase0/browser-baselines.md`](docs/phase0/browser-baselines.md) records
  the browser chrome baseline used for later migration comparisons.
- [`docs/phase0/input-boundary.md`](docs/phase0/input-boundary.md) aligns UI
  with the Broiler.Input roadmap and the temporary legacy Graphics callback
  adapter.
- [`docs/phase0/package-versioning-and-compatibility.md`](docs/phase0/package-versioning-and-compatibility.md)
  records repository, package, and compatibility-window decisions.
- [`docs/phase0/directory-structure-path-inventory.md`](docs/phase0/directory-structure-path-inventory.md)
  records the path-sensitive surfaces for the directory-structure refactor.
- [`docs/adr`](docs/adr) contains the ADRs required by roadmap section 20.

## Phase 1 records

- [`docs/phase1/implementation-record.md`](docs/phase1/implementation-record.md)
  records the runtime scaffold and validation gates.
- [`docs/phase1/phase1-boundary.json`](docs/phase1/phase1-boundary.json)
  captures the executable dependency boundary.

## Phase 2 records

- [`docs/phase2/implementation-record.md`](docs/phase2/implementation-record.md)
  records the Standard rendering, input, focus, theme, semantics, and scheduling
  services.
- [`docs/phase2/phase2-boundary.json`](docs/phase2/phase2-boundary.json)
  captures the expanded platform-neutral Input boundary and temporary legacy
  adapter gate.

## Phase 3 records

- [`docs/phase3/implementation-record.md`](docs/phase3/implementation-record.md)
  records the Window, Panel, Label, text-measurement, and app-host preview work.
- [`docs/phase3/phase3-boundary.json`](docs/phase3/phase3-boundary.json)
  captures the per-type control assembly boundary.

## Phase 4 records

- [`docs/phase4/implementation-record.md`](docs/phase4/implementation-record.md)
  records the Button, Edit, richer input, clipboard host, and toolbar proof work.
- [`docs/phase4/phase4-boundary.json`](docs/phase4/phase4-boundary.json)
  captures the Button/Edit assembly boundary and proof-toolbar compatibility
  gate.

## Phase 5 records

- [`docs/phase5/implementation-record.md`](docs/phase5/implementation-record.md)
  records the CheckBox, RadioButton, ToggleButton, Slider, ProgressBar, and
  ImageView work.
- [`docs/phase5/phase5-boundary.json`](docs/phase5/phase5-boundary.json)
  captures the per-control dependency boundary and ImageView handle-only
  contract.

## Phase 6 records

- [`docs/phase6/implementation-record.md`](docs/phase6/implementation-record.md)
  records the ScrollView, ListView, ComboBox, TabView, Menu, Tooltip, wheel
  input, virtualization, and managed popup work.
- [`docs/phase6/phase6-boundary.json`](docs/phase6/phase6-boundary.json)
  captures the per-control dependency boundary and approved Tooltip-to-Window
  abstraction edge.

## Phase 7 records

- [`docs/phase7/implementation-record.md`](docs/phase7/implementation-record.md)
  records the Dialog pair, modal/modeless result lifecycle, Edit caret geometry
  publication, semantic text metadata, and neutral host-service ports.
- [`docs/phase7/phase7-boundary.json`](docs/phase7/phase7-boundary.json)
  captures the Dialog dependency boundary, stable host-port scope, and
  host-only Windows accessibility/IME evidence gates.

## Graphics boundary

Broiler.UI standard controls draw through the platform-neutral
`Broiler.Graphics` core. UI runtime assemblies must not reference
`Broiler.Graphics.Windows`, Direct2D, Win32, WPF, WinForms, COM, HWND, or any
other native UI backend. Windows applications compose the selected Graphics
backend outside Broiler.UI.

## Linux demo

`Broiler.UI.Linux.Demo` is the Linux sibling of `Broiler.UI.Win32.Demo`. It
hosts standard controls through `Broiler.Graphics.Linux.OpenGL` and can bridge
first-round keyboard/mouse input from evdev when an X11 window has focus.
Windows-only camera and microphone previews remain intentionally outside this
Linux first pass.

```bash
dotnet run --project Broiler.UI/samples/Linux/Broiler.UI.Linux.Demo/Broiler.UI.Linux.Demo.csproj --configuration Release
dotnet run --project Broiler.UI/samples/Linux/Broiler.UI.Linux.Demo/Broiler.UI.Linux.Demo.csproj --configuration Release -- --window --input --interactive
dotnet publish Broiler.UI/samples/Linux/Broiler.UI.Linux.Demo/Broiler.UI.Linux.Demo.csproj --configuration Release --runtime linux-x64 --self-contained true -p:PublishSingleFile=true
```

## Validation

```powershell
dotnet test Broiler.UI\Broiler.UI.slnx
dotnet build Broiler.Input\Broiler.Input.slnx
dotnet run --project Broiler.Graphics\Broiler.Graphics.Tests\Broiler.Graphics.Tests.csproj
dotnet build Broiler.UI\samples\Linux\Broiler.UI.Linux.Demo\Broiler.UI.Linux.Demo.csproj -c Release
```

The test projects use the `Broiler.Graphics` submodule only for
platform-neutral graphics API characterization and rendering records.
