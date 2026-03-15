using System;

using Exp = YantraJS.Expressions.YExpression;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Exp VisitArrayPattern(AstArrayPattern arrayPattern) => throw new NotImplementedException();

    protected override Expression VisitObjectPattern(AstObjectPattern objectPattern) => throw new NotImplementedException();
}
