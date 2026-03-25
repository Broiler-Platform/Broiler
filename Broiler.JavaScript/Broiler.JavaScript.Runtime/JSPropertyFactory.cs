using System.Runtime.CompilerServices;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Runtime;

/// <summary>
/// Factory methods for creating <see cref="JSProperty"/> instances that require
/// concrete runtime types (<see cref="JSFunctionDelegate"/>).
/// These methods cannot live on <see cref="JSProperty"/> itself because it resides
/// in the Storage assembly, which has no dependency on the runtime types.
/// </summary>
public static class JSPropertyFactory
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Function(in KeyString key, JSFunctionDelegate d, JSPropertyAttributes attributes = JSPropertyAttributes.ConfigurableValue, int length = 0)
    {
        var fx = JSValue.CreateFunction(d, key.ToString(), null, length);
        return new JSProperty(key, fx, null, attributes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSProperty Property(in KeyString key, JSFunctionDelegate get, JSFunctionDelegate set = null, JSPropertyAttributes attributes = JSPropertyAttributes.EnumerableConfigurableProperty)
    {
        var fget = get == null ? null : JSValue.CreateFunction(get, "get " + key.ToString());
        var fset = set == null ? null : JSValue.CreateFunction(set, "set " + key.ToString());

        return new JSProperty(key, fget, fset, attributes);
    }
}
