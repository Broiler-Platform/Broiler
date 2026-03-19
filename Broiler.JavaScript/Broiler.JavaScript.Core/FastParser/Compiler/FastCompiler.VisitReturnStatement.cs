
using Broiler.JavaScript.Core.LinqExpressions;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Exp VisitReturnStatement(AstReturnStatement returnStatement) => 
        Exp.Return(scope.Top.ReturnLabel, returnStatement.Argument != null ? VisitExpression(returnStatement.Argument) : JSUndefinedBuilder.Value);
}
