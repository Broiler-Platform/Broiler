using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Array;
using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Error;
using Broiler.JavaScript.Core.Core.Object;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Storage;
using Broiler.JavaScript.Core.Extensions;
using System;
using System.Linq;
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
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        throw JSContext.NewTypeError(JSError.Parameter_is_not_an_object);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryAsObjectThrowIfNullOrUndefined(this JSValue value, out JSObject @object)
    {
        if (value.IsNullOrUndefined)
            throw JSContext.NewTypeError(JSError.Cannot_convert_undefined_or_null_to_object);

        @object = value as JSObject;
        return @object != null;
    }
}
