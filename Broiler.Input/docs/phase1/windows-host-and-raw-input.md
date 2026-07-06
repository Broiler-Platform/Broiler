# Broiler.Input Phase 1 Windows Host And Raw Input Contracts

**Status:** Implemented  
**Date:** 2026-07-02

## Host Boundary

`Broiler.Input.Windows` defines `IWindowsInputHost` as the narrow bridge from a
Windows message owner to Input:

| Member | Meaning |
|---|---|
| `MessageWindowHandle` | Borrowed HWND used only by Windows implementation contracts |
| `MessageReceived` | Emits `WindowsInputMessage` envelopes |
| `IsOnHostThread` | Reports host-thread affinity |
| `TryPost` | Allows non-blocking host-thread scheduling |

`WindowsInputMessageDispatcher` fans host messages out to registered
`IWindowsInputMessageSink` instances. It contains no keyboard or mouse parsing.

## Raw Input Lease

`WindowsRawInputRegistrationCoordinator` owns process-wide Raw Input
registration. Registration returns `WindowsRawInputRegistrationLease`, and
disposing the lease unregisters the device usage.

Phase 1 allows one live keyboard lease and one live mouse lease. Conflicting
registrations fail immediately instead of replacing another owner silently.

## Native Calls

The Windows support assembly uses:

- `QueryPerformanceCounter`;
- `QueryPerformanceFrequency`; and
- `RegisterRawInputDevices`.

All are declared through `LibraryImport` or `DllImport`.
