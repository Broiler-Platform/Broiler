using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using System;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitMemberExpression(AstMemberExpression memberExpression)
    {
        var isSuper = memberExpression.Object?.Type == FastNodeType.Super;
        var target = isSuper ? scope.Top.ThisExpression : VisitExpression(memberExpression.Object);
        var super = isSuper ? scope.Top.Super : null;
        var mp = memberExpression.Property;

        switch (mp.Type)
        {
            case FastNodeType.Identifier:
                var id = mp as AstIdentifier;
                if (!memberExpression.Computed)
                    return JSValueBuilder.Index(target, super, KeyOfName(id.Name), memberExpression.Coalesce);

                return JSValueBuilder.Index(target, super, VisitIdentifier(id), memberExpression.Coalesce);

            case FastNodeType.Literal:
                var l = mp as AstLiteral;
                switch (l.TokenType)
                {
                    case TokenTypes.True:
                        return JSValueBuilder.Index(target, super, KeyOfName(l.StringValue), memberExpression.Coalesce);

                    case TokenTypes.False:
                        return JSValueBuilder.Index(target, super, KeyOfName(l.StringValue), memberExpression.Coalesce);

                    case TokenTypes.String:
                        var text = l.StringValue;
                        if (NumberParser.TryCoerceToUInt32(text, out var d))
                            return JSValueBuilder.Index(target, super, d, memberExpression.Coalesce);

                        return JSValueBuilder.Index(target, super, KeyOfName(text), memberExpression.Coalesce);

                    case TokenTypes.Number:
                        var number = l.NumericValue;
                        if (number >= 0 && number < uint.MaxValue && (number % 1) == 0)
                            return JSValueBuilder.Index(target, super, (uint)l.NumericValue, memberExpression.Coalesce);

                        return JSValueBuilder.Index(target, super, KeyOfName(l.NumericValue.ToString()), memberExpression.Coalesce);

                    default:
                        throw new NotImplementedException();
                }

            case FastNodeType.MemberExpression:
                var se = mp as AstMemberExpression;
                return JSValueBuilder.Index(target, super, VisitExpression(se), memberExpression.Coalesce);
        }

        if (memberExpression.Computed)
            return JSValueBuilder.Index(target, super, VisitExpression(memberExpression.Property), memberExpression.Coalesce);

        throw new NotImplementedException();
    }
}
