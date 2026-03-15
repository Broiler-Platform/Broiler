#nullable enable
using System.CodeDom.Compiler;
using System.Reflection;
using YantraJS.Core;

namespace YantraJS.Expressions;

public class YCallExpression(YExpression? target, MethodInfo method, IFastEnumerable<YExpression> args) : YExpression(YExpressionType.Call, method.ReturnType)
{
    public readonly YExpression? Target = target;
    public readonly MethodInfo Method = method;
    public readonly IFastEnumerable<YExpression> Arguments = args;

    public override void Print(IndentedTextWriter writer)
    {
        if (Target == null)
        {
            // static method...
            writer.Write($"{Method.DeclaringType.GetFriendlyName()}.{Method.Name}(");
        }
        else
        {
            Target.Print(writer);
            writer.Write($".{Method.Name}(");
        }
        writer.PrintCSV(Arguments);
        writer.Write(')');
    }
}