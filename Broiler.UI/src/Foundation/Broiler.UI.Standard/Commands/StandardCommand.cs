using System;

namespace Broiler.UI.Standard;

public sealed class StandardCommand
{
    public StandardCommand(string name, Action execute, Func<bool>? canExecute = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Command names must be non-empty.", nameof(name));

        Name = name;
        ExecuteCore = execute ?? throw new ArgumentNullException(nameof(execute));
        CanExecuteCore = canExecute;
    }

    public string Name { get; }

    public bool CanExecute => CanExecuteCore?.Invoke() ?? true;

    private Action ExecuteCore { get; }

    private Func<bool>? CanExecuteCore { get; }

    public bool TryExecute()
    {
        if (!CanExecute)
            return false;

        ExecuteCore();
        return true;
    }
}

