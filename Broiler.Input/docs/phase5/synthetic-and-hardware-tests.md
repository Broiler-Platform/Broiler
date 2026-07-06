# Phase 5 Tests

The default contract runner remains hardware-free. It uses
`FakeCameraProvider` and `FakeCameraInputDevice` to prove bounded preview
delivery, loss-sensitive overflow, frame lease ownership, preview adapter
behavior, and assembly isolation.

## Normal Contract Checks

```powershell
dotnet build Broiler.Input\Broiler.Input.slnx
dotnet run --project Broiler.Input\Broiler.Input.Contract.Tests\Broiler.Input.Contract.Tests.csproj --no-build
```

These checks must not require a camera, privacy permission, or a running capture
service.

## Labeled Hardware Checks

Hardware checks should be labeled and opt-in. The checklist is:

- built-in and USB cameras enumerate even when display names collide;
- opaque IDs stay stable across refreshes for the same symbolic link;
- exact requested native formats are selected or fail clearly;
- slow consumers do not grow memory under preview mode;
- loss-sensitive mode reports dropped frames instead of silently replacing them;
- format changes, stream ticks, end of stream, privacy denial, and source
  shutdown map to documented flags/faults; and
- stop/dispose releases the camera indicator/resources and produces no later
  frame callback.
