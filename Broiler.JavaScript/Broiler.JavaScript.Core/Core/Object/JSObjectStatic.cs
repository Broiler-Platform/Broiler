using Broiler.JavaScript.Core.Core;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Core;

public static class JSObjectStatic
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JSObject ToJSObject(this JSValue value)
    {
        if (value is JSObject @object)
            return @object;

        if (value.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSException.Cannot_convert_undefined_or_null_to_object);

        throw JSContext.NewTypeError(JSException.Parameter_is_not_an_object);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryAsObjectThrowIfNullOrUndefined(this JSValue value, out JSObject @object)
    {
        if (value.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSException.Cannot_convert_undefined_or_null_to_object);

        @object = value as JSObject;
        return @object != null;
    }
}
