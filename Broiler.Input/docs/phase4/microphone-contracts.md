# Phase 4 Microphone Contracts

Phase 4 adds a platform-neutral microphone assembly without adding media,
encoder, playback, UI, or third-party dependencies.

## Runtime Surface

- `MicrophoneInputDevice` extends `InputDevice` and requires
  `InputKind.Microphone`.
- `IMicrophoneInputProvider` opens microphone devices with
  `MicrophoneOpenOptions`.
- `MicrophoneFormat` records sample rate, channel count, bit depth, sample
  format, and derived byte/frame sizes.
- `MicrophoneBufferLease` owns copied capture bytes. Disposing the lease
  invalidates its memory view.
- `MicrophoneSessionOptions` carries bounded delivery options, silence
  reporting, discontinuity reporting, and requested shared-mode latency.
- `MicrophoneCaptureStatistics` reports captured, delivered, dropped, silent,
  discontinuous, and queued packet counts.

## Delivery Rule

Capture providers must copy native buffers into an owned lease before invoking
callbacks. A callback receives `MicrophoneBufferReadyEvent` and owns the lease
until it disposes it. Providers dispose buffers that are dropped before delivery
or captured after stop/dispose begins.

## Fault Rule

Providers surface privacy denial as `PermissionDenied`, busy endpoints as
`DeviceBusy`, unsupported requested formats as `UnsupportedCapability`, device
removal/invalidation as `DeviceRemoved`, and native WASAPI failures as
`NativeFailure` unless a more specific category is available.
