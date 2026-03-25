using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Runtime;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.BuiltIns.String;

internal static class JSStringExtensions
{

    internal static JSString AsJSString(this JSValue v, [CallerMemberName] string helper = null)
    {
        if (v.IsNullOrUndefined)
            throw JSEngine.NewTypeError($"String.prototype.{helper} called on null or undefined");

        if (v is JSString str)
            return str;

        if (v is JSPrimitiveObject primitiveObject)
            return primitiveObject.value.AsJSString();

        throw JSEngine.NewTypeError($"String.prototype.{helper} called with non string");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string AsString(this JSValue v, [CallerMemberName] string helper = null)
    {
        if (v.IsNullOrUndefined)
            throw JSEngine.NewTypeError($"String.prototype.{helper} called on null or undefined");

        return v.ToString();
    }
}
