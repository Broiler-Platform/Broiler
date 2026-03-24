using System;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitYield(YYieldExpression node) => throw new NotImplementedException();
}
