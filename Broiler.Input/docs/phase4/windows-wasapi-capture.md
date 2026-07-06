# Phase 4 Windows WASAPI Capture

`Broiler.Input.Microphone.Windows` owns the first OS implementation. It targets
`net10.0-windows`, references only `Broiler.Input.Microphone` and
`Broiler.Input.Windows`, and uses .NET runtime interop for Windows calls.

## Endpoint Enumeration

`WindowsMicrophoneProvider.GetDevicesAsync` enumerates active capture endpoints
through `MMDeviceEnumerator`. Descriptors use stable opaque Broiler IDs and keep
the native endpoint ID in a Windows-specific capability so an explicitly chosen
endpoint can be reopened without switching to a later default endpoint.

`RefreshDevicesAsync` compares the latest endpoint set against the provider
cache and raises `Added`, `Removed`, `Changed`, and `DefaultChanged` events.
Default roles are `Console`, `Multimedia`, and `Communications`.

## Capture Session

`WindowsMicrophoneInputDevice.StartAsync` creates a capture thread that:

- initializes COM for the thread;
- activates `IAudioClient` from the selected endpoint;
- reads the shared-mode mix format;
- rejects a preferred format when it does not match the shared-mode mix format;
- initializes WASAPI shared/event capture;
- waits on the WASAPI event handle;
- copies each `IAudioCaptureClient` packet into a `MicrophoneBufferLease`;
- reports silence, discontinuity, and timestamp-error flags; and
- stops and releases COM/native handles before `StopAsync` or disposal returns.

Packet timestamps prefer the WASAPI QPC timestamp when available and fall back to
the component input clock when WASAPI reports a timestamp error.

## Non-Goals

Phase 4 does not add encoding, playback, resampling, UI preview controls, or
dependencies on application components.
