#nullable enable
using System;
using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YThrowExpression(YExpression exp, Type? type = null) : YExpression(YExpressionType.Throw, typeof(void))
{
    public readonly YExpression Expression = exp;

    public override void Print(IndentedTextWriter writer)
    {
        writer.Write("throw ");
        Expression.Print(writer);
    }
}