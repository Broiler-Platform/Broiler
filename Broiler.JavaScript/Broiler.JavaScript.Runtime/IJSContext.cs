using System;
using System.Threading.Tasks;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Minimal contract for a JavaScript execution context.
/// Core's <see cref="T:Broiler.JavaScript.Core.Core.JSContext"/> implements
/// this interface, allowing Runtime-level contracts (such as
/// <see cref="IBuiltInRegistry"/>) to reference a context without depending
/// on the concrete Core type.
/// </summary>
public interface IJSContext : IDisposable
{
    /// <summary>Unique identifier for this context instance.</summary>
    long ID { get; }

    /// <summary>
    /// Gets the code cache used for compiled script reuse in this context.
    /// </summary>
    ICodeCache CodeCache { get; }

    /// <summary>
    /// Gets a task that completes when all pending asynchronous operations
    /// in this context have finished.  May be <c>null</c> if no async
    /// operations are pending.
    /// </summary>
    Task WaitTask { get; }
}
