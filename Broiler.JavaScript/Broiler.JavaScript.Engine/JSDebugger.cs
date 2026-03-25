using Broiler.JavaScript.Runtime;
using System;

namespace Broiler.JavaScript.Engine;

/// <summary>
/// Abstract base class for JavaScript debuggers.
/// Provides the static <see cref="Break"/> event used by the compiled
/// debugger-statement handler and delegates per-instance behaviour to
/// derived classes via the <see cref="IDebugger"/> contract.
/// </summary>
public abstract class JSDebugger : IDebugger
{
    public static event EventHandler Break;

    public static object RaiseBreak()
    {
        Break?.Invoke(null, EventArgs.Empty);
        return null;
    }

    public abstract void ReportException(JSValue error);
    public abstract void ScriptParsed(long id, string code, string codeFilePath);
}
