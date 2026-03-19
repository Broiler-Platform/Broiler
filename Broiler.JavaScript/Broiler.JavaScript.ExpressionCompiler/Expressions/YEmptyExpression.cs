using System.CodeDom.Compiler;

namespace Broiler.JavaScript.ExpressionCompiler.Expressions;

public class YEmptyExpression: YExpression
{
    public YEmptyExpression()
        : base( YExpressionType.Empty, typeof(void))
    {
    }

    public override void Print(IndentedTextWriter writer) => writer.Write("<void>");
}