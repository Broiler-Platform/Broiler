# Phase 5 Camera Contracts

Phase 5 adds a platform-neutral camera assembly without adding graphics, UI,
encoder, playback, or third-party dependencies.

## Runtime Surface

- `CameraInputDevice` extends `InputDevice` and requires `InputKind.Camera`.
- `ICameraInputProvider` opens camera devices with `CameraOpenOptions`.
- `CameraFormat` records width, height, frame rate, and pixel format.
- `CameraCapability` describes a supported camera format and native subtype.
- `CameraFrameLease` owns copied frame bytes and plane metadata. Disposing the
  lease invalidates its memory.
- `CameraSessionOptions` selects latest-frame preview mode or explicit
  loss-sensitive delivery mode using `InputDeliveryOptions`.
- `CameraLatestFramePreviewAdapter` is an application-level adapter that keeps
  only the latest frame without referencing Broiler.Graphics.

## Delivery Modes

Latest-frame preview mode uses a bounded latest-frame queue. Slow consumers do
not grow memory because older frames are disposed before the newest frame is
queued.

Loss-sensitive mode is explicit. Callers choose the queue capacity and overflow
policy, and drops are reflected in `CameraCaptureStatistics`.

## Metadata

Frames carry plane layout, rotation, color space, timestamp, frame number, and
flags for discontinuity, format change, end of stream, and timestamp errors.
