using System;

namespace Broiler.UI.CheckBox;

public sealed class UiCheckStateChangedEventArgs : EventArgs
{
    public UiCheckStateChangedEventArgs(UiCheckState oldState, UiCheckState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public UiCheckState OldState { get; }

    public UiCheckState NewState { get; }
}
