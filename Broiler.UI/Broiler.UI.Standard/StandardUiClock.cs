using System;
using Broiler.UI;

namespace Broiler.UI.Standard;

public sealed class StandardUiClock : IUiClock
{
    private readonly long _startTicks = Environment.TickCount64;

    public UiTimestamp Now =>
        new(TimeSpan.FromMilliseconds(Environment.TickCount64 - _startTicks));
}

