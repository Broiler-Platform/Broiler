namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Abstraction over a JavaScript function value, allowing Runtime
/// types to invoke functions without depending on the concrete
/// <c>JSFunction</c> class in Core.
/// </summary>
public interface IJSFunction
{
    /// <summary>Invokes this function with the specified arguments.</summary>
    /// <param name="a">The arguments to pass to the function.</param>
    /// <returns>The return value produced by the function.</returns>
    JSValue InvokeFunction(in Arguments a);

    /// <summary>
    /// Gets or sets the underlying <see cref="JSFunctionDelegate"/> that implements
    /// this function's invocation logic.
    /// </summary>
    JSFunctionDelegate Delegate { get; set; }

    /// <summary>
    /// Gets the prototype object associated with this function.
    /// </summary>
    JSValue Prototype { get; }
}
