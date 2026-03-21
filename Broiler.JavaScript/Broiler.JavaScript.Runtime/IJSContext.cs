using System;

namespace Broiler.JavaScript.Core.Core;

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
}
