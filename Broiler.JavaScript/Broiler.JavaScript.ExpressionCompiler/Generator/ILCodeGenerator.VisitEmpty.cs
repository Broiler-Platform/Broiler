using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{

    protected override CodeInfo VisitEmpty(YEmptyExpression exp) => true;

}
