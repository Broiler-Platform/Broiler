using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.Core.Utils;
using Exp = YantraJS.Expressions.YExpression;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Expression VisitIfStatement(AstIfStatement ifStatement)
    {
        var test = JSValueBuilder.BooleanValue(VisitExpression(ifStatement.Test));
        var trueCase = VisitStatement(ifStatement.True).ToJSValue();

        if (ifStatement.False != null)
        {
            var elseCase = VisitStatement(ifStatement.False).ToJSValue();
            return Exp.Condition(test, trueCase, elseCase);
        }

        return Exp.Condition(test, trueCase, JSUndefinedBuilder.Value);
    }
}
