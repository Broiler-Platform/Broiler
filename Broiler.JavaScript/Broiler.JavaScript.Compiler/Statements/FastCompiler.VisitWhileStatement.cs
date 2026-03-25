using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitWhileStatement(AstWhileStatement whileStatement, string label = null)
    {
        var breakTarget = YExpression.Label();
        var continueTarget = YExpression.Label();

        using var s = scope.Top.Loop.Push(new LoopScope(breakTarget, continueTarget, false, label));
        var body = Visit(whileStatement.Body);
        var test = YExpression.Not(JSValueBuilder.BooleanValue(Visit(whileStatement.Test)));

        return YExpression.Loop(YExpression.Block(YExpression.IfThen(test, YExpression.Goto(breakTarget)), body), breakTarget, continueTarget);
    }
}
