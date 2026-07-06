# Broiler.UI Phase 2 Implementation Record

**Status:** Implemented  
**Date:** 2026-07-02

Phase 2 adds reusable Standard infrastructure for retained UI behavior without
adding any type-specific concrete controls.

## Added Input Bindings

Broiler.UI now consumes platform-neutral Broiler.Input contracts for keyboard,
mouse, touch, pen, text, and text composition. The neutral input component gained
the following contract projects so UI routing can be tested before platform
providers are complete:

| Project | Purpose |
|---|---|
| `Broiler.Input.Touch` | Touch contact state and contact events |
| `Broiler.Input.Pen` | Pen contact state, button state, and contact events |
| `Broiler.Input.Text` | Committed text and composition transaction events |

`UiInputEvent` adapts all of these records into the UI event pipeline. Keyboard,
committed text, and composition events route to the focused element first; pointer
events route through capture or hit testing.

## Standard Infrastructure

| Service | Purpose |
|---|---|
| `StandardTreeTraversal` | Deterministic pre-order, post-order, and ancestor traversal |
| `StandardDirtyRootScheduler` | Dirty-root collection from session invalidations |
| `StandardRenderTraversal` | Deterministic render-list construction from a session |
| `StandardInputRoute` | Routed input dispatch for mouse, keyboard, touch, pen, text, and composition |
| `StandardHitTestService` | Session-scoped hit testing |
| `StandardFocusScope` | Session-scoped focus management |
| `StandardCommandDispatcher` | Explicit command dispatch without global registration |
| `StandardThemeResolver` | Per-element theme overrides with inherited defaults |
| `StandardSemanticSnapshot` | Stable semantic snapshots from the retained tree |
| `StandardAnimationScheduler` | Clock-driven animation callbacks without static state |
| `StandardLegacyGraphicsInputAdapter` | Temporary Graphics callback migration adapter marked obsolete |

`Broiler.UI.Standard` remains infrastructure-only. It contains no public
type-specific concrete controls such as a button, edit, label, panel, or window
implementation.

## Session Behavior

`UiSession` now preserves invalidations raised during rendering for the next
frame, so reentrant invalidation does not disappear when a render pass completes.
Input target resolution remains session-local: pointer capture, focus, hit
testing, dirty roots, theme overrides, command handlers, and animation callbacks
are all instance state.

The temporary legacy adapter converts current `Broiler.Graphics` callback
arguments into the same neutral UI event path used by Broiler.Input. It is
isolated in `Broiler.UI.Standard` and marked for removal after the Input cutover.

## Exit Gate Evidence

The Phase 2 tests prove:

- a synthetic retained tree responds to replayed pointer, keyboard, touch, pen,
  text, and composition input;
- render-list output is deterministic across repeated traversals;
- focus, capture, routed input, timing, invalidation, and reentrant invalidation
  behavior are deterministic;
- command dispatch, theme resolution, hit testing, semantics, and animation
  scheduling are session-scoped;
- multiple sessions do not share static UI state;
- the legacy adapter is isolated and obsolete-marked; and
- `Broiler.UI.Standard` still contains no type-specific concrete control.

## Validation

```powershell
dotnet build Broiler.Input\Broiler.Input.slnx
dotnet run --project Broiler.Input\Broiler.Input.Contract.Tests\Broiler.Input.Contract.Tests.csproj --no-build
dotnet test Broiler.UI\Broiler.UI.slnx --no-restore
```
