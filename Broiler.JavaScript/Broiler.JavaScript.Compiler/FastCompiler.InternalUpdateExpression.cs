using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    private YExpression InternalVisitUpdateExpression(AstUnaryExpression updateExpression)
    {
        // added support for a++, a--
        updateExpression.Argument.VerifyIdentifierForUpdate();

        var list = new Sequence<YExpression>();

        FastFunctionScope.VariableScope target = null;
        FastFunctionScope.VariableScope @return = null;
        var right = VisitExpression(updateExpression.Argument);

        switch (right.NodeType)
        {
            case YExpressionType.Index:
                var index = right as YIndexExpression;
                target = scope.Top.GetTempVariable(index.Type);
                list.Add(YExpression.Assign(target.Variable, index.Target));
                right = YExpression.Index(target.Variable, index.Property, index.Arguments);
                break;
        }

        if (!updateExpression.Prefix)
        {
            @return = scope.Top.GetTempVariable(right.Type);
            list.Add(YExpression.Assign(@return.Variable, right));
        }

        switch (updateExpression.Operator)
        {
            case UnaryOperator.Increment:
                list.Add(YExpression.Assign(right, JSValueBuilder.AddDouble(right, YExpression.Constant((double)1))));
                break;

            case UnaryOperator.Decrement:
                list.Add(YExpression.Assign(right, JSValueBuilder.AddDouble(right, YExpression.Constant((double)-1))));
                break;
        }

        if (!updateExpression.Prefix)
        {
            list.Add(@return.Variable);
        }
        else
        {
            list.Add(right);
        }

        var r = YExpression.Block(list);
        @return?.Dispose();
        target?.Dispose();

        return r;
    }
}
