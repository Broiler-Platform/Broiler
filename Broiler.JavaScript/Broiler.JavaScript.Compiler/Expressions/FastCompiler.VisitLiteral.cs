
using Broiler.JavaScript.Ast.Misc;
using System;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitLiteral(AstLiteral literal)
    {
        switch (literal.TokenType)
        {
            case TokenTypes.True:
                return JSBooleanBuilder.True;

            case TokenTypes.False:
                return JSBooleanBuilder.False;

            case TokenTypes.String:
                return JSStringBuilder.New(YExpression.Constant(literal.StringValue));

            case TokenTypes.BigInt:
                return JSBigIntBuilder.New(literal.StringValue);

            case TokenTypes.Decimal:
                return JSDecimalBuilder.New(literal.StringValue);

            case TokenTypes.RegExLiteral:
                return JSRegExpBuilder.New(YExpression.Constant(literal.Regex.Pattern), YExpression.Constant(literal.Regex.Flags));
            
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

                return JSNumberBuilder.New(YExpression.Constant(n));
        }

        throw new NotImplementedException();
    }
}
