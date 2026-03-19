using System;

namespace Broiler.JavaScript.ExpressionCompiler.Expressions;

public abstract class YExpressionVisitor<T>: StackGuard<T, YExpression>
{
    public override T VisitIn(YExpression exp)
    {
        if (exp == null)
            return default;
        
        return exp.NodeType switch
        {
            YExpressionType.Block => VisitBlock(exp as YBlockExpression),
            YExpressionType.Call => VisitCall(exp as YCallExpression),
            YExpressionType.Binary => VisitBinary(exp as YBinaryExpression),
            YExpressionType.Constant => VisitConstant(exp as YConstantExpression),
            YExpressionType.Conditional => VisitConditional(exp as YConditionalExpression),
            YExpressionType.Assign => VisitAssign(exp as YAssignExpression),
            YExpressionType.Parameter => VisitParameter(exp as YParameterExpression),
            YExpressionType.New => VisitNew(exp as YNewExpression),
            YExpressionType.Field => VisitField(exp as YFieldExpression),
            YExpressionType.Property => VisitProperty(exp as YPropertyExpression),
            YExpressionType.NewArray => VisitNewArray(exp as YNewArrayExpression),
            YExpressionType.GoTo => VisitGoto(exp as YGoToExpression),
            YExpressionType.Return => VisitReturn(exp as YReturnExpression),
            YExpressionType.Loop => VisitLoop(exp as YLoopExpression),
            YExpressionType.Lambda => VisitLambda(exp as YLambdaExpression),
            YExpressionType.Label => VisitLabel(exp as YLabelExpression),
            YExpressionType.TypeAs => VisitTypeAs(exp as YTypeAsExpression),
            YExpressionType.TypeIs => VisitTypeIs(exp as YTypeIsExpression),
            YExpressionType.NewArrayBounds => VisitNewArrayBounds(exp as YNewArrayBoundsExpression),
            YExpressionType.ArrayIndex => VisitArrayIndex(exp as YArrayIndexExpression),
            YExpressionType.Index => VisitIndex(exp as YIndexExpression),
            YExpressionType.Coalesce => VisitCoalesce(exp as YCoalesceExpression),
            YExpressionType.Unary => VisitUnary(exp as YUnaryExpression),
            YExpressionType.ArrayLength => VisitArrayLength(exp as YArrayLengthExpression),
            YExpressionType.TryCatchFinally => VisitTryCatchFinally(exp as YTryCatchFinallyExpression),
            YExpressionType.Throw => VisitThrow(exp as YThrowExpression),
            YExpressionType.Convert => VisitConvert(exp as YConvertExpression),
            YExpressionType.Invoke => VisitInvoke(exp as YInvokeExpression),
            YExpressionType.Delegate => VisitDelegate(exp as YDelegateExpression),
            YExpressionType.MemberInit => VisitMemberInit(exp as YMemberInitExpression),
            //case YExpressionType.Relay:
            //    return VisitRelay(exp as YRelayExpression);
            YExpressionType.Empty => VisitEmpty(exp as YEmptyExpression),
            YExpressionType.Switch => VisitSwitch(exp as YSwitchExpression),
            YExpressionType.Yield => VisitYield(exp as YYieldExpression),
            YExpressionType.DebugInfo => VisitDebugInfo(exp as YDebugInfoExpression),
            YExpressionType.ILOffset => VisitILOffset(exp as YILOffsetExpression),
            YExpressionType.Box => VisitBox(exp as YBoxExpression),
            YExpressionType.Unbox => VisitUnbox(exp as YUnboxExpression),
            YExpressionType.JumpSwitch => VisitJumpSwitch(exp as YJumpSwitchExpression),
            YExpressionType.ListInit => VisitListInit(exp as YListInitExpression),
            YExpressionType.CoalesceCall => VisitCoalesceCall(exp as YCoalesceCallExpression),
            //case YExpressionType.TypeEqual:
            //    break;
            YExpressionType.Int32Constant => VisitInt32Constant(exp as YInt32ConstantExpression),
            YExpressionType.UInt32Constant => VisitUInt32Constant(exp as YUInt32ConstantExpression),
            YExpressionType.Int64Constant => VisitInt64Constant(exp as YInt64ConstantExpression),
            YExpressionType.UInt64Constant => VisitUInt64Constant(exp as YUInt64ConstantExpression),
            YExpressionType.DoubleConstant => VisitDoubleConstant(exp as YDoubleConstantExpression),
            YExpressionType.FloatConstant => VisitFloatConstant(exp as YFloatConstantExpression),
            YExpressionType.BooleanConstant => VisitBooleanConstant(exp as YBooleanConstantExpression),
            YExpressionType.StringConstant => VisitStringConstant(exp as YStringConstantExpression),
            YExpressionType.ByteConstant => VisitByteConstant(exp as YByteConstantExpression),
            YExpressionType.TypeConstant => VisitTypeConstant(exp as YTypeConstantExpression),
            YExpressionType.MethodConstant => VisitMethodConstant(exp as YMethodConstantExpression),
            YExpressionType.AddressOf => VisitAddressOf(exp as YAddressOfExpression),
            _ => throw new NotImplementedException($"{exp.NodeType}"),
        };
    }

    protected abstract T VisitAddressOf(YAddressOfExpression node);
    protected abstract T VisitMethodConstant(YMethodConstantExpression node);
    protected abstract T VisitTypeConstant(YTypeConstantExpression node);
    protected abstract T VisitByteConstant(YByteConstantExpression node);
    protected abstract T VisitStringConstant(YStringConstantExpression node);
    protected abstract T VisitBooleanConstant(YBooleanConstantExpression node);
    protected abstract T VisitFloatConstant(YFloatConstantExpression node);
    protected abstract T VisitDoubleConstant(YDoubleConstantExpression node);
    protected abstract T VisitUInt64Constant(YUInt64ConstantExpression node);
    protected abstract T VisitInt64Constant(YInt64ConstantExpression node);
    protected abstract T VisitUInt32Constant(YUInt32ConstantExpression node);
    protected abstract T VisitInt32Constant(YInt32ConstantExpression node);
    protected abstract T VisitCoalesceCall(YCoalesceCallExpression node);
    protected abstract T VisitListInit(YListInitExpression node);
    protected abstract T VisitJumpSwitch(YJumpSwitchExpression node);
    protected abstract T VisitUnbox(YUnboxExpression node);
    protected abstract T VisitBox(YBoxExpression node);
    protected abstract T VisitILOffset(YILOffsetExpression node);
    protected abstract T VisitDebugInfo(YDebugInfoExpression node);
    protected abstract T VisitYield(YYieldExpression node);
    protected abstract T VisitSwitch(YSwitchExpression node);
    protected abstract T VisitEmpty(YEmptyExpression exp);
    // protected abstract T VisitRelay(YRelayExpression yRelayExpression);
    protected abstract T VisitMemberInit(YMemberInitExpression memberInitExpression);
    protected abstract T VisitDelegate(YDelegateExpression yDelegateExpression);
    protected abstract T VisitInvoke(YInvokeExpression invokeExpression);
    protected abstract T VisitConvert(YConvertExpression convertExpression);
    protected abstract T VisitThrow(YThrowExpression throwExpression);
    protected abstract T VisitTryCatchFinally(YTryCatchFinallyExpression tryCatchFinallyExpression);
    protected abstract T VisitArrayLength(YArrayLengthExpression arrayLengthExpression);
    protected abstract T VisitUnary(YUnaryExpression yUnaryExpression);
    protected abstract T VisitCoalesce(YCoalesceExpression yCoalesceExpression);
    protected abstract T VisitIndex(YIndexExpression yIndexExpression);
    protected abstract T VisitArrayIndex(YArrayIndexExpression yArrayIndexExpression);
    protected abstract T VisitNewArrayBounds(YNewArrayBoundsExpression yNewArrayBoundsExpression);
    protected abstract T VisitTypeIs(YTypeIsExpression yTypeIsExpression);
    protected abstract T VisitTypeAs(YTypeAsExpression yTypeAsExpression);
    protected abstract T VisitLabel(YLabelExpression yLabelExpression);
    protected abstract T VisitLambda(YLambdaExpression yLambdaExpression);
    protected abstract T VisitLoop(YLoopExpression yLoopExpression);
    protected abstract T VisitReturn(YReturnExpression yReturnExpression);
    protected abstract T VisitGoto(YGoToExpression yGoToExpression);
    protected abstract T VisitNewArray(YNewArrayExpression yNewArrayExpression);
    protected abstract T VisitProperty(YPropertyExpression yPropertyExpression);
    protected abstract T VisitField(YFieldExpression yFieldExpression);
    protected abstract T VisitNew(YNewExpression yNewExpression);
    protected abstract T VisitCall(YCallExpression yCallExpression);
    protected abstract T VisitBlock(YBlockExpression yBlockExpression);
    protected abstract T VisitParameter(YParameterExpression yParameterExpression);
    protected abstract T VisitAssign(YAssignExpression yAssignExpression);
    protected abstract T VisitConditional(YConditionalExpression yConditionalExpression);
    protected abstract T VisitConstant(YConstantExpression yConstantExpression);
    protected abstract T VisitBinary(YBinaryExpression yBinaryExpression);
}
