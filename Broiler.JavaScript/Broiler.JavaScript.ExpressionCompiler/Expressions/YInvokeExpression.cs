#nullable enable
using Broiler.JavaScript.ExpressionCompiler.Core;
using System;
using System.CodeDom.Compiler;

namespace Broiler.JavaScript.ExpressionCompiler.Expressions;

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