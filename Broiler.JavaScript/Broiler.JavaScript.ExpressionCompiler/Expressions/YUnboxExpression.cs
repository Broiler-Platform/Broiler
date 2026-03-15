using System;
using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YUnboxExpression(YExpression target, Type type) : YExpression(YExpressionType.Unbox, type)
{
    public readonly YExpression Target = target;

    public override void Print(IndentedTextWriter writer)
    {
        writer.Write($"({Type.GetFriendlyName()})");
        Target.Print(writer);
    }
}