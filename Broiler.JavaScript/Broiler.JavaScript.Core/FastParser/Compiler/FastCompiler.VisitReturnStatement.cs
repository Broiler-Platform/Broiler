using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.Core.LinqExpressions;
using Exp = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Exp VisitReturnStatement(AstReturnStatement returnStatement) => 
        Exp.Return(scope.Top.ReturnLabel, returnStatement.Argument != null ? VisitExpression(returnStatement.Argument) : JSUndefinedBuilder.Value);
}
