using System;
using YantraJS.Expressions;

namespace YantraJS.Generator;

public partial class ILCodeGenerator
{
    protected override CodeInfo VisitYield(YYieldExpression node) => throw new NotImplementedException();
}
