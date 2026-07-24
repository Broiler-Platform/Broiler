# Broiler.UI

Broiler.UI is the platform-neutral retained-mode UI component for Broiler
application chrome and general-purpose widgets.

The component defines the platform-neutral root, shared Standard infrastructure,
and type-specific control pairs through Window, Panel, Label, Button, Edit,
CheckBox, RadioButton, ToggleButton, Slider, ProgressBar, ImageView, ScrollView,
ListView, ComboBox, TabView, Menu, Tooltip, Dialog, FileDialog, FontDialog,
Toolbar, RichEdit, and Formatting Codes view. Current migration, UX, RichEdit,
and stabilization work is tracked in the
[component roadmap](docs/roadmap.md).

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
  Broiler.UI.Splitter
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
  Broiler.UI.FormatCodeView
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
  Broiler.UI.Splitter.Standard
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
  Broiler.UI.FormatCodeView.Standard
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
  Broiler.UI.Splitter.Tests
  Broiler.UI.FormatCodeView.Tests
  Broiler.UI.FormatCodeView.Standard.Tests
  Broiler.UI.RichEdit.Tests
  Broiler.UI.RichEdit.Standard.Tests
  Broiler.UI.RichEdit.Rtf.Tests
```

`Broiler.UI` references only the platform-neutral `Broiler.Graphics` core and
platform-neutral `Broiler.Input` abstractions for keyboard, mouse, touch, pen,
text, and composition. `Broiler.UI.Standard` contains shared standard-control
infrastructure only; it does not contain public concrete controls. Type-specific
standard controls live in their own `.Standard` assemblies.

## Documentation

- [Current roadmap](docs/roadmap.md)
- [ADR index](docs/adr/README.md)
- [Pending human review](HUMAN_REVIEW.md)

Repository topology and dependency rules are enforced by the component's
architecture/topology tests. Completed implementation-phase records are not
maintained as current documentation.

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
