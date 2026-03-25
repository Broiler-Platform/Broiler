using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Extensions;

public static class JSPropertyExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue GetValue(this JSValue target, in JSProperty p)
    {
        if (p.IsEmpty)
            return JSUndefined.Value;

        return !p.IsProperty ? (JSValue)p.value : ((IJSFunction)p.get).InvokeFunction(new Arguments(target));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JSValue ToJSValue(in this JSProperty px)
    {
        var t = JSValue.BooleanTrue;
        var f = JSValue.BooleanFalse;
        JSObject obj;

        if (px.IsValue)
        {
            obj = JSObject.NewWithProperties()
                .AddProperty(KeyStrings.configurable, px.IsConfigurable ? t : f)
                .AddProperty(KeyStrings.enumerable, px.IsEnumerable ? t : f)
                .AddProperty(KeyStrings.writable, !px.IsReadOnly ? t : f)
                .AddProperty(KeyStrings.value, (JSValue)px.value);
        }
        else
        {
            obj = JSObject.NewWithProperties()
                .AddProperty(KeyStrings.configurable, px.IsConfigurable ? t : f)
                .AddProperty(KeyStrings.enumerable, px.IsEnumerable ? t : f)
                .AddProperty(KeyStrings.@get, (JSValue)px.get)
                .AddProperty(KeyStrings.@set, (JSValue)px.set);
        }

        return obj;
    }
}
