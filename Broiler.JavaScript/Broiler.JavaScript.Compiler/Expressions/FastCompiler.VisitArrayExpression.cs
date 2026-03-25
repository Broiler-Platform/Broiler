using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitArrayExpression(AstArrayExpression arrayExpression)
    {
        var e = arrayExpression.Elements.GetFastEnumerator();
        var list = new Sequence<YElementInit>();

        while (e.MoveNext(out var item))
        {
            if (item == null)
            {
                list.Add(YExpression.ElementInit(JSArrayBuilder._Add, [YExpression.Null]));
                continue;
            }

            if (item.Type == FastNodeType.SpreadElement)
            {
                var i = (item as AstSpreadElement).Argument;
                list.Add(YExpression.ElementInit(JSArrayBuilder._AddRange, [Visit(i)]));
                continue;
            }

            list.Add(YExpression.ElementInit(JSArrayBuilder._Add, [Visit(item)]));
        }

        if (list.Count > 0)
            return YExpression.ListInit(YExpression.New(JSArrayBuilder._New), list);

        return YExpression.New(JSArrayBuilder._New);
    }
}
