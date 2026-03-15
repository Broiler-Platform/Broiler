using YantraJS.Expressions;

namespace YantraJS.Generator;


public partial class ILCodeGenerator
{

    protected override CodeInfo VisitConstant(YConstantExpression yConstantExpression)
    {
        il.EmitConstant(yConstantExpression.Value, yConstantExpression.Type);
        return true;
    }

}
