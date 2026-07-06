using System;

namespace Broiler.UI.ToggleButton;

public sealed class UiToggleStateChangedEventArgs : EventArgs
{
    public UiToggleStateChangedEventArgs(UiToggleState oldState, UiToggleState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public UiToggleState OldState { get; }

    public UiToggleState NewState { get; }
}
