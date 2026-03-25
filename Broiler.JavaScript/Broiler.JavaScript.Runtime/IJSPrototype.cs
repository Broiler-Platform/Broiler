using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Abstraction over the prototype chain, allowing Runtime types
/// to traverse prototypes without depending on the concrete
/// <c>JSPrototype</c> class in BuiltIns.
/// </summary>
public interface IJSPrototype
{
    /// <summary>Gets the object this prototype wraps.</summary>
    JSValue Object { get; }

    /// <summary>Looks up a property by string key.</summary>
    JSProperty GetInternalProperty(in KeyString name);

    /// <summary>Looks up a property by numeric index.</summary>
    JSProperty GetInternalProperty(uint name);

    /// <summary>Looks up a property by symbol.</summary>
    JSProperty GetInternalProperty(IJSSymbol symbol);

    /// <summary>Gets the function delegate for a string-keyed method.</summary>
    JSFunctionDelegate GetMethod(in KeyString key);

    /// <summary>Marks the prototype as dirty (needs rebuild).</summary>
    void Dirty();

    /// <summary>Tries to remove an element at the given index from the prototype chain.</summary>
    bool TryRemove(uint i, out JSProperty p);
}
