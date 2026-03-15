using System;
using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YNewArrayBoundsExpression(Type type, YExpression size) : YExpression(YExpressionType.NewArrayBounds, type.MakeArrayType())
{
    public readonly Type ElementType = type;
    public readonly YExpression Size = size;

    public override void Print(IndentedTextWriter writer)
    {
        writer.Write($"new {ElementType.GetFriendlyName()} [");
        Size.Print(writer);
        writer.Write("]");
    }
}