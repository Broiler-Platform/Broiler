using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using System;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;
using Broiler.JavaScript.LinqExpressions.Utils;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    protected override YExpression VisitBinaryExpression(AstBinaryExpression binaryExpression)
    {
        var @operator = binaryExpression.Operator;

        if (@operator > TokenTypes.BeginAssignTokens && @operator < TokenTypes.EndAssignTokens)
            return VisitAssignmentExpression(binaryExpression.Left, @operator, binaryExpression.Right);

        var (isLeftString, isLeftNumber, left) = ToNativeExpression(binaryExpression.Left);
        var (isRightString, isRightNumber, right) = ToNativeExpression(binaryExpression.Right);

        switch (@operator)
        {
            case TokenTypes.Plus:
                if (isLeftNumber && isRightNumber)
                    return JSNumberBuilder.New(YExpression.Add(left, right));

                if (isLeftString && isRightString)
                    return JSStringBuilder.New(ClrStringBuilder.Concat(left, right));

                if (isRightNumber)
                    return JSValueBuilder.AddDouble(ToJSValueExpression(left), right);

                if (isRightString)
                    return JSValueBuilder.AddString(ToJSValueExpression(left), right);

                return JSValueBuilder.Add(ToJSValueExpression(left), ToJSValueExpression(right));

            case TokenTypes.Equal:
                if (isLeftNumber)
                {
                    // to do
                    // Add cocering...
                    if (isRightNumber)
                        return JSBooleanBuilder.NewFromCLRBoolean(YExpression.Equal(left, right));
                }

                if (isLeftString)
                {
                    if (isRightString)
                        return JSBooleanBuilder.NewFromCLRBoolean(ClrStringBuilder.Equal(left, right));
                }

                return JSValueBuilder.Equals(ToJSValueExpression(left), right);

            case TokenTypes.NotEqual:
                if (isLeftNumber)
                {
                    // to do
                    // Add cocering...
                    if (isRightNumber)
                        return JSBooleanBuilder.NewFromCLRBoolean(YExpression.NotEqual(left, right));
                }

                if (isLeftString)
                {
                    if (isRightString)
                        return JSBooleanBuilder.NewFromCLRBoolean(ClrStringBuilder.NotEqual(left, right));
                }

                return JSValueBuilder.NotEquals(ToJSValueExpression(left), right);

            case TokenTypes.StrictlyEqual:
                if (isLeftNumber)
                {
                    // to do
                    // Add cocering...
                    if (isRightNumber)
                        return JSBooleanBuilder.NewFromCLRBoolean(YExpression.Equal(left, right));
                }

                if (isLeftString)
                {
                    if (isRightString)
                        return JSBooleanBuilder.NewFromCLRBoolean(ClrStringBuilder.Equal(left, right));
                }

                return JSValueBuilder.StrictEquals(ToJSValueExpression(left), right);

            case TokenTypes.StrictlyNotEqual:
                if (isLeftNumber)
                {
                    // to do
                    // Add cocering...
                    if (isRightNumber)
                        return JSBooleanBuilder.NewFromCLRBoolean(YExpression.NotEqual(left, right));
                }

                if (isLeftString)
                {
                    if (isRightString)
                        return JSBooleanBuilder.NewFromCLRBoolean(ClrStringBuilder.NotEqual(left, right));
                }

                return JSValueBuilder.NotStrictEquals(ToJSValueExpression(left), right);
        }
        
        var be = BinaryOperation.Operation(ToJSValueExpression(left), ToJSValueExpression(right), @operator);
        return be ?? throw new FastParseException(binaryExpression.Start, $"Undefined binary operation {@operator}");
    }

    public static YExpression ToJSValueExpression(YExpression exp)
    {
        if (typeof(JSValue).IsAssignableFrom(exp.Type))
            return exp;

        if (exp.Type == typeof(string))
            return JSStringBuilder.New(exp);

        if (exp.Type == typeof(double))
            return JSNumberBuilder.New(exp);

        throw new NotImplementedException();
    }

    public (bool isString, bool isNumber, YExpression exp) ToNativeExpression(AstExpression ast)
    {
        if (ast.Type == FastNodeType.Literal && ast is AstLiteral a)
        {
            switch (a.TokenType)
            {
                case TokenTypes.String:
                    return (true, false, YExpression.Constant(a.StringValue));

                case TokenTypes.Number:
                    return (false, true, YExpression.Constant(a.NumericValue));
            }
        }
        return (false, false, Visit(ast));
    }
}
