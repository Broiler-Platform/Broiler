using System;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Contract interface for the ES2025 DisposableStack built-in.
/// Lives in Runtime so that the Compiler can reference it without
/// depending on the concrete <c>JSDisposableStack</c> implementation
/// in the BuiltIns assembly.
/// </summary>
public interface IJSDisposableStack
{
    /// <summary>
    /// Adds a disposable resource to the stack.
    /// </summary>
    void AddDisposableResource(JSValue value, bool async);

    /// <summary>
    /// Disposes all resources on the stack. Returns a <see cref="JSValue"/>
    /// (either <c>undefined</c> for sync disposal or a Promise for async).
    /// </summary>
    JSValue Dispose();

    /// <summary>
    /// Factory delegate used by the Compiler to create new instances
    /// without referencing the concrete type.
    /// Wired by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    static Func<IJSDisposableStack> CreateNew { get; set; }

    /// <summary>
    /// Creates a new <see cref="IJSDisposableStack"/> instance via the
    /// registered factory delegate.
    /// </summary>
    static IJSDisposableStack New() => CreateNew();
}
