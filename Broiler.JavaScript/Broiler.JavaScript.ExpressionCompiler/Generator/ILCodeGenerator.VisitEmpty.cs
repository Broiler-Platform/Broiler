using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Generator;

public partial class ILCodeGenerator
{

    protected override CodeInfo VisitEmpty(YEmptyExpression exp) => true;

}
