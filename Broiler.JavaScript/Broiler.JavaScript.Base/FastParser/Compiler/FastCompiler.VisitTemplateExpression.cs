using Broiler.JavaScript.Core.FastParser;
using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.Core.LinqExpressions;
using Exp = YantraJS.Expressions.YExpression;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

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
