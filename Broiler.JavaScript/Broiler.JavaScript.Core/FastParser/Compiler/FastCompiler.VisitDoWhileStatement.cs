using Broiler.JavaScript.Core.CodeGen;
using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.Core.LinqExpressions;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    // In doWhile continue should preced the test
    protected override Expression VisitDoWhileStatement(AstDoWhileStatement doWhileStatement, string label = null)
    {
        var breakTarget = Exp.Label();
        var continueTarget = Exp.Label();

        using var s = scope.Top.Loop.Push(new LoopScope(breakTarget, continueTarget, false, label));
        var body = VisitStatement(doWhileStatement.Body);
        var test = Exp.Not(JSValueBuilder.BooleanValue(VisitExpression(doWhileStatement.Test)));
        
        return Exp.Loop(Exp.Block(body, Exp.Label(continueTarget), Exp.IfThen(test, Exp.Goto(breakTarget))), breakTarget, null);
    }
}
