using System;

namespace Broiler.UI;

public sealed class UiRoutedEventArgs : EventArgs
{
    public UiRoutedEventArgs(UiRoutedEvent routedEvent, UiElement source, UiInputEvent? input = null)
    {
        RoutedEvent = routedEvent ?? throw new ArgumentNullException(nameof(routedEvent));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Input = input;
    }

    public UiRoutedEvent RoutedEvent { get; }

    public UiElement Source { get; }

    public UiInputEvent? Input { get; }

    public bool Handled { get; set; }
}

