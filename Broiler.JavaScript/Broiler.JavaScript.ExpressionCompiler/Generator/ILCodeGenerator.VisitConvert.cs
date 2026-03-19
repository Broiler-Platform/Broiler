using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Generator;

namespace Broiler.JavaScript.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitConvert(YConvertExpression convertExpression)
    {
        Visit(convertExpression.Target);
        il.EmitCall(convertExpression.Method);
        return true;
    }
}
