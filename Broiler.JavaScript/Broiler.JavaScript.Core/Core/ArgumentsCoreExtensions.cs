using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Core.Core.Array;
using Broiler.JavaScript.Core.Core.Primitive;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Core.Core;

/// <summary>
/// Core-dependent implementations for <see cref="Arguments"/> factory delegates.
/// These methods were extracted from Arguments when it moved to Runtime because
/// they depend on Core-only types (JSArray, JSString, JSArguments, JSException).
/// </summary>
internal static class ArgumentsCoreExtensions
{
    internal static Arguments ForApplyCore(JSValue @this, JSValue args)
    {
        if (args.IsArray && args is JSArray argArray)
        {
            var length = argArray._length;
            switch (length)
            {
                case 0:
                    return new Arguments(@this);
                case 1:
                    return new Arguments(@this, argArray[0u]);
                case 2:
                    return new Arguments(@this, argArray[0u], argArray[1u]);
                case 3:
                    return new Arguments(@this, argArray[0u], argArray[1u], argArray[2u]);
                case 4:
                    return new Arguments(@this, argArray[0u], argArray[1u], argArray[2u], argArray[3u]);
                default:
                    var argList = new JSValue[argArray._length];
                    var ee = argArray.GetElementEnumerator();
                    while (ee.MoveNext(out var hasValue, out var value, out var index))
                        argList[index] = hasValue ? value : JSUndefined.Value;
                    return new Arguments(@this, argList);
            }
        }

        if (args is JSArguments arguments)
        {
            var length = arguments.Length;
            switch (length)
            {
                case 0:
                    return new Arguments(@this);
                case 1:
                    return new Arguments(@this, arguments[0u]);
                case 2:
                    return new Arguments(@this, arguments[0u], arguments[1u]);
                case 3:
                    return new Arguments(@this, arguments[0u], arguments[1u], arguments[2u]);
                case 4:
                    return new Arguments(@this, arguments[0u], arguments[1u], arguments[2u], arguments[3u]);
                default:
                    var argList = new JSValue[arguments.Length];
                    var ee = arguments.GetElementEnumerator();
                    while (ee.MoveNext(out var hasValue, out var value, out var index))
                        argList[index] = hasValue ? value : JSUndefined.Value;
                    return new Arguments(@this, argList);
            }
        }

        return new Arguments(@this);
    }

    internal static JSValue RestFromCore(Arguments self, uint index)
    {
        var a = new JSArray();
        ref var ae = ref a.GetElements(true);
        uint ai;
        uint i;
        for (ai = 0, i = index; i < self.Length; i++, ai++)
            ae.Put(ai, self.GetAt((int)i));
        a._length = ai;
        return a;
    }

    internal static StringSpan GetStringCore(JSValue item, string name, string function, string filePath, int line) =>
        item is JSString s ? s.value : throw new JSException(name + " is required", function, filePath, line);

    internal static JSValue GetSpreadTargetCore(JSValue a) =>
        ((JSSpreadValue)a).Value;
}
