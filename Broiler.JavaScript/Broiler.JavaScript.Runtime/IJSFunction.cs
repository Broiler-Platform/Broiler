using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core.Core;

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
}
