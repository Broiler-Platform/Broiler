
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitReturnStatement(AstReturnStatement returnStatement) =>
        YExpression.Return(scope.Top.ReturnLabel, returnStatement.Argument != null ? VisitExpression(returnStatement.Argument) : JSUndefinedBuilder.Value);
}
