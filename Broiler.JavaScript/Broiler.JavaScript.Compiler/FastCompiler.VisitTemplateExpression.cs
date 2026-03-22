using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Core.LinqExpressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Expression VisitTemplateExpression(AstTemplateExpression templateExpression)
    {
        var items = new Sequence<Exp>(templateExpression.Parts.Count);
        var e = templateExpression.Parts.GetFastEnumerator();
        int size = 0;

        while (e.MoveNext(out var item))
        {
            if (item.Type == FastNodeType.Literal)
            {
                var l = item as AstLiteral;
                var txt = l.TokenType == TokenTypes.TemplatePart ? l.Start.CookedText : l.StringValue;

                size += txt.Length;
                items.Add(Exp.Constant(txt));
            }
            else
            {
                items.Add(VisitExpression(item));
            }
        }

        return JSTemplateStringBuilder.New(items, size);
    }
}
