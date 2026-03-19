using Broiler.JavaScript.Core.FastParser;
using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.Core.LinqExpressions;
using System;
using Exp = YantraJS.Expressions.YExpression;


namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    private Exp CreateMemberExpression(Exp target, AstExpression property, bool computed)
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
