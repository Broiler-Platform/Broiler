using System;
using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YTypeIsExpression(YExpression target, Type type) : YExpression(YExpressionType.TypeIs, typeof(bool))
{
    public readonly YExpression Target = target;
    public readonly Type TypeOperand = type;

    public override void Print(IndentedTextWriter writer)
    {
        Target.Print(writer);
        writer.Write(" is ");
        writer.Write(TypeOperand.GetFriendlyName());
    }
}