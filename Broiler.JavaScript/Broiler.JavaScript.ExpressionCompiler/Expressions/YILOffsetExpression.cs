using System.CodeDom.Compiler;

namespace Broiler.JavaScript.ExpressionCompiler.Expressions;

public class YILOffsetExpression : YExpression
{

    public YILOffsetExpression():
        base (YExpressionType.ILOffset, typeof(int))
    {

    }

    public override void Print(IndentedTextWriter writer) => writer.WriteLine("// IL Offset");
}
