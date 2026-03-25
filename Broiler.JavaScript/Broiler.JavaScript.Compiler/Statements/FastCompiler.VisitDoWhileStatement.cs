using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    // In doWhile continue should preced the test
    protected override YExpression VisitDoWhileStatement(AstDoWhileStatement doWhileStatement, string label = null)
    {
        var breakTarget = YExpression.Label();
        var continueTarget = YExpression.Label();

        using var s = scope.Top.Loop.Push(new LoopScope(breakTarget, continueTarget, false, label));
        var body = VisitStatement(doWhileStatement.Body);
        var test = YExpression.Not(JSValueBuilder.BooleanValue(VisitExpression(doWhileStatement.Test)));
        
        return YExpression.Loop(YExpression.Block(body, YExpression.Label(continueTarget), YExpression.IfThen(test, YExpression.Goto(breakTarget))), breakTarget, null);
    }
}
