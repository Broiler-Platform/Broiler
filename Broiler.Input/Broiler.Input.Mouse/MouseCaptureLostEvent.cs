using Broiler.Input;

namespace Broiler.Input.Mouse;

public readonly record struct MouseCaptureLostEvent(
    InputEventHeader Header,
    InputPoint? LastKnownPosition = null,
    InputEventSource Source = InputEventSource.Semantic);
