using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitProperty(YPropertyExpression yPropertyExpression)
    {
        if (!yPropertyExpression.IsStatic)
        {
            Visit(yPropertyExpression.Target);
        }
        il.EmitCall(yPropertyExpression.GetMethod);
        return true;
    }

}
