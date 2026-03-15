#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Reflection;
using YantraJS.Core;

namespace YantraJS.Expressions;

public class YConvertExpression(YExpression exp, Type type, MethodInfo? method) : YExpression(YExpressionType.Convert, type)
{
    public readonly YExpression Target = exp;
    public readonly MethodInfo? Method = method;

    private static Sequence<(MethodInfo method, Type inputType)> ConvertMethods =
        new( typeof(Convert).GetMethods()
            .Select(x => (x, x.GetParameters()))
            .Where(x => x.Item2.Length == 1)
            .Select(x => (x.Item1, x.Item2.First().ParameterType)));

    public static bool TryGetConversionMethod(Type from, Type to, out MethodInfo? m)
    {
        if (to.IsAssignableFrom(from))
        {
            m = null;
            return true;
        }

        //var nfrom = Nullable.GetUnderlyingType(from);
        //from = nfrom ?? from;

        var (method, inputType) = ConvertMethods.FirstOrDefault((m) => m.method.ReturnType == to
            && m.inputType == from);
        if (method == null)
        {
            m = default;
            return false;
        }
        m = method;
        return true;
    }

    public override void Print(IndentedTextWriter writer)
    {
        writer.Write("convert(");
        Target.Print(writer);
        writer.Write(")");
    }
}