using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;
using System;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Compiler;

partial class FastCompiler
{
    private static YExpression DoubleValue(YExpression exp) => JSValueBuilder.DoubleValue(exp);

    private YExpression DoubleValue(AstExpression exp) => JSValueBuilder.DoubleValue(VisitExpression(exp));

    private YExpression BooleanValue(AstExpression exp) => JSValueBuilder.BooleanValue(VisitExpression(exp));

    protected override YExpression VisitUnaryExpression(AstUnaryExpression unaryExpression)
    {
        var target = unaryExpression.Argument;

        switch (unaryExpression.Operator)
        {
            case UnaryOperator.Plus:
                return JSNumberBuilder.New(YExpression.UnaryPlus(DoubleValue(target)));

            case UnaryOperator.Minus:
                if (target.Type == FastNodeType.Literal)
                {
                    AstLiteral l = unaryExpression.Argument as AstLiteral;

                    if (l.TokenType == TokenTypes.Number)
                        return JSNumberBuilder.New(YExpression.Constant(-l.NumericValue));

                    if (l.TokenType == TokenTypes.BigInt)
                        return JSBigIntBuilder.New("-" + l.StringValue);

                    if (l.TokenType == TokenTypes.String)
                        return JSNumberBuilder.New(YExpression.Negate(DoubleValue(target)));
                }

                return JSValueBuilder.Negate(Visit(target));

            case UnaryOperator.BitwiseNot:
                return JSNumberBuilder.New(YExpression.OnesComplement(JSValueBuilder.IntValue(Visit(target))));

            case UnaryOperator.Negate:
                return YExpression.Condition(BooleanValue(target), JSBooleanBuilder.False, JSBooleanBuilder.True);

            case UnaryOperator.delete:
                // delete expression...
                switch (target.Type)
                {
                    case FastNodeType.Literal:
                        return JSBooleanBuilder.True;

                    case FastNodeType.Identifier:
                        var id = target as AstIdentifier;
                        if (id.Name == "this")
                            return JSBooleanBuilder.True;

                        return JSExceptionBuilder.ThrowSyntaxError("Cannot delete a variable in Strict Mode");

                    case FastNodeType.MemberExpression:
                        break;

                    default:
                        return YExpression.Block(Visit(target), JSBooleanBuilder.True);
                }

                var me = target as AstMemberExpression;
                var targetObj = VisitExpression(me.Object);

                if (me.Computed)
                {
                    YExpression pe = VisitExpression(me.Property);
                    return JSValueBuilder.Delete(targetObj, pe);
                }
                else
                {
                    var mep = me.Property;
                    switch (mep.Type)
                    {
                        case FastNodeType.Literal:
                            AstLiteral l = mep as AstLiteral;
                            if (l.TokenType == TokenTypes.Number)
                                return JSValueBuilder.Delete(targetObj, YExpression.Constant((uint)l.NumericValue));
                            if (l.TokenType == TokenTypes.String)
                                return JSValueBuilder.Delete(targetObj, KeyOfName(l.StringValue));
                            break;

                        case FastNodeType.Identifier:
                            AstIdentifier id = mep as AstIdentifier;
                            return JSValueBuilder.Delete(targetObj, KeyOfName(id.Name));
                    }
                }
                break;

            case UnaryOperator.@void:
                if (target != null && target.Type != FastNodeType.Literal)
                    return YExpression.Condition(YExpression.Equal(YExpression.Null, Visit(target)), JSUndefinedBuilder.Value, JSUndefinedBuilder.Value);

                return JSUndefinedBuilder.Value;

            case UnaryOperator.@typeof:
                return JSValueBuilder.TypeOf(VisitExpression(target));

            case UnaryOperator.Increment:
                return InternalVisitUpdateExpression(unaryExpression);

            case UnaryOperator.Decrement:
                return InternalVisitUpdateExpression(unaryExpression);
        }

        throw new InvalidOperationException();
    }
}
