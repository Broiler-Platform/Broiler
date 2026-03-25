namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Abstraction over a JavaScript Generator value, allowing Core
/// types to interact with generators without depending on the
/// concrete <c>JSGenerator</c> class in BuiltIns.
/// </summary>
public interface IJSGenerator
{
    /// <summary>Advances the generator, returning whether there are more values.</summary>
    bool MoveNext(JSValue replaceOld, out JSValue item);

    /// <summary>Injects an exception into the generator.</summary>
    JSValue Throw(JSValue value);
}
