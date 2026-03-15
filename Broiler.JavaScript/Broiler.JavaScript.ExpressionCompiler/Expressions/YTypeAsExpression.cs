using System;
using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YTypeAsExpression(YExpression target, Type type) : YExpression(YExpressionType.TypeAs, type)
{
    public readonly YExpression Target = target;

    public override void Print(IndentedTextWriter writer)
    {
        Target.Print(writer);
        writer.Write(" as ");
        writer.Write(Type.GetFriendlyName());
    }
}