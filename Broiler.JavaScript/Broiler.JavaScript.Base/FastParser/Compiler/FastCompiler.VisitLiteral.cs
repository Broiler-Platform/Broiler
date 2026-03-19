using Broiler.JavaScript.Core.FastParser;
using Broiler.JavaScript.Core.FastParser.Ast;
using Broiler.JavaScript.Core.LinqExpressions;
using System;

using Exp = YantraJS.Expressions.YExpression;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.Core.FastParser.Compiler;

partial class FastCompiler
{
    protected override Expression VisitLiteral(AstLiteral literal)
    {
        switch (literal.TokenType)
        {
            case TokenTypes.True:
                return JSBooleanBuilder.True;

            case TokenTypes.False:
                return JSBooleanBuilder.False;

            case TokenTypes.String:
                return JSStringBuilder.New(Exp.Constant(literal.StringValue));

            case TokenTypes.BigInt:
                return JSBigIntBuilder.New(literal.StringValue);

            case TokenTypes.Decimal:
                return JSDecimalBuilder.New(literal.StringValue);

            case TokenTypes.RegExLiteral:
                return JSRegExpBuilder.New(Exp.Constant(literal.Regex.Pattern),Exp.Constant(literal.Regex.Flags));
            
            case TokenTypes.Null:
                return JSNullBuilder.Value;
            
            case TokenTypes.Number:
                var n = literal.NumericValue;

                if (double.IsNaN(n))
                    return JSNumberBuilder.NaN;

                if (n == 1)
                    return JSNumberBuilder.One;

                if (n == 2)
                    return JSNumberBuilder.Two;

                if (n == 0 && n != -0)
                    return JSNumberBuilder.Zero;

                return JSNumberBuilder.New(Exp.Constant(n));
        }

        throw new NotImplementedException();
    }
}
