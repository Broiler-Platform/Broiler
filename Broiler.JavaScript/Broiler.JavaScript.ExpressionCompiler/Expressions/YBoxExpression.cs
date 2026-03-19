using System.CodeDom.Compiler;

namespace Broiler.JavaScript.ExpressionCompiler.Expressions;

public class YBoxExpression(YExpression target) : YExpression(YExpressionType.Box, typeof(object))
{
    public readonly YExpression Target = target;

    public override void Print(IndentedTextWriter writer)
    {
        Target.Print(writer);
        writer.Write(" as object");
    }
}
