using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Generator;

namespace Broiler.JavaScript.Generator;


public partial class ILCodeGenerator
{

    protected override CodeInfo VisitConstant(YConstantExpression yConstantExpression)
    {
        il.EmitConstant(yConstantExpression.Value, yConstantExpression.Type);
        return true;
    }

}
