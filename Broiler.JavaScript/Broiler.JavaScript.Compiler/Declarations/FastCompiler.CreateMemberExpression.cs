using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using System;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;


namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    private YExpression CreateMemberExpression(YExpression target, AstExpression property, bool computed)
    {
        switch (property.Type)
        {
            case FastNodeType.Identifier:
                var id = property as AstIdentifier;
                if (!computed)
                    return JSValueBuilder.Index(target, KeyOfName(id.Name));

                return JSValueBuilder.Index(target, VisitIdentifier(id));

            case FastNodeType.Literal:
                var l = property as AstLiteral;
                switch (l.TokenType)
                {
                    case TokenTypes.True:
                        return JSValueBuilder.Index(target, 1);

                    case TokenTypes.False:
                        return JSValueBuilder.Index(target, 0);

                    case TokenTypes.String:
                        return JSValueBuilder.Index(target, KeyOfName(l.Start.CookedText));

                    case TokenTypes.Number:
                        if (l.NumericValue >= 0 && (l.NumericValue % 1 == 0))
                            return JSValueBuilder.Index(target, (uint)l.NumericValue);

                        break;
                }
                break;

            case FastNodeType.MemberExpression:
                var se = property as AstMemberExpression;
                return JSValueBuilder.Index(target, Visit(se.Property));
        }

        if (computed)
            return JSValueBuilder.Index(target, Visit(property));

        throw new NotImplementedException();
    }
}
