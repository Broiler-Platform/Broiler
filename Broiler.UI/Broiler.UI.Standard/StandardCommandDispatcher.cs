using System;
using System.Collections.Generic;

namespace Broiler.UI.Standard;

public sealed class StandardCommandDispatcher
{
    private readonly Dictionary<string, StandardCommand> _commands = new(StringComparer.Ordinal);

    public void Add(StandardCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_commands.TryAdd(command.Name, command))
            throw new InvalidOperationException($"Duplicate UI command '{command.Name}'.");
    }

    public bool TryExecute(string name) =>
        _commands.TryGetValue(name, out StandardCommand? command) && command.TryExecute();
}

