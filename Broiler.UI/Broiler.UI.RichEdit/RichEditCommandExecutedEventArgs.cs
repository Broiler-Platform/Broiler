using System;

namespace Broiler.UI.RichEdit;

/// <summary>Raised after a command runs, whether or not it changed state.</summary>
public sealed class RichEditCommandExecutedEventArgs : EventArgs
{
    public RichEditCommandExecutedEventArgs(RichEditCommand command, bool changed)
    {
        Command = command;
        Changed = changed;
    }

    public RichEditCommand Command { get; }

    /// <summary>True when the command had an effect (edited, moved selection, or wrote the clipboard).</summary>
    public bool Changed { get; }
}
