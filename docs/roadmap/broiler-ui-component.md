# Broiler.UI Component Roadmap

**Status:** Proposed  
**Date:** 2026-06-27  
**Scope:** Architecture, assembly boundaries, migration, and delivery plan only. This document contains no implementation.

## 1. Executive decision

Create `Broiler.UI` as a platform-neutral, retained-mode user-interface component
family that manages control trees, logical top-level windows, child windows,
subwindows, focus, input routing, layout, rendering, and control state.

The component has four defining rules:

1. `Broiler.UI` defines exactly one abstract root class, `UiElement`.
2. Every independently instantiable UI type has exactly one public abstract base
   class in its own abstraction assembly.
3. Every standard concrete UI type has its own implementation assembly. There is
   no monolithic `Broiler.UI.Controls` or `Broiler.UI.Windows.All` runtime
   assembly containing all control implementations.
4. Every Broiler.UI runtime assembly is operating-system-neutral. It may reference
   only the platform-neutral Broiler.Graphics and Broiler.Input component
   families plus other Broiler.UI assemblies and the .NET base libraries. It may
   not reference Win32, WPF, WinForms, Direct2D, COM, HWND, Windows UI Automation,
   or any `*.Windows` implementation assembly.

Windows is the first supported deployment, but it is a composition target, not a
namespace or API shape inside Broiler.UI. A Windows application composes:

- a Broiler.Graphics Windows backend;
- the required Broiler.Input Windows providers;
- the platform-neutral Broiler.UI standard implementations; and
- a small application-owned host adapter that supplies surfaces, invalidation,
  dispatch, window lifetime, clipboard, cursor, accessibility, and other native
  capabilities through platform-neutral ports.

Broiler.UI does not own or expose an HWND. A `UiWindow` is a logical UI window.
A top-level logical window may be hosted by one native window, while dialogs,
menus, tooltips, and other subwindows normally render as managed layers within a
host surface. A host may map additional logical windows to native windows, but
that mapping remains outside Broiler.UI.

The first usable vertical slice is deliberately small: `Window`, `Panel`,
`Label`, `Button`, and `Edit`. It must be possible to build a Windows browser
toolbar with these standard implementations before expanding the control set.

## 2. Current-state findings

### 2.1 UI responsibilities currently live in Broiler.Graphics

The current Graphics component contains several UI-shaped abstractions:

- `BControl` exposes bounds, text, enabled/visible state, focus, disposal, and a
  native handle;
- `BButtonControl` adds a clicked event;
- `BEditControl` adds text-changed and submitted events;
- `BControlOptions` describes control construction;
- `BWindow` owns window options, a native handle, client size, DPI, renderer,
  surface, run loop, invalidation, control creation, animation timers, rendering,
  and input callbacks; and
- `Direct2DWindow` creates Win32 `EDIT` and `BUTTON` child windows and translates
  Windows messages directly into Graphics input callbacks.

This proves the immediate product need, but it combines four responsibilities
that should evolve independently:

| Current responsibility | Target owner |
|---|---|
| Render lists, surfaces, drawing, text measurement, and presentation | Broiler.Graphics |
| Keyboard, pointer, touch, pen, and text/composition observations | Broiler.Input |
| Control tree, focus, layout, interaction state, widgets, and logical windows | Broiler.UI |
| Native window creation, message loop, HWND, UIA, clipboard, cursor, and OS integration | Windows application/host composition |

### 2.2 Existing control contracts leak the operating system

`BControl.NativeHandle` and `BWindow.NativeHandle` are `IntPtr` values. The
Direct2D control implementation creates and destroys Win32 child HWNDs directly.
Those handles cannot move into Broiler.UI because doing so would make the shared
contract Windows-shaped even if the type names were platform-neutral.

The migration therefore cannot be a namespace-only move. The new controls are
Broiler-drawn controls with platform-neutral state and behavior. Existing native
controls remain a time-boxed compatibility path until the new `Edit` reaches the
required text, caret, selection, IME, accessibility, and clipboard quality gates.

### 2.3 Existing input is transitional

Graphics currently translates a cooked subset of mouse, wheel, keyboard, and
character messages. The proposed
[Broiler.Input component roadmap](broiler-input-component.md) moves those
responsibilities into typed, platform-neutral input abstractions with separate
Windows implementations.

Broiler.UI should consume the normalized Input contracts, not copy the current
Graphics event classes and not parse Windows messages. Until Input extraction is
complete, a migration adapter may translate the legacy Graphics callbacks into
the same internal UI event pipeline. That adapter must be removable and must not
become the permanent public contract.

### 2.4 Application chrome is the first real consumer

`src/Broiler.App.Graphics/BrowserWindow.cs` currently builds its navigation bar
with `BButtonControl` and `BEditControl`. This is the first migration target
because it exercises:

- layout and resizing;
- pointer and keyboard activation;
- focus traversal;
- editable text and submission;
- dynamic favorite buttons;
- enabled/disabled command state;
- invalidation and rendering; and
- coexistence with the browser content surface.

The browser content DOM is not part of Broiler.UI. Broiler.UI owns application
chrome and general-purpose widgets; HTML, CSS, DOM events, and form-control
semantics remain in their existing browser layers unless a separate adapter
chooses to reuse a Broiler.UI control.

### 2.5 Repository topology affects sequencing

Broiler.Graphics is present in more than one checkout in the aggregate workspace.
Removing control and input types from Graphics before all consumers are migrated
would create revision drift and broken standalone builds. Changes must land in
provider-first order: new UI contracts and packages, compatibility adapters,
consumer migration, Graphics cleanup, and finally aggregate submodule pointer
updates.

## 3. Goals

1. Define one abstract root class, `UiElement`, in `Broiler.UI`.
2. Define one abstract base class per independently instantiable UI type, each in
   its own assembly.
3. Put each standard concrete UI implementation in a separate assembly paired
   with its abstraction assembly.
4. Manage logical top-level windows, owned windows, dialogs, popups, menus,
   tooltips, and other subwindows without exposing native window handles.
5. Provide a retained UI tree with deterministic parentage, attachment,
   measurement, arrangement, rendering, input, focus, and disposal behavior.
6. Render all standard controls through platform-neutral Broiler.Graphics APIs.
7. Consume keyboard, mouse, touch, pen, text, and composition data only through
   platform-neutral Broiler.Input abstractions.
8. Keep all Windows-specific behavior in Graphics/Input Windows providers and
   the Windows application host, never in a Broiler.UI runtime assembly.
9. Replace the current Graphics-owned button and edit abstractions without a
   flag-day migration.
10. Make every standard control testable with an in-memory graphics target, a
    deterministic clock, and replayable input.
11. Support themes, accessibility semantics, localization, high DPI, and
    right-to-left layout as platform-neutral data and behavior.
12. Keep the API usable by applications that select only a few controls; adding a
    button must not pull list, menu, dialog, or text-editor implementations into
    the dependency graph.
13. Establish architecture tests that make the assembly and OS-neutrality rules
    executable release gates.

## 4. Non-goals

- Replacing Broiler.Graphics rendering primitives, surfaces, renderers, image
  codecs, or backend selection.
- Replacing Broiler.Input device discovery, device capture, key normalization,
  or pointer normalization.
- Creating HWNDs, processing Win32 messages, initializing COM, calling WPF or
  WinForms, or shipping a `Broiler.UI.Windows` implementation.
- Owning the process message loop or deciding when the process exits.
- Implementing an HTML DOM, CSS cascade, browser form controls, or browser event
  propagation in Broiler.UI.
- Referencing Broiler.Layout. Broiler.UI has a small widget layout protocol suited
  to controls; webpage layout remains a separate concern.
- Providing a visual designer, markup language, XAML clone, CSS theme engine, or
  dependency-property system in the first stable release.
- Supporting arbitrary native child controls inside the managed tree in the
  first release.
- Making every logical subwindow a native operating-system window.
- Defining platform clipboard, drag-and-drop, cursor, accessibility, or IME APIs
  in terms of native handles or Windows structures.
- Guaranteeing source compatibility with `BControl`, `BWindow`, or their current
  event argument types. Compatibility is explicit, temporary, and adapter-based.
- Shipping all standard controls as one required package.
- Using reflection-based control discovery, module initializers, or mutable
  process-wide registries to choose implementations.

## 5. Terminology and responsibility rules

| Term | Meaning |
|---|---|
| UI element | Any logical object derived from `UiElement` and attached to at most one UI tree |
| Control type | An independently instantiable element with a public behavior contract, such as Window, Button, or Edit |
| Abstraction assembly | Assembly containing one primary abstract control base and its type-specific contracts |
| Implementation assembly | Assembly containing the standard concrete implementation for exactly one control type |
| Standard implementation | Broiler-drawn, platform-neutral reference behavior shipped under a `.Standard` suffix |
| Host | Application-owned adapter that supplies a graphics target, invalidation, dispatch, and optional native capabilities |
| Logical window | A `UiWindow` with content, owner, state, focus scope, and z-order, independent of a native handle |
| Subwindow | A logical child/owned window such as a dialog, popup, menu, or tooltip |
| Viewport | One drawable host area with size, scale, graphics target, and an input route into one UI session |
| UI session | Owner of one or more logical windows, their routing/focus state, render scheduling, and host bindings |
| Control part | Internal drawing/interaction detail that is not independently instantiated and therefore is not a public control type |
| Semantic tree | Platform-neutral accessibility roles, names, values, states, actions, and relationships |

The following rules are mandatory:

1. Every `UiElement` has zero or one logical parent and is attached to zero or one
   `UiSession`.
2. A child cannot be inserted into two trees, and cycles are rejected before a
   tree mutation becomes visible.
3. Public control state may be changed only on the owning UI context unless the
   API explicitly represents an immutable snapshot.
4. Bounds are expressed in device-independent logical units. Pixel conversion
   belongs at the Graphics host boundary.
5. Controls never expose native handles.
6. A logical window does not imply a native top-level window.
7. Input events carry normalized Input identities and timestamps; they do not
   carry Windows message numbers, virtual-key constants, or HWNDs.
8. Rendering is side-effect-free with respect to logical state. Event handlers do
   not execute while a render traversal holds internal locks.
9. Public events are ordered, cancellation-aware where relevant, and raised on
   the owning UI context.
10. A disabled or hidden element does not become an input target. Hidden elements
    do not participate in layout unless the visibility mode explicitly reserves
    space.
11. Focus, pointer capture, modality, and z-order are session concepts rather than
    static global state.
12. Clipboard, cursor, accessibility, drag-and-drop, and native-window operations
    are optional host capabilities. A missing capability produces documented
    degraded behavior, not a platform probe inside a control.
13. Helper parts do not become public control types merely to share code. If a
    helper becomes independently instantiable public UI, it receives its own
    abstraction and implementation assembly pair.

## 6. Target component and assembly structure

### 6.1 Shared runtime assemblies

| Assembly | Purpose | Allowed Broiler references |
|---|---|---|
| `Broiler.UI` | `UiElement`, tree/session primitives, layout protocol, routed event vocabulary, host ports, semantic contracts | `Broiler.Graphics`, `Broiler.Input` |
| `Broiler.UI.Standard` | Shared retained-mode implementation support, theme tokens, render traversal, focus/routing services, clocks, animation scheduler | `Broiler.UI`, platform-neutral `Broiler.Graphics`, required platform-neutral `Broiler.Input.*` abstractions |

`Broiler.UI.Standard` is support infrastructure, not a control bundle. It must not
contain a public `StandardButton`, `StandardEdit`, or any other type-specific
concrete control.

Every runtime assembly has a matching test assembly. Contract test helpers may
later be published as `Broiler.UI.Testing`, but ordinary runtime packages must
not depend on it.

### 6.2 Initial control assembly pairs

| UI type | Abstraction assembly and abstract class | Standard implementation assembly and concrete class | Stable-release role |
|---|---|---|---|
| Window | `Broiler.UI.Window` / `UiWindow` | `Broiler.UI.Window.Standard` / `StandardWindow` | Top-level logical windows, owned windows, subwindow root, z-order and close lifecycle |
| Panel | `Broiler.UI.Panel` / `UiPanel` | `Broiler.UI.Panel.Standard` / `StandardPanel` | Multi-child container with pluggable stack, dock, wrap, overlay, and grid-like policies |
| Label | `Broiler.UI.Label` / `UiLabel` | `Broiler.UI.Label.Standard` / `StandardLabel` | Non-interactive text and access-key labeling |
| Button | `Broiler.UI.Button` / `UiButton` | `Broiler.UI.Button.Standard` / `StandardButton` | Pointer/keyboard command activation and default/cancel roles |
| Edit | `Broiler.UI.Edit` / `UiEdit` | `Broiler.UI.Edit.Standard` / `StandardEdit` | Single-line and multiline text editing, selection, caret, undo, composition, and submission |
| CheckBox | `Broiler.UI.CheckBox` / `UiCheckBox` | `Broiler.UI.CheckBox.Standard` / `StandardCheckBox` | Two-state and optional three-state selection |
| RadioButton | `Broiler.UI.RadioButton` / `UiRadioButton` | `Broiler.UI.RadioButton.Standard` / `StandardRadioButton` | Exclusive selection within an explicit group scope |
| ToggleButton | `Broiler.UI.ToggleButton` / `UiToggleButton` | `Broiler.UI.ToggleButton.Standard` / `StandardToggleButton` | Stateful button behavior without checkbox presentation |
| Slider | `Broiler.UI.Slider` / `UiSlider` | `Broiler.UI.Slider.Standard` / `StandardSlider` | Bounded scalar selection with keyboard and pointer manipulation |
| ProgressBar | `Broiler.UI.ProgressBar` / `UiProgressBar` | `Broiler.UI.ProgressBar.Standard` / `StandardProgressBar` | Determinate and indeterminate progress presentation |
| ScrollView | `Broiler.UI.ScrollView` / `UiScrollView` | `Broiler.UI.ScrollView.Standard` / `StandardScrollView` | Scroll offsets, wheel/touch manipulation, clipping, and scrollbars |
| ListView | `Broiler.UI.ListView` / `UiListView` | `Broiler.UI.ListView.Standard` / `StandardListView` | Item presentation, selection, keyboard navigation, and virtualization |
| ComboBox | `Broiler.UI.ComboBox` / `UiComboBox` | `Broiler.UI.ComboBox.Standard` / `StandardComboBox` | Selection plus managed popup list |
| TabView | `Broiler.UI.TabView` / `UiTabView` | `Broiler.UI.TabView.Standard` / `StandardTabView` | Tab headers, selection, and one active content region |
| Menu | `Broiler.UI.Menu` / `UiMenu` | `Broiler.UI.Menu.Standard` / `StandardMenu` | Menu bar/context menu, nested submenus, keyboard navigation, and access keys |
| ImageView | `Broiler.UI.ImageView` / `UiImageView` | `Broiler.UI.ImageView.Standard` / `StandardImageView` | Display of an already available Graphics image resource |
| Dialog | `Broiler.UI.Dialog` / `UiDialog` | `Broiler.UI.Dialog.Standard` / `StandardDialog` | Owned modal/modeless window behavior and result completion |
| Tooltip | `Broiler.UI.Tooltip` / `UiTooltip` | `Broiler.UI.Tooltip.Standard` / `StandardTooltip` | Delayed transient descriptive subwindow |

The first stable release does not need every pair on day one. The table defines
the intended boundary so early shortcuts do not create a future monolith.

### 6.3 Abstract type hierarchy

The public hierarchy stays shallow:

```text
UiElement                         (Broiler.UI)
  UiWindow                        (Broiler.UI.Window)
    UiDialog                      (Broiler.UI.Dialog)
    UiTooltip                     (Broiler.UI.Tooltip)
  UiPanel                         (Broiler.UI.Panel)
  UiLabel                         (Broiler.UI.Label)
  UiButton                        (Broiler.UI.Button)
    UiToggleButton                (Broiler.UI.ToggleButton)
  UiEdit                          (Broiler.UI.Edit)
  UiCheckBox                      (Broiler.UI.CheckBox)
  UiRadioButton                  (Broiler.UI.RadioButton)
  UiSlider                        (Broiler.UI.Slider)
  UiProgressBar                   (Broiler.UI.ProgressBar)
  UiScrollView                    (Broiler.UI.ScrollView)
  UiListView                      (Broiler.UI.ListView)
  UiComboBox                      (Broiler.UI.ComboBox)
  UiTabView                       (Broiler.UI.TabView)
  UiMenu                          (Broiler.UI.Menu)
  UiImageView                     (Broiler.UI.ImageView)
```

Only `Dialog`, `Tooltip`, and `ToggleButton` have a type-specific abstract parent
above `UiElement`. All other controls derive directly from `UiElement`. Deeper
inheritance requires an ADR because behavior sharing should normally use
composition and internal parts rather than expanding the public hierarchy.

Each implementation assembly contains exactly one primary public concrete
control class corresponding to its abstract base. Internal renderers, state
machines, item containers, text helpers, and parts stay internal. Public option,
event, item-model, and value types may live beside the abstract base when they are
specific to that control.

### 6.4 Dependency direction

```text
Broiler.UI -> Broiler.Graphics
Broiler.UI -> Broiler.Input

Broiler.UI.<Type> -> Broiler.UI
Broiler.UI.<Type>.Standard -> Broiler.UI.<Type>
Broiler.UI.<Type>.Standard -> Broiler.UI.Standard

Windows application composition -> Broiler.Graphics.Windows
Windows application composition -> Broiler.Input.<Kind>.Windows
Windows application composition -> selected Broiler.UI.<Type>.Standard assemblies
```

An abstraction may reference another type abstraction only when its public type
hierarchy or contract requires it. Approved initial edges are:

- `Broiler.UI.Dialog -> Broiler.UI.Window`;
- `Broiler.UI.Tooltip -> Broiler.UI.Window`;
- `Broiler.UI.ToggleButton -> Broiler.UI.Button`;
- `Broiler.UI.ComboBox -> Broiler.UI.ListView` only if the final public contract
  exposes the list abstraction; prefer an item-model contract to avoid this edge;
- `Broiler.UI.ListView -> Broiler.UI.ScrollView` only if scrolling is public
  inheritance/composition rather than an internal implementation detail; and
- implementation assemblies may reference required sibling abstractions, never
  sibling implementation assemblies, except through an approved composition
  package or factory supplied by the application.

### 6.5 Forbidden references and API leakage

No Broiler.UI runtime assembly may reference:

- `Broiler.Graphics.Windows` or a concrete graphics backend;
- any `Broiler.Input.*.Windows` assembly;
- WPF, WinForms, WinUI, Windows App SDK, User32, GDI, Direct2D, UI Automation,
  COM, or Windows Runtime APIs;
- HTML, DOM, CSS, JavaScript, Media, application, or CLI projects;
- native handle types in a public signature;
- `OperatingSystem.IsWindows*` platform branches;
- P/Invoke, `SupportedOSPlatform("windows")`, or Windows target frameworks;
- assembly scanning or module initialization used to locate controls; or
- a concrete sibling control implementation merely to construct it implicitly.

Architecture tests inspect project references, target frameworks, package
references, public signatures, attributes, P/Invoke metadata, and forbidden
namespace usage. These checks are release gates, not style guidance.

### 6.6 Packages and convenience composition

Each runtime assembly publishes independently. A later
`Broiler.UI.Standard.Controls` package may reference the stable implementation
packages for application convenience, but it contains no runtime types and no
implementation code. It must never become a dependency of an abstraction or of
another standard implementation.

The initial repository may keep all UI projects in one `Broiler.UI` component
checkout while preserving one-project/one-assembly/one-package boundaries. A
future repository split must not require public API changes.

## 7. Shared `Broiler.UI` contract

### 7.1 `UiElement`

The root abstract class owns only behavior shared by every element:

- immutable runtime identity for diagnostics;
- optional name and automation identifier;
- parent and session attachment state;
- visibility, enabled state, opacity, clipping, and hit-test participation;
- requested size constraints, margin, alignment, and arranged bounds;
- measure, arrange, render, and invalidation lifecycle hooks;
- focusability and tab-order metadata;
- semantic role/name/value/state hooks;
- routed event participation;
- theme/localization change notification;
- deterministic disposal and attachment teardown; and
- diagnostic state such as last measured size and invalidation reason.

It must not own:

- a native handle or backend object;
- a universal `Text`, `Value`, `Items`, or `Clicked` member;
- an untyped property bag used to simulate control-specific APIs;
- a device provider or global input singleton;
- a static current application/session/theme;
- a public collection of arbitrary children; or
- a control factory that knows every implementation assembly.

Only container types expose child/content contracts. A button may expose one
content model later, but that does not make every `UiElement` a container.

### 7.2 Attachment and lifecycle state machine

Every element follows a documented state machine:

```text
Created -> Attached -> Measured -> Arranged -> Renderable
   |          |           |           |            |
   +----------+-----------+-----------+------------+-> Disposed
              |
              +-> Detached -> Attached to a later compatible session
```

Measurement, arrangement, and rendering repeat while attached. Detachment clears
focus, pointer capture, animation registrations, host capability leases, and
parent-derived theme state. Disposal is idempotent and terminal.

Tree mutations made during input dispatch or layout are queued or applied at a
defined safe point so traversal is not invalidated unpredictably. Events never
observe a half-attached subtree.

### 7.3 UI session

`UiSession` is a concrete orchestration service in the core assembly, not another
control base. It owns:

- the set of logical root windows;
- viewport bindings supplied by the host;
- one UI-thread/dispatcher affinity contract;
- focus scopes and active window;
- pointer capture and hover paths;
- modal and popup stacks;
- input queue ordering;
- animation and delayed-action scheduling;
- dirty layout/render roots;
- theme and localization snapshots; and
- diagnostics, shutdown, and teardown ordering.

The session receives explicitly constructed factories for the standard controls
the application wants. It does not scan assemblies, load packages by name, or use
mutable global defaults.

### 7.4 Host ports

The core declares small platform-neutral ports rather than an all-powerful host
interface. The initial capability set covers:

- graphics viewport: logical size, scale, renderer/surface access or render-list
  submission, and invalidation request;
- dispatcher: access check and queued callback scheduling;
- monotonic clock and animation-frame scheduling;
- top-level window requests: create/show/hide/close/title/state without exposing
  a native handle;
- clipboard: typed text and optional structured-data transactions;
- cursor: semantic cursor request such as arrow, text, resize, or hand;
- text services: composition start/update/commit/cancel and caret rectangle
  publication;
- accessibility: semantic-tree snapshots, change notifications, and action
  dispatch;
- drag-and-drop: neutral data package and allowed effects; and
- system settings: reduced motion, contrast preference, text scale, and double-
  click timing where supplied by the host.

All capabilities except the graphics viewport and dispatcher are optional for an
early host. Controls query the session capability set, not the operating system.
Capabilities have explicit lifetimes and thread-affinity rules.

### 7.5 Factory and implementation selection

Construction is explicit. Each implementation package exposes a factory for its
own abstract type. The application creates an immutable `UiFactorySet` containing
only the selected factories and passes it to the session or higher-level
composition code.

Rules:

- duplicate factories for one type are rejected unless the caller selects one;
- a missing optional factory is a deterministic capability result;
- no implementation self-registers during assembly load;
- factory sets are immutable and scoped per session/application;
- tests can substitute fakes without changing static process state; and
- factory creation must not open devices or native windows.

### 7.6 Diagnostics and errors

Common failures use UI-specific categories such as invalid tree mutation,
attachment conflict, unavailable host capability, layout cycle, renderer fault,
input dispatch fault, disposed object, and factory mismatch.

Diagnostics include element identity/path, logical window, session, active
control type, phase, monotonic timestamp, and inner exception where safe. Text
content, clipboard data, typed keystrokes, and accessibility values are excluded
from default telemetry.

## 8. Layout and rendering model

### 8.1 Retained tree and layout protocol

Broiler.UI uses a two-pass widget layout protocol:

1. Measure: a parent offers available logical size; a child reports its desired
   size subject to minimum, maximum, margin, and content constraints.
2. Arrange: a parent assigns the final logical rectangle; the child records its
   render and hit-test geometry.

Layout uses Broiler.Graphics geometry value types where they are suitably
platform-neutral. It does not reference Broiler.Layout or reuse webpage CSS box
algorithms.

The protocol must define:

- finite/infinite constraint behavior;
- NaN, negative, and overflow rejection;
- desired-size caching and invalidation propagation;
- transforms and clipping;
- rounding at the host pixel boundary;
- right-to-left mirroring rules;
- baseline alignment for text-bearing controls;
- collapsed versus hidden visibility;
- scroll extent versus viewport size; and
- a bounded response to pathological remeasure loops.

### 8.2 Panel policies

`UiPanel` is the only initial general multi-child control. Its abstraction accepts
platform-neutral layout-policy objects. The first standard policies are stack,
dock, wrap, overlay, and a constrained grid. Policies are strategies, not public
`UiElement` types, so they do not require separate control assemblies.

A policy becomes its own control type only if it gains independently
instantiable state, input behavior, semantics, or lifecycle beyond arranging
panel children.

### 8.3 Graphics submission

The standard implementation builds platform-neutral Broiler.Graphics render
commands/lists from the arranged tree. It must:

- never downcast to a Direct2D or Windows renderer;
- use logical units until Graphics performs final device conversion;
- preserve clip and transform stack balance;
- reuse immutable brushes, text styles, images, and geometry where safe;
- make opacity and disabled-state composition deterministic;
- submit no native control or native text widget;
- degrade predictably when a Graphics capability is absent; and
- surface renderer faults without corrupting UI state.

The preferred host boundary submits a complete immutable render list or frame
description. Direct surface access is permitted only if the Graphics API requires
it and the lifetime is scoped to one render callback.

### 8.4 Invalidation and scheduling

Invalidation categories are explicit:

- visual only;
- arrange subtree;
- measure subtree;
- semantic/accessibility only;
- input hit-test path; and
- full viewport/window.

Multiple invalidations coalesce before the next frame. A control cannot render
recursively from an event callback. Animation uses the session clock and frame
scheduler; no control owns an OS timer. Inactive or hidden subtrees stop
requesting frames unless their logical state still requires time progression.

### 8.5 Theme and visual states

Themes are immutable token sets supplied per session or subtree. Initial tokens
cover typography, spacing, corner radius, border widths, colors, focus cues,
selection, disabled state, elevation/shadow hints, and motion duration/easing.

Every interactive standard control defines a finite visual-state model such as
normal, hover, pressed, focused, disabled, checked, selected, or invalid.
Control state drives theme token lookup; themes do not reach into private fields
or replace behavior.

The first standard theme should be visually appropriate on Windows while using no
Windows APIs. A later platform may supply different tokens while reusing the same
control implementations.

## 9. Input, focus, and command model

### 9.1 Input boundary

Broiler.UI consumes platform-neutral observations from the Keyboard, Mouse,
Touch, and Pen Input abstractions. The UI adapter translates them into a small
routed-event vocabulary while retaining device identity, source timestamp,
modifiers, contact identity, and handled/canceled state.

The event flow is:

```text
Broiler.Input device/provider
  -> application/session binding
  -> viewport coordinate normalization
  -> hit test or capture target
  -> preview route from window to target
  -> target handler
  -> bubble route from target to window
  -> default control action if not canceled
```

The UI layer does not enumerate devices, open cameras/microphones, or interpret
Windows key codes. Gamepad navigation is deferred until a separate navigation
contract is approved.

### 9.2 Pointer routing and capture

The session maintains one hover path per pointer and capture per active pointer.
It defines enter/leave, move, press, release, wheel, cancel, and capture-lost
semantics. Press/release transitions are never coalesced. Movement may coalesce
within a frame while preserving the latest position and timestamps needed for
gesture calculations.

Controls use an interaction state machine rather than inferring a click from any
release. A button activates only when press, capture, release, enabled state, and
cancelation rules all succeed.

### 9.3 Keyboard, text, and composition

Physical/logical key transitions and text composition are separate streams:

- keys drive shortcuts, navigation, activation, selection extension, and editing
  commands;
- committed text inserts characters;
- composition updates display the in-progress text and selection; and
- composition commit/cancel completes the transaction.

`UiEdit` must never create text by converting key codes itself. Dead keys, input
methods, surrogate pairs, combining marks, and emoji sequences arrive through
text/composition services.

### 9.4 Focus

Each logical window is a focus scope. The session owns:

- active window and focused element;
- focus request, preview-losing, losing, preview-gaining, and gained transitions;
- tab and reverse-tab traversal;
- explicit tab index and natural tree order;
- focus restoration when a modal/subwindow closes;
- focus removal on hide, disable, detach, or dispose; and
- visible keyboard focus cues independent of pointer focus.

Focus changes are transactional. Reentrant focus requests queue until the current
transition completes, and failed/canceled transitions leave one well-defined
focused element.

### 9.5 Commands and access keys

Controls expose semantic commands rather than binding directly to application
delegates hidden in implementation state. A command reports whether it can
execute, executes with a neutral parameter, and notifies state changes.

Buttons, menus, and keyboard shortcuts invoke commands through the session.
Labels can associate an access key with another element. Default and cancel
buttons are window-scoped. Command execution happens after routed input handlers
have had a chance to cancel the default action.

## 10. Window and subwindow management

### 10.1 Logical window model

`UiWindow` owns platform-neutral window behavior:

- title and application-visible identity;
- content root;
- owner/owned-window relationship;
- requested visibility and window state;
- activation and focus scope;
- logical bounds and minimum/maximum size;
- z-order group;
- close-request, cancelation, closed, and result lifecycle;
- resizable/movable capability intent;
- modal state; and
- host binding status.

It does not own a native handle, message pump, monitor object, Direct2D surface,
or Windows style flags.

### 10.2 Top-level hosting

For each native top-level window, the application host creates the Graphics
window/surface and Input bindings, then binds one UI viewport. `UiSession` renders
the logical root window into that viewport and routes normalized input back to
it.

Host requests are asynchronous where the native operation may be deferred. A
logical close request may be canceled by application code. Host destruction is
authoritative and triggers deterministic UI detachment even when no prior close
event was delivered.

### 10.3 Managed subwindows

By default, dialogs, combo popups, menus, tooltips, and other subwindows remain in
the parent viewport. The window manager owns:

- stacking bands for normal content, popups, modal content, tooltips, and drag
  adorners;
- clipping policy and placement against viewport bounds;
- light-dismiss behavior;
- pointer capture across the subwindow stack;
- modal input blocking;
- activation/focus restoration;
- owner hide/close propagation; and
- deterministic teardown from topmost child to owner.

This gives consistent behavior without native child windows. A host may request a
separate top-level viewport for a logical window when the application needs a
real second OS window.

### 10.4 Dialogs, menus, and tooltips

`UiDialog` extends `UiWindow` with an asynchronous result and explicit modal or
modeless presentation. Nested modal loops are forbidden; the owning application
message loop continues while the result task completes.

`UiMenu` uses data item descriptors and internal item containers. Menu items are
not public control types in the first release. If applications later need to
instantiate or subclass menu items directly, `Broiler.UI.MenuItem` and
`Broiler.UI.MenuItem.Standard` must be introduced rather than adding a second
control base to the Menu assembly.

`UiTooltip` is non-activating, non-focusable, delayed, and dismissed by pointer,
keyboard, owner-state, or timeout rules. Timing comes from the session clock.

### 10.5 Native window feature mapping

Requested title, state, size constraints, close, show, hide, and activation flow
through host ports. Unsupported requests return capability results. UI does not
infer Windows support or inspect runtime OS versions.

Monitor selection, taskbar behavior, system menus, native decorations, snap,
per-monitor DPI notifications, and shell integration remain host concerns. The
host projects resulting viewport size/scale/state changes back into neutral UI
events.

## 11. Control-specific contract requirements

### 11.1 Window

- Exactly one content root per window.
- Explicit owner and no ownership cycles.
- Cancelable close followed by one terminal closed notification.
- Window activation independent of keyboard focus.
- Modal state enforced by `UiSession`, not by a blocking loop.
- Subwindow placement and z-order deterministic under resize and DPI changes.

### 11.2 Panel

- Ordered child collection with transactional mutation.
- Policy-driven measure/arrange and no control-specific rendering assumptions.
- Optional background, border, clip, and padding.
- Child z-index stable for equal values by insertion order.
- Large-child-count operations avoid quadratic invalidation.

### 11.3 Label

- Plain text baseline; rich text is deferred.
- Wrapping, trimming, alignment, mnemonic/access-key marker, and target labeling.
- Never focusable by default.
- Semantic association exposes the labeled element rather than duplicating an
  interactive role.

### 11.4 Button and ToggleButton

- Activation by primary pointer, Enter/Space according to role, command, and
  programmatic invocation.
- One activation per valid gesture; no duplicate click after key repeat unless
  explicitly configured.
- Pressed state tied to capture and canceled on disable, detach, or input cancel.
- Toggle state changes transactionally and may be canceled before command
  execution if the contract permits.

### 11.5 Edit

`UiEdit` is the highest-risk foundational control. Its stable contract covers:

- single-line and multiline modes;
- Unicode text storage without splitting surrogate pairs or grapheme clusters;
- caret movement by grapheme, word, line, document, and visual direction;
- anchor/active selection and selection change events;
- insertion, replacement, deletion, overwrite policy, and maximum length;
- composition range and attributes;
- undo/redo transaction grouping with bounded history;
- copy, cut, paste, select-all, and neutral clipboard capability use;
- horizontal/vertical scrolling and caret visibility;
- password presentation without exposing text through default semantics or
  diagnostics;
- read-only versus disabled behavior;
- validation state separate from text storage;
- submission policy; and
- accessible value, selection, caret, and editable/read-only state.

The implementation must not claim parity until Windows IME composition, screen
reader interaction, clipboard, high-DPI caret placement, bidirectional text, and
large-text behavior pass the required gates. The existing native `BEditControl`
remains available during that period.

### 11.6 Selection controls

CheckBox, RadioButton, Slider, ListView, ComboBox, and TabView share neutral
selection concepts but not a public inheritance base.

- CheckBox supports false/true and optional indeterminate state.
- RadioButton grouping is explicit and scoped; a string name alone does not create
  process-wide groups.
- Slider validates range, step, orientation, direction, and value coercion.
- ListView separates item model, generated item container, selection model, and
  viewport virtualization.
- ComboBox owns a managed popup and restores focus/selection on commit or cancel.
- TabView keeps inactive content lifetime policy explicit rather than silently
  disposing it.

### 11.7 ScrollView and ListView virtualization

ScrollView owns offset, extent, viewport, clipping, wheel/touch manipulation,
keyboard commands, and optional overlay/reserved scrollbars. Offset changes are
clamped and report old/new values.

ListView virtualization is part of the standard implementation, not the abstract
item model. It must preserve stable item identity, selection, focus, and semantic
positions while recycling internal containers. The first release supports a
linear vertical layout; grid/tree virtualization is deferred.

### 11.8 Menu

- Menu bar and context-menu presentation modes.
- Nested submenu ownership with bounded depth.
- Pointer intent delay driven by the session clock.
- Arrow, Enter, Escape, access key, and type-to-select behavior.
- Commands and checked/radio menu state in data descriptors.
- Light-dismiss and focus restoration through the subwindow manager.
- No dependence on an OS menu API.

### 11.9 ImageView and ProgressBar

ImageView accepts an already decoded/created Graphics image resource and display
policy. It does not fetch URIs or decode files. Stretch, fit, alignment, clipping,
opacity, and accessible description are explicit.

ProgressBar supports determinate range/value and indeterminate animation.
Reduced-motion settings replace motion with a non-animated state. It does not
report progress or own a background task.

## 12. Accessibility, localization, and text services

### 12.1 Semantic tree

Every standard control exposes platform-neutral semantics:

- role;
- accessible name and description;
- value and value bounds where appropriate;
- enabled, focused, selected, checked, expanded, read-only, required, invalid,
  modal, and offscreen states;
- labeled-by, described-by, owner, and controlled-element relationships;
- supported actions; and
- text selection/caret information for Edit.

The semantic tree is derived from the UI tree but may flatten internal parts and
virtualize large item collections. It contains no UI Automation identifiers or
COM objects. The Windows application host maps it to UI Automation.

### 12.2 Accessibility action routing

Host-initiated semantic actions return to the owning UI dispatcher and invoke the
same control commands as keyboard or pointer interaction. They must not call
control code from an arbitrary native accessibility thread.

Semantic changes are batched with stable element identifiers. Removing an element
invalidates outstanding semantic references deterministically.

### 12.3 Localization and bidirectionality

The session supplies an immutable culture/direction snapshot. Controls support:

- localized text supplied by the application;
- logical leading/trailing alignment;
- right-to-left layout mirroring where appropriate;
- culture-aware number presentation supplied by the application or neutral
  formatter service;
- access-key conflict diagnostics; and
- runtime culture/direction change invalidation.

Broiler.UI does not own resource-file discovery or application translation
policy.

### 12.4 Text and IME boundary

The host publishes caret/selection geometry from UiEdit to the platform text
service and returns neutral composition transactions through Input/text-service
ports. Windows TSF/IMM details remain outside UI.

The fallback matrix is explicit:

| Capability | Standard Edit behavior |
|---|---|
| Committed text only | Basic insertion works; no composition support claimed |
| Composition stream | Full in-progress composition display and commit/cancel |
| Clipboard absent | Copy/cut/paste commands are unavailable; editing remains functional |
| Accessibility bridge absent | Semantic tree remains testable; no OS accessibility support claimed |
| Caret rectangle publication absent | Edit works visually; native candidate-window placement is unsupported |

## 13. Threading, ordering, ownership, and teardown

### 13.1 UI context

One `UiSession` has one owning dispatcher/context. Tree mutation, control state,
layout, input dispatch, and render-list construction happen on that context.
Background work may produce immutable data and post it to the session; it may not
mutate controls directly.

### 13.2 Input ordering

The session preserves source order per Input device/session. Events from different
devices are merged by arrival without inventing a stronger global order. Each
event retains its monotonic source timestamp. Capture-lost, key-up, contact-end,
composition-commit/cancel, and close events are never discarded during
coalescing.

### 13.3 Render ownership

Render lists and resources follow the ownership rules of Broiler.Graphics. A UI
frame does not retain a borrowed surface after submission. Images, fonts, brushes,
and other reusable resources have explicit shared/owned lifetime semantics.

### 13.4 Teardown order

Session shutdown follows this order:

1. Stop accepting new host input and window requests.
2. Cancel delayed actions, animations, and pending composition.
3. Release pointer capture and clear focus.
4. Close/detach managed subwindows from topmost to bottommost.
5. Detach root windows and semantic bridges.
6. Release UI-owned Graphics resources.
7. Unbind host viewports and Input subscriptions.
8. Dispose elements and the session.

Host destruction may begin this path at any point. Every cleanup action is
idempotent, and callbacks arriving after unbind are ignored safely.

## 14. Windows-first composition

### 14.1 Composition boundary

The first Windows application assembles the system as follows:

```text
Broiler.Graphics.Windows (native window + renderer + surface)
              |
              +---- application-owned neutral graphics viewport adapter ----+
                                                                            |
Broiler.Input.*.Windows (keyboard/mouse/text/touch/pen providers)            |
              |                                                             v
              +---- application/session bindings --------------------> UiSession
                                                                            |
Selected Broiler.UI.<Type>.Standard factories ------------------------------+
```

The adapter may live in a Windows application or a narrowly scoped application
integration assembly. It is not part of the Broiler.UI component family and may
reference the Windows implementation assemblies.

### 14.2 First Windows support statement

Windows support means:

- the Windows host can display a logical `UiWindow` through Broiler.Graphics;
- keyboard, mouse, committed text, and later composition arrive through
  Broiler.Input;
- standard controls render and interact without native child controls;
- application chrome can migrate from the existing Graphics controls; and
- optional host bridges expose clipboard, cursor, UI Automation, and native
  top-level operations as they reach their phase gates.

It does not mean Broiler.UI targets `net10.0-windows`, links User32, or contains a
Windows-specific control skin.

### 14.3 Native-control compatibility window

During migration, the Windows browser app may host existing `BButtonControl` and
`BEditControl` beside the Broiler-rendered viewport. Compatibility rules are:

- no new control types are added to Graphics;
- no new public consumer is encouraged to adopt the old contracts;
- the old path receives only critical fixes needed for migration;
- Button can move first after pointer/keyboard/accessibility basics pass;
- Edit moves only after the high-risk gates in section 11.5 pass; and
- Graphics control types are removed or made obsolete only after all aggregate
  consumers and nested checkouts have migrated.

## 15. Testing strategy

### 15.1 Architecture tests

For every runtime assembly, verify:

- exact project-reference allowlist;
- platform-neutral target framework;
- no `*.Windows`, WPF, WinForms, WinUI, Direct2D, COM, or P/Invoke dependency;
- no `IntPtr`, safe handle, or native structure in public UI signatures;
- exactly one primary abstract control class in each type abstraction assembly;
- exactly one corresponding primary concrete class in each `.Standard`
  implementation assembly;
- the concrete class derives from the correct abstract base;
- no implementation assembly contains a sibling concrete control;
- no mutable global service/factory registry; and
- no Graphics-owned input event type in the stable UI public API.

### 15.2 Root contract tests

- parent uniqueness and cycle rejection;
- attach/detach/dispose state transitions;
- queued mutation during traversal;
- measure/arrange cache invalidation;
- focus cleanup on hide, disable, detach, and dispose;
- pointer capture and capture-lost behavior;
- routed-event order, handled/canceled behavior, and reentrancy;
- session isolation with two independent sessions in one process;
- deterministic delayed action and animation under a fake clock; and
- teardown after host loss at every lifecycle stage.

### 15.3 Control contract suites

Each abstraction publishes or internally shares a reusable contract suite run
against its Standard implementation and test fakes. Suites cover public state,
events, layout, rendering invariants, input, semantics, errors, and disposal.

Type-specific emphasis includes:

- Button: press/capture/release, keyboard activation, command availability;
- Edit: Unicode/graphemes, selection, composition, undo, clipboard, caret, bidi,
  large text, and password privacy;
- Window/Dialog: owner cycles, close cancelation, modality, focus restoration;
- ScrollView: extent/viewport changes, clamping, wheel/touch cancellation;
- ListView: virtualization, selection identity, recycling, semantics;
- Menu/ComboBox/Tooltip: popup placement, dismissal, nested ownership, timers;
  and
- Slider/ProgressBar: range validation, coercion, direction, reduced motion.

### 15.4 Rendering and visual tests

- deterministic render-list snapshots for every state;
- pixel tests through the Broiler raster renderer;
- selected Windows Direct2D parity captures;
- DPI scales including fractional scale;
- clipping, transforms, opacity, text baseline, and focus cue tests;
- light/dark/high-contrast token sets;
- right-to-left and long-localized-text layouts;
- reduced-motion behavior; and
- no reliance on host font or timer defaults without an explicit test profile.

Golden images include tolerance metadata and the graphics backend identity.
Behavioral assertions remain primary so harmless raster differences do not hide
state bugs.

### 15.5 Input replay and fuzzing

- recorded keyboard/pointer/composition traces replayed deterministically;
- randomized press/move/release/cancel/capture-lost sequences;
- randomized focus and tree mutation during event dispatch;
- malformed coordinates, extreme wheel values, repeated timestamps, and device
  removal;
- layout fuzzing with extreme constraints, transforms, nesting, and text;
- subwindow open/close races and host destruction; and
- long-running hover/animation/input soak tests with allocation tracking.

### 15.6 Windows integration tests

Windows-only application/host tests verify:

- Graphics window creation and UI viewport binding;
- real keyboard/mouse/text delivery through Input Windows providers;
- DPI and resize propagation;
- clipboard and cursor bridges;
- UI Automation tree/action mapping;
- IME candidate placement and composition with representative IMEs;
- native close/activation/state changes projected into logical windows;
- graphics device/resource recreation; and
- no native callbacks after session/window disposal.

These tests belong to the Windows host/integration project, not a UI runtime
assembly.

### 15.7 Performance gates

Establish baselines for:

- first window/control tree layout and first frame;
- steady-state frame construction with no changes;
- button hover/press invalidation;
- Edit insertion, selection, undo, and caret movement at representative sizes;
- scrolling and ListView virtualization at 1,000, 10,000, and 100,000 items;
- menu/subwindow open latency;
- semantic snapshot/update cost;
- allocations per idle frame and per input event; and
- session teardown with large trees.

No-op frames should allocate near zero after warm-up. Work should scale with the
dirty subtree or visible item count, not the total application model size.

## 16. Delivery roadmap

### Phase 0 - Decisions, inventory, and baselines

**Objective:** Freeze ownership and compatibility decisions before scaffolding.

Tasks:

- Approve the logical-window versus native-host split.
- Approve `UiElement`, per-type abstract bases, and `.Standard` implementation
  naming.
- Approve the initial control matrix and identify which types are required for
  the first stable release versus later releases.
- Inventory every `BWindow`, `BControl`, input callback, timer, and native-control
  consumer across all Graphics checkouts and applications.
- Record browser chrome behavior, screenshots, keyboard routes, accessibility,
  DPI, resize, and resource-lifetime baselines.
- Align the dependency boundary with the Broiler.Input roadmap.
- Decide package/repository versioning and compatibility duration.
- Write the ADRs listed in section 20.

Exit gate:

- no unresolved owner exists for window hosting, input translation, focus,
  accessibility, clipboard, or rendering;
- initial assembly names and allowed reference graph are approved;
- baseline artifacts and migration consumers are documented; and
- no implementation API must be guessed in Phase 1.

### Phase 1 - Core and architecture guardrails

**Objective:** Establish the platform-neutral root without controls.

Tasks:

- Scaffold `Broiler.UI`, `Broiler.UI.Standard`, and matching test projects.
- Target the repository `net10.0` baseline with nullable and warning policies.
- Define `UiElement`, attachment/lifecycle, geometry/layout, invalidation, routed
  event, semantic, dispatcher, clock, host-port, session, and factory contracts.
- Implement deterministic fake host, fake clock, recording renderer/input route,
  and architecture inspection tests in test projects.
- Add projects to the aggregate solution without migrating consumers.
- Prove that all runtime projects build on a non-Windows target host.

Exit gate:

- root/session tests pass;
- forbidden-reference tests fail against deliberate fixtures and pass against the
  runtime assemblies;
- a fake element can attach, layout, render a recording, receive input, and
  dispose deterministically; and
- no Windows target or native handle exists in UI.

### Phase 2 - Standard rendering, input, focus, and theme services

**Objective:** Build reusable standard infrastructure before type implementations.

Tasks:

- Implement tree traversal, dirty-root scheduling, render-list construction,
  routed input, hit testing, pointer capture, focus scopes, command dispatch,
  theme resolution, semantics, and animation scheduling in `Broiler.UI.Standard`.
- Add application/session bindings for platform-neutral Keyboard, Mouse, Touch,
  Pen, text, and composition contracts.
- Add a temporary adapter from current Graphics callbacks solely for migration
  tests if the Input extraction has not landed.
- Validate multiple sessions and multiple logical windows without static state.

Exit gate:

- a synthetic element tree responds to replayed pointer/keyboard input and emits
  deterministic render lists;
- focus, capture, routing, timing, and invalidation pass reentrancy tests;
- the legacy adapter is isolated and marked for removal; and
- no type-specific concrete control lives in `Broiler.UI.Standard`.

### Phase 3 - Window, Panel, and Label foundation

**Objective:** Render and manage the first real logical UI tree.

Tasks:

- Add Window, Panel, and Label abstraction/implementation assembly pairs.
- Implement logical window lifecycle, viewport binding, z-order, ownership,
  close, activation, and managed subwindow primitives.
- Implement Panel child collection and initial stack/dock/overlay policies.
- Implement Label text measurement, wrapping, trimming, access-key association,
  semantics, and RTL behavior.
- Compose the tree into a Windows Graphics viewport through an application-owned
  adapter.

Exit gate:

- one Windows native host displays a standard logical window containing nested
  panels and labels;
- resize/DPI changes relayout and redraw correctly;
- a managed child window can open, stack, and close without an HWND; and
- UI assemblies remain platform-neutral under architecture inspection.

### Phase 4 - Button and Edit vertical slice

**Objective:** Make Broiler.UI usable for browser application chrome.

Tasks:

- Add Button and Edit abstraction/implementation pairs.
- Complete Button pointer, keyboard, command, default/cancel, focus, theme, and
  semantic behavior.
- Build Edit in increments: text model, caret/selection, rendering, committed
  text, keyboard commands, scrolling, clipboard, undo, composition, bidi,
  accessibility, and password privacy.
- Build a parallel browser-toolbar proof using Window, Panel, Label, Button, and
  Edit without removing the current controls.
- Compare behavior, visuals, keyboard navigation, DPI, and resource usage against
  the baseline.

Exit gate:

- the proof toolbar can navigate by mouse and keyboard;
- Edit passes committed-text, selection, clipboard, submission, and minimum
  accessibility gates;
- no native child control is used in the proof path; and
- the production browser remains able to use the old path until Phase 8.

### Phase 5 - Basic state and value controls

**Objective:** Complete common form and status widgets.

Tasks:

- Add CheckBox, RadioButton, ToggleButton, Slider, ProgressBar, and ImageView
  abstraction/implementation pairs.
- Finalize group, range, step, coercion, indeterminate, reduced-motion, and image
  ownership contracts.
- Add high-contrast, RTL, keyboard, touch-target, and semantic tests.
- Ensure implementation packages do not pull unrelated controls.

Exit gate:

- each control passes its reusable contract suite;
- range/selection state remains deterministic under reentrancy;
- ImageView accepts only already available Graphics resources; and
- dependency graphs remain per-control.

### Phase 6 - Scrolling, collections, and popups

**Objective:** Add scalable containers and transient window behavior.

Tasks:

- Add ScrollView, ListView, ComboBox, TabView, Menu, and Tooltip pairs.
- Implement scrolling, clipping, manipulation, item identity, selection model,
  linear virtualization, popup placement, light-dismiss, menu navigation, and
  tooltip timing.
- Exercise 100,000-item ListView scenarios and large semantic collections.
- Verify managed popups remain within the logical window manager and need no
  native child window.

Exit gate:

- scrolling and virtualization meet performance gates;
- focus/capture restore correctly after every popup dismissal path;
- nested menus are bounded and keyboard-complete; and
- popups relayout correctly after viewport/DPI changes.

### Phase 7 - Dialogs and host-service parity

**Objective:** Complete subwindows and the Windows quality bridges needed to
replace native controls.

Tasks:

- Add Dialog abstraction/implementation pairs.
- Complete modal/modeless result lifecycle without nested message loops.
- Harden text composition and caret geometry with Windows IMEs.
- Complete Windows clipboard, cursor, drag/drop if in stable scope, and UI
  Automation bridges in the application host.
- Validate screen readers, keyboard-only use, high contrast, text scaling,
  reduced motion, and RTL scenarios.
- Decide whether native top-level mapping for secondary logical windows is ready
  or remains experimental.

Exit gate:

- standard Edit meets the replacement gates in section 11.5;
- Dialog and subwindow teardown is leak-free under host destruction;
- Windows accessibility and IME support statements are evidence-based; and
- no OS code was added to a UI runtime assembly.

### Phase 8 - Application migration and Graphics cleanup

**Objective:** Make Broiler.UI the owner of application chrome and controls.

Tasks:

- Migrate `Broiler.App.Graphics` navigation/favorites chrome to standard UI
  controls in small, reversible steps.
- Preserve browser surface input routing separately from chrome routing.
- Remove application dependencies on `BButtonControl`, `BEditControl`,
  `BControlOptions`, and Graphics input callbacks.
- Move or retire `BControl`, `BButtonControl`, `BEditControl`, and control-creation
  methods from Graphics after every consumer and nested checkout has migrated.
- Narrow `BWindow`/`Direct2DWindow` to Graphics hosting/presentation or replace
  their mixed responsibilities with approved host interfaces.
- Remove the temporary legacy input adapter after Broiler.Input cutover.
- Update Graphics, Input, application, public-preview, and architecture docs.

Exit gate:

- application chrome uses no Graphics-owned control abstraction;
- Graphics owns no UI control type and no authoritative input translation path;
- the application builds and runs with explicit UI/Graphics/Input composition;
- old APIs follow the approved obsolete/removal policy; and
- aggregate plus standalone component revisions are coherent.

### Phase 9 - Stabilization, packaging, and cross-platform readiness

**Objective:** Freeze the first stable contract after real Windows use.

Tasks:

- Run performance, leak, fuzz, accessibility, localization, DPI, IME, and long-
  duration soak suites.
- Freeze public names and XML documentation after consumer review.
- Publish each abstraction and implementation assembly as an independent package.
- Optionally publish the package-only standard-controls convenience bundle.
- Add component README, control support matrix, host integration guide, theming
  guide, accessibility statement, and migration notes.
- Build all UI runtime packages on Windows and at least one non-Windows CI host.
- Create a minimal fake/non-Windows host proof to demonstrate that no Windows
  assumption escaped into the contracts.

Exit gate:

- the definition of done in section 21 is satisfied;
- package graphs match the architecture allowlist;
- all supported Windows behaviors have explicit test evidence; and
- a later platform can implement host adapters without changing public control
  contracts.

## 17. Suggested pull-request sequence

1. ADRs, current-state inventory, compatibility policy, and baselines.
2. `Broiler.UI` core contracts plus architecture tests.
3. `Broiler.UI.Standard` retained tree, layout, render, and fake-host foundation.
4. Platform-neutral Input binding and temporary legacy callback adapter.
5. Window abstraction and Standard implementation.
6. Panel abstraction and Standard implementation.
7. Label abstraction and Standard implementation.
8. Button abstraction and Standard implementation.
9. Edit text model and rendering without production cutover.
10. Edit clipboard, composition, accessibility, and replacement gates.
11. Browser-toolbar proof and visual/input parity results.
12. CheckBox, RadioButton, ToggleButton, Slider, and ProgressBar as separate PRs.
13. ImageView as a separate PR.
14. ScrollView foundation.
15. ListView selection and virtualization.
16. ComboBox, TabView, Menu, and Tooltip as separate PRs.
17. Dialog and host-service parity.
18. Browser chrome migration by control region.
19. Graphics control deprecation/removal and Input callback cleanup.
20. Packaging, documentation, and stable release gates.

Every PR leaves the aggregate solution buildable. A new type pair is not combined
with unrelated control work merely to reduce PR count. Cross-repository changes
land provider first, consumer second, cleanup third, and parent revision updates
last.

## 18. Compatibility and versioning policy

The recommended migration policy is:

1. Add new UI contracts without changing existing Graphics public types.
2. Ship adapters and migrate internal applications.
3. Mark old Graphics control creation and input callbacks obsolete after the new
   path passes production gates.
4. Maintain one documented compatibility window.
5. Remove old APIs only at the next approved breaking release.

Compatibility wrappers may translate construction options, clicked/submitted
events, bounds, text, visible/enabled state, and focus requests. They must not
expose native handles through new UI types or promise exact native visual
behavior.

Public UI abstraction packages follow semantic versioning independently from
Standard implementations. A Standard implementation version declares the
supported abstraction version range. Package tests load each supported pairing
and reject ambiguous duplicate factory selection.

## 19. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| One assembly per type creates package sprawl | Higher project and release overhead | Generate project metadata in implementation phase, use shared build props, publish an optional package-only convenience bundle |
| Shared Standard assembly becomes a hidden control monolith | Boundary erosion and unwanted dependencies | Permit infrastructure only; architecture test bans primary concrete controls from it |
| Logical window is confused with native HWND | Windows assumptions leak into API | Name/document host mapping explicitly; ban native handles and Windows references |
| Graphics remains owner of controls by convenience | Duplicate UI systems | Freeze new Graphics control work, prove UI vertical slice, then time-box compatibility |
| Edit quality lags native controls | IME/accessibility regression | Keep native edit compatibility until explicit replacement gates pass |
| Input roadmap lands later than UI work | Blocked interaction testing | Use one isolated legacy adapter with the same internal route; remove after Input cutover |
| Layout grows into a second web layout engine | Scope and maintenance explosion | Keep a small widget measure/arrange protocol; do not reference CSS or Broiler.Layout |
| Standard implementations reference one another | Recreates monolithic dependency graph | Depend on abstractions/factories; architecture-test sibling implementation references |
| Routed events become reentrant and unpredictable | Focus/capture/tree corruption | Transactional transitions, safe-point mutations, deterministic queueing, fuzz tests |
| Managed Edit leaks passwords or typed text to telemetry | Privacy issue | Redacted diagnostics, semantic restrictions, bounded undo, no default content logging |
| Virtualized controls break accessibility | Incorrect counts/focus/actions | Stable item identity, virtual semantic nodes, dedicated large-list accessibility tests |
| Subwindows escape viewport or trap focus | Unusable menus/dialogs | Central placement/modal/focus manager and exhaustive close/dismiss tests |
| Per-frame allocation causes UI stalls | Poor responsiveness | Dirty-subtree scheduling, immutable resource caches, allocation performance gates |
| Device loss breaks rendering state | Blank or corrupted UI | Rebuildable render resources and host resource-recreation tests |
| Multiple component checkouts drift | Broken builds and stale APIs | Canonical checkout/package policy and provider-consumer-parent update order |
| Windows-first theme is mistaken for Windows coupling | Future platform friction | Theme tokens only, no OS API or platform branch, non-Windows build/proof host |

## 20. Required ADRs before API stabilization

1. **UI root and per-type assembly rule** - approve `UiElement`, one abstract base
   per type, one Standard implementation per implementation assembly, and the
   exception process for future types.
2. **Logical versus native windows** - define ownership, host mapping, close,
   activation, state, DPI, secondary top-level windows, and destruction.
3. **Graphics submission boundary** - choose render-list submission versus scoped
   surface access and define resource ownership/device-loss behavior.
4. **Input and text-service boundary** - align normalized pointer/key/text/
   composition contracts with Broiler.Input and the legacy migration adapter.
5. **UI context and reentrancy** - define dispatcher affinity, safe-point tree
   mutations, focus/capture transitions, and event ordering.
6. **Layout protocol** - freeze measure/arrange, visibility, rounding, transforms,
   RTL, invalidation, and cycle handling.
7. **Implementation factories** - define explicit immutable factory selection and
   reject global registration/module initialization.
8. **Accessibility semantic bridge** - define identifiers, snapshots, virtualized
   children, action dispatch, privacy, and Windows UIA mapping ownership.
9. **Edit text model** - choose text storage/indices, grapheme and bidi behavior,
   composition transactions, undo bounds, password privacy, and clipboard ports.
10. **Theme and visual state model** - define token inheritance, overrides,
    animation, high contrast, reduced motion, and versioning.
11. **Compatibility removal** - set obsolete/removal releases for Graphics
    controls, `BWindow` UI/input responsibilities, and duplicate checkout updates.
12. **Package/repository topology** - choose one component repository with many
    assemblies versus later repository splits and define release automation.

## 21. Definition of done for the first stable release

Broiler.UI is complete for this roadmap when:

- `UiElement` is the sole abstract root in `Broiler.UI`;
- every supported independently instantiable type has one abstract base in its
  own abstraction assembly and one Standard concrete implementation in its own
  implementation assembly;
- the stable control set includes at least Window, Panel, Label, Button, Edit,
  CheckBox, RadioButton, ToggleButton, Slider, ProgressBar, ScrollView, ListView,
  ComboBox, TabView, Menu, ImageView, Dialog, and Tooltip, or any deferred type is
  removed from the stable support claim explicitly;
- a `UiSession` manages logical top-level windows and managed subwindows, focus,
  capture, modality, layout, rendering, input routing, scheduling, and teardown;
- all standard controls draw only through platform-neutral Broiler.Graphics;
- all device input reaches UI only through platform-neutral Broiler.Input after
  removal of the temporary adapter;
- no UI runtime assembly targets Windows, calls native APIs, exposes native
  handles, or references a `*.Windows` assembly;
- the Windows browser application uses Broiler.UI for its application chrome;
- Graphics no longer owns public UI control types or the authoritative input
  translation path;
- Standard Edit passes the declared Unicode, selection, undo, clipboard, IME,
  accessibility, password, DPI, bidi, and performance gates before native edit
  removal;
- the Windows host supplies tested graphics, input, clipboard, cursor,
  accessibility, composition, and native-window bridges without moving that code
  into UI;
- architecture, contract, rendering, input replay, accessibility, fuzz,
  performance, leak, and Windows integration suites pass;
- every assembly/package can be consumed according to the documented dependency
  graph without the aggregate repository; and
- a non-Windows build and proof host demonstrate that the public contracts are
  genuinely platform-neutral.

## 22. Recommended decisions to approve before implementation

1. Approve `UiElement` as the single root abstract class.
2. Approve the `Broiler.UI.<Type>` and `Broiler.UI.<Type>.Standard` naming rule.
3. Approve logical UI windows with external native-host mapping and no HWND in UI.
4. Approve the initial control matrix in section 6.2 and identify any type to
   defer beyond the first stable release.
5. Approve Broiler-drawn Standard controls rather than native child controls.
6. Approve a time-boxed native Edit compatibility path until parity gates pass.
7. Approve explicit factory sets and reject global implementation registration.
8. Approve the widget-specific measure/arrange protocol without a Broiler.Layout
   dependency.
9. Approve the host-port model for graphics, dispatch, clipboard, cursor, text
   services, accessibility, drag/drop, and native window requests.
10. Approve application-owned Windows composition with no `Broiler.UI.Windows`
    runtime assembly.
11. Approve provider-first migration from Graphics controls and Input callbacks.
12. Approve architecture tests as release gates for assembly ownership and
    operating-system neutrality.

These decisions make implementation incremental: establish the root and guardrails,
prove logical windows and basic controls, harden Edit, expand the control library,
migrate the Windows browser application, and only then remove the mixed UI/input
responsibilities from Graphics.

