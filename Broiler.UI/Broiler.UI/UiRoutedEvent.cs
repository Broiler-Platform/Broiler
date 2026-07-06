using System;

namespace Broiler.UI;

public sealed class UiRoutedEvent
{
    public UiRoutedEvent(string name, UiRoutedEventStrategy strategy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Routed event names must be non-empty.", nameof(name));

        Name = name;
        Strategy = strategy;
    }

    public string Name { get; }

    public UiRoutedEventStrategy Strategy { get; }

    public static UiRoutedEvent Input { get; } = new("Input", UiRoutedEventStrategy.Bubble);
}

