using System.Collections.Generic;

namespace Broiler.App.Rendering;

/// <summary>
/// Abstraction over a JavaScript execution engine.
/// </summary>
public interface IScriptEngine
{
    /// <summary>
    /// Execute the supplied <paramref name="scripts"/> in a fresh context.
    /// Returns <c>true</c> when all scripts executed without error.
    /// </summary>
    bool Execute(IReadOnlyList<string> scripts);

    /// <summary>
    /// Execute the supplied <paramref name="scripts"/> in a fresh context
    /// with a <c>document</c> object derived from <paramref name="html"/>,
    /// enabling basic DOM interaction via the <see cref="DomBridge"/>.
    /// Returns <c>true</c> when all scripts executed without error.
    /// </summary>
    bool Execute(IReadOnlyList<string> scripts, string html);

    /// <summary>
    /// Execute scripts and return a detailed <see cref="ScriptExecutionResult"/>
    /// that includes per-script error messages and stack traces.
    /// </summary>
    ScriptExecutionResult ExecuteDetailed(IReadOnlyList<string> scripts);

    /// <summary>
    /// Whether strict mode (<c>"use strict";</c>) is prepended to every
    /// script before execution. Default is <c>false</c>.
    /// </summary>
    bool StrictModeEnabled { get; set; }

    /// <summary>
    /// The <see cref="ContentSecurityPolicy"/> applied to this engine.
    /// When set, <c>eval()</c> calls are gated by the policy.
    /// </summary>
    ContentSecurityPolicy? Csp { get; set; }

    /// <summary>
    /// Optional profiling hook. When set, every script execution is timed
    /// and recorded.
    /// </summary>
    ScriptProfilingHook? Profiler { get; set; }

    /// <summary>
    /// The micro-task queue used for <c>queueMicrotask</c> and
    /// <c>Promise</c> callbacks.
    /// </summary>
    MicroTaskQueue MicroTasks { get; }
}
