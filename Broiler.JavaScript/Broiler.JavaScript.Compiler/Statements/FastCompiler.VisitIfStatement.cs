
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.LinqExpressions.Utils;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitIfStatement(AstIfStatement ifStatement)
    {
        var test = JSValueBuilder.BooleanValue(VisitExpression(ifStatement.Test));
        var trueCase = VisitStatement(ifStatement.True).ToJSValue();

        if (ifStatement.False != null)
        {
            var elseCase = VisitStatement(ifStatement.False).ToJSValue();
            return YExpression.Condition(test, trueCase, elseCase);
        }

        return YExpression.Condition(test, trueCase, JSUndefinedBuilder.Value);
    }
}
