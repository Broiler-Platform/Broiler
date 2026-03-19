using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{

    protected override CodeInfo VisitGoto(YGoToExpression yGoToExpression)
    {
        il.Branch(labels[yGoToExpression.Target]);
        return true;
    }

}
