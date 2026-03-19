using System.CodeDom.Compiler;

namespace Broiler.JavaScript.ExpressionCompiler.Expressions;

public class YArrayLengthExpression(YExpression target) : YExpression(YExpressionType.ArrayLength, typeof(int))
{
    public readonly YExpression Target = target;

    public override void Print(IndentedTextWriter writer)
    {
        Target.Print(writer);
        writer.Write(".Length");
    }
}