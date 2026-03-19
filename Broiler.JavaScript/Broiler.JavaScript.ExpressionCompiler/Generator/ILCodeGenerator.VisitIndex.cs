using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Generator;

namespace Broiler.JavaScript.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitIndex(YIndexExpression yIndexExpression)
    {
        Visit(yIndexExpression.Target);
        EmitParameters(yIndexExpression.GetMethod, yIndexExpression.Arguments, yIndexExpression.Type);
        il.EmitCall(yIndexExpression.GetMethod);
        return true;
    }
}
