using Exp = YantraJS.Expressions.YExpression;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Expression VisitWhileStatement(AstWhileStatement whileStatement, string label = null)
    {
        var breakTarget = Exp.Label();
        var continueTarget = Exp.Label();
        using (var s = scope.Top.Loop.Push(new LoopScope(breakTarget, continueTarget, false, label)))
        {

            var body = Visit(whileStatement.Body);

            var test = Exp.Not(ExpHelper.JSValueBuilder.BooleanValue(Visit(whileStatement.Test)));

            return Exp.Loop(
                Exp.Block(Exp.IfThen(test, Exp.Goto(breakTarget)), body),
                breakTarget,
                continueTarget);
        }
    }

}
