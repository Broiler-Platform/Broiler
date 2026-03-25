using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Engine;

/// <summary>
/// Core-dependent implementations for <see cref="Arguments"/> factory delegates.
/// These methods were extracted from Arguments when it moved to Runtime because
/// they depend on Core-only types (JSArguments, JSException) and JSValue factory delegates.
/// </summary>
internal static class ArgumentsCoreExtensions
{
    internal static Arguments ForApplyCore(JSValue @this, JSValue args)
    {
        if (args.IsArray)
        {
            var length = (uint)args.Length;
            switch (length)
            {
                case 0:
                    return new Arguments(@this);
                case 1:
                    return new Arguments(@this, args[0u]);
                case 2:
                    return new Arguments(@this, args[0u], args[1u]);
                case 3:
                    return new Arguments(@this, args[0u], args[1u], args[2u]);
                case 4:
                    return new Arguments(@this, args[0u], args[1u], args[2u], args[3u]);
                default:
                    var argList = new JSValue[length];
                    var ee = args.GetElementEnumerator();
                    while (ee.MoveNext(out var hasValue, out var value, out var index))
                        argList[index] = hasValue ? value : JSUndefined.Value;
                    return new Arguments(@this, argList);
            }
        }

        if (args.TypeOf() == JSConstants.Arguments)
        {
            var length = args.Length;
            switch (length)
            {
                case 0:
                    return new Arguments(@this);
                case 1:
                    return new Arguments(@this, args[0u]);
                case 2:
                    return new Arguments(@this, args[0u], args[1u]);
                case 3:
                    return new Arguments(@this, args[0u], args[1u], args[2u]);
                case 4:
                    return new Arguments(@this, args[0u], args[1u], args[2u], args[3u]);
                default:
                    var argList = new JSValue[args.Length];
                    var ee = args.GetElementEnumerator();
                    while (ee.MoveNext(out var hasValue, out var value, out var index))
                        argList[index] = hasValue ? value : JSUndefined.Value;
                    return new Arguments(@this, argList);
            }
        }

        return new Arguments(@this);
    }

    internal static JSValue RestFromCore(Arguments self, uint index)
    {
        var a = JSValue.CreateArray();
        for (uint i = index; i < self.Length; i++)
            a.AddArrayItem(self.GetAt((int)i));
        return a;
    }

    internal static StringSpan GetStringCore(JSValue item, string name, string function, string filePath, int line) =>
        item.IsString ? item.StringValue : throw new JSException(name + " is required", function, filePath, line);

    internal static JSValue GetSpreadTargetCore(JSValue a) =>
        ((JSSpreadValue)a).Value;
}
