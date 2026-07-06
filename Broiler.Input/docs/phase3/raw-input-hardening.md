# Broiler.Input Phase 3 Raw Input Hardening

**Status:** Implemented inside `Broiler.Input`  
**Date:** 2026-07-03

## Physical Identity

`Broiler.Input.Windows` adds Raw Input report types that preserve the native
device handle as an opaque `WindowsRawInputDeviceIdentity`. Callers can convert
it to an opaque `InputDeviceId`; they must not parse it as a Windows device
path.

`WindowsRawInputReader` reads `WM_INPUT` payloads with `GetRawInputData` and
returns typed keyboard or mouse reports.

## Raw Mouse Coalescing

`Broiler.Input.Mouse.Windows` adds `WindowsRawMouseBuffer` for high-rate raw
mouse reports. It coalesces adjacent relative movement for the same physical
device only when no button data is present.

Metrics expose accepted, dequeued, coalesced, dropped, and queue-depth counts.
Movement from distinct physical devices is not coalesced together.

## Background Policy

Raw Input foreground mode remains the default. Background Raw Input requires:

- `ReceiveInputWhenNotFocused=true`;
- a non-zero target HWND; and
- `AcknowledgeBackgroundInput=true`.

This prevents accidental background capture from a single options flag.
