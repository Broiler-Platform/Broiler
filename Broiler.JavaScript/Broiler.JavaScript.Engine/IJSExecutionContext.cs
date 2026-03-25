using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Engine;

/// <summary>
/// Extended execution context contract used internally by Engine and
/// higher-level types that need access to prototype objects, the call
/// stack, and variable registration.
/// <see cref="IJSContext"/> (in Runtime) provides the minimal contract;
/// this interface adds properties required by types that participate in
/// execution (e.g., <c>JSContext</c>, <c>CallStackItem</c>).
/// </summary>
public interface IJSExecutionContext : IJSContext
{
    /// <summary>Gets the Function.prototype object for this context.</summary>
    JSObject FunctionPrototype { get; }

    /// <summary>Gets the Object.prototype object for this context.</summary>
    JSObject ObjectPrototype { get; }

    /// <summary>Gets the global Object constructor for this context.</summary>
    JSValue Object { get; }

    /// <summary>Gets or sets the current top of the call stack.</summary>
    CallStackItem Top { get; set; }

    /// <summary>Gets or sets the current <c>new.target</c> value.</summary>
    JSValue CurrentNewTarget { get; set; }

    /// <summary>Registers a global variable in this context.</summary>
    JSValue Register(JSVariable variable);

    /// <summary>Dispatches an eval event if any handlers are registered.</summary>
    void DispatchEvalEvent(ref string script, ref string location);
}
