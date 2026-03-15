#nullable enable
using System;
using System.CodeDom.Compiler;
using YantraJS.Core;

namespace YantraJS.Expressions;

public class YInvokeExpression(YExpression target, IFastEnumerable<YExpression> args, Type type) : YExpression(YExpressionType.Invoke, type)
{
    public readonly YExpression Target = target;
    public readonly IFastEnumerable<YExpression> Arguments = args;

    public override void Print(IndentedTextWriter writer)
    {
        Target.Print(writer);
        writer.Write(".Invoke(");
        writer.PrintCSV(Arguments);
        writer.Write(")");
    }
}