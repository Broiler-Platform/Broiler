using System.Threading.Tasks;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Abstraction over JavaScript Promise objects, allowing Core types
/// to detect and interact with promises without depending on the concrete
/// <c>JSPromise</c> class in BuiltIns.
/// </summary>
public interface IJSPromise
{
    /// <summary>Gets the promise result as a .NET Task.</summary>
    Task<JSValue> Task { get; }
}
