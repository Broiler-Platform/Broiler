using Exp = YantraJS.Expressions.YExpression;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{


    protected override Expression VisitIfStatement(AstIfStatement ifStatement)
    {
        var test = ExpHelper.JSValueBuilder.BooleanValue(VisitExpression(ifStatement.Test));
        var trueCase = VisitStatement(ifStatement.True).ToJSValue();
        if (ifStatement.False != null)
        {
            var elseCase = VisitStatement(ifStatement.False).ToJSValue();
            return Exp.Condition(test, trueCase, elseCase);
        }
        return Exp.Condition(test, trueCase, ExpHelper.JSUndefinedBuilder.Value);
    }
}
