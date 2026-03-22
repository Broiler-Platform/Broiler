using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Expression VisitArrayExpression(AstArrayExpression arrayExpression)
    {
        var e = arrayExpression.Elements.GetFastEnumerator();
        var list = new Sequence<YElementInit>();

        while (e.MoveNext(out var item))
        {
            if (item == null)
            {
                list.Add(Expression.ElementInit(JSArrayBuilder._Add, [Expression.Null]));
                continue;
            }

            if (item.Type == FastNodeType.SpreadElement)
            {
                var i = (item as AstSpreadElement).Argument;
                list.Add(Expression.ElementInit(JSArrayBuilder._AddRange, [Visit(i)]));
                continue;
            }

            list.Add(Expression.ElementInit(JSArrayBuilder._Add, [Visit(item)]));
        }

        if (list.Count > 0)
            return Expression.ListInit(Expression.New(JSArrayBuilder._New), list);

        return Expression.New(JSArrayBuilder._New);
    }
}
