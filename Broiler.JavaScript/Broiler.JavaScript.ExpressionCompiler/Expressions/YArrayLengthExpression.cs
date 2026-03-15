using System.CodeDom.Compiler;

namespace YantraJS.Expressions;

public class YArrayLengthExpression(YExpression target) : YExpression(YExpressionType.ArrayLength, typeof(int))
{
    public readonly YExpression Target = target;

    public override void Print(IndentedTextWriter writer)
    {
        Target.Print(writer);
        writer.Write(".Length");
    }
}