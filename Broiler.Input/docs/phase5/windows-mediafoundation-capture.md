# Phase 5 Windows Media Foundation Capture

`Broiler.Input.Camera.Windows` owns the first camera implementation. It targets
`net10.0-windows`, references only `Broiler.Input.Camera` and
`Broiler.Input.Windows`, and calls Windows through `DllImport` plus COM
interfaces declared inside the assembly.

## Enumeration

`WindowsCameraProvider.GetDevicesAsync` initializes Media Foundation, filters
device sources to video capture, and calls `MFEnumDeviceSources`. Descriptors use
a stable opaque Broiler ID derived from the Media Foundation symbolic link. The
symbolic link is kept as a Windows-specific capability so explicitly selected
cameras can be reopened without using display names.

## Capture

`WindowsCameraInputDevice.StartAsync` creates a background capture thread. The
session:

- activates the selected `IMFMediaSource`;
- creates an `IMFSourceReader`;
- selects the first video stream;
- negotiates the current native media type or an exact requested format;
- copies each contiguous sample buffer into a `CameraFrameLease`;
- derives plane metadata from the negotiated format;
- reports Source Reader format changes, stream ticks, end of stream, and
  timestamp errors; and
- shuts down the media source and releases COM objects before stop/dispose
  returns.

Unsupported requested formats fail as `UnsupportedCapability`. Privacy denial is
reported as `PermissionDenied` when Media Foundation returns access denied, and
source shutdown/device removal maps to `DeviceRemoved`.

## Boundary

The camera provider does not depend on DirectShow wrappers, OpenCV, AForge,
encoders, playback, graphics, or application assemblies.
