namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Defines the contract for a JavaScript debugger.
/// Implementations receive notifications about parsed scripts and runtime
/// exceptions so that external tooling (e.g., DAP or V8 inspector) can
/// observe and control execution.
/// </summary>
public interface IDebugger
{
    /// <summary>
    /// Reports an exception that occurred during script execution.
    /// </summary>
    /// <param name="error">The JavaScript value representing the error.</param>
    void ReportException(JSValue error);

    /// <summary>
    /// Notifies the debugger that a script has been parsed and is about to execute.
    /// </summary>
    /// <param name="id">The context identifier that owns the script.</param>
    /// <param name="code">The full source code of the script.</param>
    /// <param name="codeFilePath">
    /// An optional file path or URL associated with the script source.
    /// </param>
    void ScriptParsed(long id, string code, string codeFilePath);
}
