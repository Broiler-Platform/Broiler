using System;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Clr;

/// <summary>
/// Default <see cref="IClrInterop"/> implementation that delegates to the
/// existing static <see cref="ClrProxy.Marshal(object)"/> and
/// <see cref="ClrType.From(Type)"/> helpers.
/// </summary>
public sealed class DefaultClrInterop : IClrInterop
{
    /// <summary>
    /// Shared singleton instance — the default interop is stateless.
    /// </summary>
    public static readonly DefaultClrInterop Instance = new();

    /// <inheritdoc />
    public JSValue Marshal(object value) => ClrProxy.Marshal(value);

    /// <inheritdoc />
    public JSValue GetClrType(Type type) => ClrType.From(type);

    /// <inheritdoc />
    public bool TryUnwrapClrObject(JSValue value, out object clrObject)
    {
        if (value is ClrProxy proxy)
        {
            clrObject = proxy.value;
            return true;
        }

        clrObject = null;
        return false;
    }
}
