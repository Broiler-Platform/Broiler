# Phase 4 Tests

The default contract runner remains hardware-free. It uses
`FakeMicrophoneProvider` and `FakeMicrophoneInputDevice` to prove bounded
delivery, lease ownership, default-device change observation, and assembly
isolation.

## Normal Contract Checks

```powershell
dotnet build Broiler.Input\Broiler.Input.slnx
dotnet run --project Broiler.Input\Broiler.Input.Contract.Tests\Broiler.Input.Contract.Tests.csproj --no-build
```

These checks must not require a microphone, a specific privacy setting, or a
running audio service.

## Labeled Hardware Checks

Hardware checks should be labeled and run only on machines that opt in to audio
capture. The checklist is:

- built-in and USB microphones enumerate with stable Broiler IDs;
- default console, multimedia, and communications roles are observable;
- shared/event capture starts and produces buffers for the default endpoint;
- sustained capture under the baseline workload keeps queue depth bounded;
- silence and discontinuity counters match deliberate mute/unplug scenarios;
- explicit endpoint capture does not silently switch when the default changes;
- stop/dispose releases the endpoint and produces no later callback;
- privacy denial, busy endpoint, unsupported format, and invalidation map to
  the documented `InputErrorCategory` values.
