
using Broiler.JavaScript.Ast.Patterns;
using System;

using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Exp VisitArrayPattern(AstArrayPattern arrayPattern) => throw new NotImplementedException();

    protected override Expression VisitObjectPattern(AstObjectPattern objectPattern) => throw new NotImplementedException();
}
