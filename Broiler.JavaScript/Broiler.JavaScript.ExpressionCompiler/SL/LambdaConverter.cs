using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.ExpressionCompiler.SL;

public class LambdaConverter : YExpressionVisitor<Expression>
{
    private Dictionary<YParameterExpression, ParameterExpression> cache = [];

    public (IFastEnumerable<ParameterExpression> pe, IDisposable disposable) Register(IFastEnumerable<YParameterExpression> plist)
    {
        if (plist == null)
        {
            return (null, null);
        }

        var pe = new Sequence<ParameterExpression>(plist.Count);
        var en = plist.GetFastEnumerator();
        while(en.MoveNext(out var e))
        {
            // var e = plist[i];
            var p = Expression.Parameter(e.Type, e.Name);
            // pe[i] = p;
            pe.Add(p);
            cache[e] = p;
        }

        var d = new DisposableAction(() => {
            var a = plist;
            var en = plist.GetFastEnumerator();
            while(en.MoveNext(out var item))
            {
                cache.Remove(item);
            }
        });

        return (pe, d);
    }

    protected override Expression VisitAddressOf(YAddressOfExpression node) => throw new NotImplementedException();

    protected override Expression VisitArrayIndex(YArrayIndexExpression yArrayIndexExpression) => Expression.ArrayIndex(Visit(yArrayIndexExpression.Target), Visit(yArrayIndexExpression.Index));

    protected override Expression VisitArrayLength(YArrayLengthExpression arrayLengthExpression) => Expression.ArrayLength(Visit(arrayLengthExpression.Target));

    protected override Expression VisitAssign(YAssignExpression yAssignExpression) => Expression.Assign(Visit(yAssignExpression.Left), Visit(yAssignExpression.Right));

    protected override Expression VisitBinary(YBinaryExpression yBinaryExpression)
    {
        var left = Visit(yBinaryExpression.Left);
        var right = Visit(yBinaryExpression.Right);
        return yBinaryExpression.Operator switch
        {
            YOperator.Add => Expression.Add(left, right),
            YOperator.Subtract => Expression.Subtract(left, right),
            YOperator.Multipley => Expression.Multiply(left, right),
            YOperator.Divide => Expression.Divide(left, right),
            YOperator.Mod => Expression.Modulo(left, right),
            YOperator.Power => Expression.Power(left, right),
            YOperator.Xor => Expression.ExclusiveOr(left, right),
            YOperator.BitwiseAnd => Expression.And(left, right),
            YOperator.BitwiseOr => Expression.Or(left, right),
            YOperator.BooleanAnd => Expression.AndAlso(left, right),
            YOperator.BooleanOr => Expression.OrElse(left, right),
            YOperator.Less => Expression.LessThan(left, right),
            YOperator.LessOrEqual => Expression.LessThanOrEqual(left, right),
            YOperator.Greater => Expression.GreaterThan(left, right),
            YOperator.GreaterOrEqual => Expression.GreaterThanOrEqual(left, right),
            YOperator.Equal => Expression.Equal(left, right),
            YOperator.NotEqual => Expression.NotEqual(left, right),
            YOperator.LeftShift => Expression.LeftShift(left, right),
            YOperator.RightShift => Expression.RightShift(left, right),
            YOperator.UnsignedRightShift => Expression.RightShift(Expression.Convert(left, typeof(uint)), right),
            _ => throw new NotImplementedException(),
        };
    }

    protected override Expression VisitBlock(YBlockExpression yBlockExpression)
    {
        var (list, d) = Register(yBlockExpression.Variables);
        using (d)
        {
            return Expression.Block(list, yBlockExpression.Expressions.Select(Visit));
        }
    }

    protected override Expression VisitBooleanConstant(YBooleanConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitBox(YBoxExpression node) => Expression.Convert(Visit(node.Target), typeof(object));

    protected override Expression VisitByteConstant(YByteConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitCall(YCallExpression yCallExpression) => Expression.Call(Visit(yCallExpression.Target), yCallExpression.Method, yCallExpression.Arguments.Select(Visit));

    protected override Expression VisitCoalesce(YCoalesceExpression yCoalesceExpression) => Expression.Coalesce(Visit(yCoalesceExpression.Left), Visit(yCoalesceExpression.Right));

    protected override Expression VisitCoalesceCall(YCoalesceCallExpression node) => throw new NotImplementedException();

    protected override Expression VisitConditional(YConditionalExpression yConditionalExpression) => Expression.Condition(
            Visit(yConditionalExpression.test),
            Visit(yConditionalExpression.@true),
            Visit(yConditionalExpression.@false));

    protected override Expression VisitConstant(YConstantExpression yConstantExpression) => Expression.Constant(yConstantExpression.Value);

    protected override Expression VisitConvert(YConvertExpression convertExpression) => Expression.Convert(Visit(convertExpression), convertExpression.Type);

    protected override Expression VisitDebugInfo(YDebugInfoExpression node) => Expression.Empty();

    protected override Expression VisitDelegate(YDelegateExpression yDelegateExpression) => throw new NotImplementedException();

    protected override Expression VisitDoubleConstant(YDoubleConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitEmpty(YEmptyExpression exp) => Expression.Empty();

    protected override Expression VisitField(YFieldExpression yFieldExpression) => Expression.Field(Visit(yFieldExpression.Target), yFieldExpression.FieldInfo);

    protected override Expression VisitFloatConstant(YFloatConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitGoto(YGoToExpression yGoToExpression) => throw new NotImplementedException();

    protected override Expression VisitILOffset(YILOffsetExpression node) => throw new NotImplementedException();

    protected override Expression VisitIndex(YIndexExpression yIndexExpression) => throw new NotImplementedException();

    protected override Expression VisitInt32Constant(YInt32ConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitInt64Constant(YInt64ConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitInvoke(YInvokeExpression invokeExpression) => throw new NotImplementedException();

    protected override Expression VisitJumpSwitch(YJumpSwitchExpression node) => throw new NotImplementedException();

    protected override Expression VisitLabel(YLabelExpression yLabelExpression) => throw new NotImplementedException();

    protected override Expression VisitLambda(YLambdaExpression yLambdaExpression) => throw new NotImplementedException();

    protected override Expression VisitListInit(YListInitExpression node) => throw new NotImplementedException();

    protected override Expression VisitLoop(YLoopExpression yLoopExpression) => throw new NotImplementedException();

    protected override Expression VisitMemberInit(YMemberInitExpression memberInitExpression) => throw new NotImplementedException();

    protected override Expression VisitMethodConstant(YMethodConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitNew(YNewExpression yNewExpression) => throw new NotImplementedException();

    protected override Expression VisitNewArray(YNewArrayExpression yNewArrayExpression) => throw new NotImplementedException();

    protected override Expression VisitNewArrayBounds(YNewArrayBoundsExpression yNewArrayBoundsExpression) => throw new NotImplementedException();

    protected override Expression VisitParameter(YParameterExpression yParameterExpression) => throw new NotImplementedException();

    protected override Expression VisitProperty(YPropertyExpression yPropertyExpression) => throw new NotImplementedException();

    protected override Expression VisitReturn(YReturnExpression yReturnExpression) => throw new NotImplementedException();

    protected override Expression VisitStringConstant(YStringConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitSwitch(YSwitchExpression node) => throw new NotImplementedException();

    protected override Expression VisitThrow(YThrowExpression throwExpression) => throw new NotImplementedException();

    protected override Expression VisitTryCatchFinally(YTryCatchFinallyExpression tryCatchFinallyExpression) => throw new NotImplementedException();

    protected override Expression VisitTypeAs(YTypeAsExpression yTypeAsExpression) => throw new NotImplementedException();

    protected override Expression VisitTypeConstant(YTypeConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitTypeIs(YTypeIsExpression yTypeIsExpression) => throw new NotImplementedException();

    protected override Expression VisitUInt32Constant(YUInt32ConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitUInt64Constant(YUInt64ConstantExpression node) => throw new NotImplementedException();

    protected override Expression VisitUnary(YUnaryExpression yUnaryExpression) => throw new NotImplementedException();

    protected override Expression VisitUnbox(YUnboxExpression node) => throw new NotImplementedException();

    protected override Expression VisitYield(YYieldExpression node) => throw new NotImplementedException();
}
