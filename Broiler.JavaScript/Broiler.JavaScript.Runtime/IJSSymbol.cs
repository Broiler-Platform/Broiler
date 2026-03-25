namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Abstraction over a JavaScript Symbol value, allowing Runtime
/// types to accept symbol-typed keys without depending on the
/// concrete <c>JSSymbol</c> class in Core.
/// </summary>
public interface IJSSymbol
{
    /// <summary>Gets the internal numeric key that uniquely identifies this symbol.</summary>
    uint Key { get; }
}
