using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Expressions;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    private Exp InternalVisitUpdateExpression(AstUnaryExpression updateExpression)
    {
        // added support for a++, a--
        updateExpression.Argument.VerifyIdentifierForUpdate();

        var list = new Sequence<Exp>();

        FastFunctionScope.VariableScope target = null;
        FastFunctionScope.VariableScope @return = null;
        var right = VisitExpression(updateExpression.Argument);

        switch (right.NodeType)
        {
            case YExpressionType.Index:
                var index = right as YIndexExpression;
                target = scope.Top.GetTempVariable(index.Type);
                list.Add(Exp.Assign(target.Variable, index.Target));
                right = Exp.Index(target.Variable, index.Property, index.Arguments);
                break;
        }

        if (!updateExpression.Prefix)
        {
            @return = scope.Top.GetTempVariable(right.Type);
            list.Add(Exp.Assign(@return.Variable, right));
        }

        switch (updateExpression.Operator)
        {
            case UnaryOperator.Increment:
                list.Add(Exp.Assign(right, JSValueBuilder.AddDouble(right, Exp.Constant((double)1))));
                break;

            case UnaryOperator.Decrement:
                list.Add(Exp.Assign(right, JSValueBuilder.AddDouble(right, Exp.Constant((double)-1))));
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

        var r = Exp.Block(list);
        @return?.Dispose();
        target?.Dispose();

        return r;
    }
}
