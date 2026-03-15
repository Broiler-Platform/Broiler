#nullable enable
using System;
using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YAssignExpression(YExpression left, YExpression right, Type? type) : YExpression(YExpressionType.Assign, type ?? left.Type)
{
    public readonly YExpression Left = left;
    public readonly YExpression Right = right;

    public override void Print(IndentedTextWriter writer)
    {
        Left.Print(writer);
        writer.Write(" = ");
        Right.Print(writer);
    }
}