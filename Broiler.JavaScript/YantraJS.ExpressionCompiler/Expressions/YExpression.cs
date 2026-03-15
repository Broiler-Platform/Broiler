#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using YantraJS.Core;

namespace YantraJS.Expressions;


/// <summary>
/// System.Linq.Expressions.Expression is very complex and it allows
/// various complex operations such as += etc.
/// 
/// We need simpler operations to build IL easily without automatically
/// assuming or supporting nullability etc.
/// 
/// Simple IL Generator does not allow += operators etc. It does not 
/// allow Nullable types as well. Expression creator must take care of it.
/// </summary>
public abstract class YExpression(YExpressionType nodeType, Type type)
{
    public readonly YExpressionType NodeType = nodeType;

    public readonly Type Type = type;

    public static YEmptyExpression Empty = new();

    public static YILOffsetExpression ILOffset = new();

    public static YExpression operator +(YExpression left, YExpression right) => YExpression.Binary(left, YOperator.Add, right);
    public static YExpression operator -(YExpression left, YExpression right) => YExpression.Binary(left, YOperator.Subtract, right);

    public static YExpression Break(YLabelTarget @break) => new YGoToExpression(@break, null);

    public static YConditionalExpression IfThen(YExpression test, YExpression @true, YExpression? @false = null) => new(test, @true, @false);

    public static YExpression operator +(YExpression left, object right) => YExpression.Binary(left, YOperator.Add, YExpression.Constant(right));
    public static YExpression operator -(YExpression left, object right) => YExpression.Binary(left, YOperator.Subtract, YExpression.Constant(right));

    public static YExpression operator +(YExpression left, int right) => YExpression.Binary(left, YOperator.Add, YExpression.Constant(right));
    public static YExpression operator -(YExpression left, int right) => YExpression.Binary(left, YOperator.Subtract, YExpression.Constant(right));


    public static YExpression operator >(YExpression left, YExpression right) => YExpression.Binary(left, YOperator.Greater, right);
    public static YExpression operator <(YExpression left, YExpression right) => YExpression.Binary(left, YOperator.Less, right);


    public static YExpression operator >=(YExpression left, object right) => YExpression.Binary(left, YOperator.GreaterOrEqual, Constant(right));
    public static YExpression operator <=(YExpression left, object right) => YExpression.Binary(left, YOperator.LessOrEqual, YExpression.Constant(right));

    public static YExpression Throw(YExpression yNewExpression, Type type) => new YThrowExpression(yNewExpression, type);

    public static YExpression operator >(YExpression left, object right) => YExpression.Binary(left, YOperator.Greater, Constant(right));
    public static YExpression operator <(YExpression left, object right) => YExpression.Binary(left, YOperator.Less, Constant(right));


    public static YExpression operator >=(YExpression left, YExpression right) => YExpression.Binary(left, YOperator.GreaterOrEqual, right);
    public static YExpression operator <=(YExpression left, YExpression right) => YExpression.Binary(left, YOperator.LessOrEqual, right);

    public abstract void Print(IndentedTextWriter writer);

    public string DebugView => ToString();

    public override string ToString()
    {
        using (var sw = new StringWriter())
        {
            using (var iw = new IndentedTextWriter(sw))
            {
                Print(iw);
                return sw.ToString();
            }
        }
    }

    public static YBinaryExpression Binary(YExpression left, YOperator @operator, YExpression right) => new(left, @operator, right);

    public static YCoalesceExpression Coalesce(YExpression left, YExpression right) => new(left, right);

    /// <summary>
    /// This works in following fashion...
    /// 
    /// var returnValue = if(!target.Member) ? target : target.Call(method, arguments);
    /// 
    /// Here target is not read again, it is only read once and it's value is duplicated.
    /// 
    /// It is equivalent to 
    /// 
    /// var targetValue  = target?.Call(method, arguments);
    /// 
    /// if member is null. For JavaScript, we can introduce null and undefined check using a field or property check
    /// </summary>
    /// <param name="target"></param>
    /// <param name="test"></param>
    /// <param name="testArgs"></param>
    /// <param name="true"></param>
    /// <param name="trueArguments"></param>
    /// <param name="false"></param>
    /// <param name="falseArguments"></param>
    /// <returns></returns>
    public static YCoalesceCallExpression CoalesceCall(
        YExpression target,
        MemberInfo test,
        IFastEnumerable<YExpression> testArgs,
        MethodInfo @true,
        IFastEnumerable<YExpression> trueArguments,
        MethodInfo @false,
        IFastEnumerable<YExpression> falseArguments) => new(
            target,
            test, testArgs,
            @true,
            trueArguments,
            @false,
            falseArguments);



    //public static YCoalesceCallExpression CoalesceCall(
    //    YExpression target,
    //    MemberInfo test,
    //    MethodInfo @true,
    //    IFastEnumerable<YExpression> arguments)
    //{
    //    return new YCoalesceCallExpression(target, test, Sequence<YExpression>.Empty, @true, arguments);
    //}

    //public static YCoalesceCallExpression CoalesceCall(
    //    YExpression target,
    //    MemberInfo test,
    //    IFastEnumerable<YExpression> testArgs,
    //    MethodInfo @true,
    //    IFastEnumerable<YExpression> arguments)
    //{
    //    return new YCoalesceCallExpression(target, test, testArgs, @true, arguments);
    //}

    public static YDebugInfoExpression DebugInfo(in Position start, in Position end) => new(start, end);

    public static YExpression Add(YExpression left, YExpression right) => new YBinaryExpression(left, YOperator.Add, right);

    public static YExpression Subtract(YExpression left, YExpression right) => new YBinaryExpression(left, YOperator.Subtract, right);

    public static YExpression Multiply(YExpression left, YExpression right) => new YBinaryExpression(left, YOperator.Multipley, right);

    public static YExpression Divide(YExpression left, YExpression right) => new YBinaryExpression(left, YOperator.Divide, right);

    public static YExpression Modulo(YExpression left, YExpression right) => new YBinaryExpression(left, YOperator.Mod, right);
    public static YExpression And(YExpression left, YExpression right) => new YBinaryExpression(left, YOperator.BitwiseAnd, right);
    public static YExpression ExclusiveOr(YExpression left, YExpression right) => new YBinaryExpression(left, YOperator.Xor, right);

    public static YExpression LeftShift(YExpression left, YExpression right) => new YBinaryExpression(left, YOperator.LeftShift, right);
    public static YExpression RightShift(YExpression left, YExpression right) => new YBinaryExpression(left, YOperator.RightShift, right);

    public static YExpression UnsignedRightShift(YExpression left, YExpression right) => new YBinaryExpression(left, YOperator.UnsignedRightShift, right);

    public static YExpression Power(YExpression left, YExpression right)
    {
        //return new YBinaryExpression(left, YOperator.Power, right);
        var m = typeof(Math).GetMethod(nameof(Math.Pow));
        // return YExpression.Binary(Visit(node.Left), YOperator.Power, Visit(node.Right));

        left = left.Type == typeof(double) ? left : YExpression.Convert(left, typeof(double));
        right = right.Type == typeof(double) ? right : YExpression.Convert(right, typeof(double));
        return YExpression.Call(null, m, left, right);
    }

    public static YBoxExpression Box(YExpression target) => new(target);

    public static YInt32ConstantExpression Constant(int value) => YInt32ConstantExpression.For(value);

    public static YUInt32ConstantExpression Constant(uint value) => YUInt32ConstantExpression.For(value);

    public static YInt64ConstantExpression Constant(long value) => new(value);

    public static YUInt64ConstantExpression Constant(ulong value) => new(value);


    public static YBooleanConstantExpression Constant(bool value) => value 
        ? YBooleanConstantExpression.True
        : YBooleanConstantExpression.False;

    public static YExpression Constant(string value) => value == null
        ? new YConstantExpression(null, typeof(string))
        : new YStringConstantExpression(value);

    public static YDoubleConstantExpression Constant(double value) => new(value);

    public static YFloatConstantExpression Constant(float value) => new(value);

    public static YByteConstantExpression Constant(byte value) => new(value);

    public static YTypeConstantExpression Constant(Type value) => new(value);

    public static YMethodConstantExpression Constant(MethodInfo value) => new(value);

    public static YInt32ConstantExpression Constant(Enum value) => new(System.Convert.ToInt32(value));

    public static YExpression Constant(object value, Type? type = null)
    {
        if (value is YConstantExpression)
            throw new NotSupportedException();
        if (value is string @string)
            return new YStringConstantExpression(@string);
        return new YConstantExpression(value, type ?? value?.GetType() ?? typeof(object));
    }

    public static YExpression MakeIndex(YExpression target, PropertyInfo index, params YExpression[] args) => Index(target, index, args);

    public static YConditionalExpression Conditional(
        YExpression test,
        YExpression @true,
        YExpression @false,
        Type? type = null) => new(test, @true, @false, type);

    public static YAssignExpression Assign(YExpression left, YExpression right, Type? type = null) => new(left, right, type);

    public static YParameterExpression Parameter(Type type, string? name = null) => new(type, name);

    public static YParameterExpression[] Parameters(params Type[] types)
    {
        var pl = new YParameterExpression[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            pl[i] = new YParameterExpression(types[i], null);
        }
        return pl;
    }

    public static YMemberInitExpression MemberInit(
        YNewExpression exp,
        IFastEnumerable<YBinding> list) => new(exp, list);


    public static YMemberInitExpression MemberInit(
        YNewExpression exp,
        IEnumerable<YBinding> list) => new(exp, list.AsSequence());

    public static YMemberAssignment Bind(MemberInfo field, YExpression value) => new(field, value);

    public static YMemberInitExpression MemberInit(
        YNewExpression exp,
        params YBinding[] list) => new(exp, list.AsSequence());

    //public static YMemberAssignment Bind(MemberInfo field, YExpression value)
    //{
    //    return new YMemberAssignment(field, value);
    //}

    public static YBlockExpression Block(
        IFastEnumerable<YParameterExpression>? variables,
        params YExpression[] expressions) => new(variables, expressions.AsSequence());

    public static YBlockExpression Block(
        IFastEnumerable<YParameterExpression>? variables,
        IFastEnumerable<YExpression> expressions) => new(variables, expressions);

    public static YExpression Block(IFastEnumerable<YExpression> expressions)
    {
        if (expressions.Count == 1)
            return expressions.First();
        return new YBlockExpression(null, expressions);
    }

    public static YBlockExpression Block(params YExpression[] expressions) => new(null, expressions.AsSequence());


    public static YExpression Convert(YExpression exp, Type type, bool cast = false)
    {
        if (YConvertExpression.TryGetConversionMethod(exp.Type, type, out var method))
        {
            if (method == null)
                return new YTypeAsExpression(exp, type);
            return new YConvertExpression(exp, type, method);
        }
        if (exp.Type.IsValueType && type == typeof(object))
            return Box(exp);
        return new YTypeAsExpression(exp, type);
    }

    //public static YConvertExpression Convert(YExpression exp, Type type, MethodInfo method)
    //{
    //    return new YConvertExpression(exp, type, method);
    //}

    public static YExpression Continue(YLabelTarget @break) => new YGoToExpression(@break, null);

    public static YDelegateExpression Delegate(MethodInfo method, Type? type = null) => new(method, type);


    public static YBinaryExpression Equal(YExpression left, YExpression right)
         => YExpression.Binary(left, YOperator.Equal, right);

    internal static YNewExpression CallNew(
        ConstructorInfo constructor, params YExpression[] args) => new(constructor, args.AsSequence(), true);

    public static YBinaryExpression Or(YExpression left, YExpression right)
        => YExpression.Binary(left, YOperator.BitwiseOr, right);

    public static YBinaryExpression OrElse(YExpression left, YExpression right)
        => YExpression.Binary(left, YOperator.BooleanOr, right);

    public static YBinaryExpression NotEqual(YExpression left, YExpression right)
         => YExpression.Binary(left, YOperator.NotEqual, right);

    public static YBinaryExpression Greater(YExpression left, YExpression right)
         => YExpression.Binary(left, YOperator.Greater, right);


    public static YJumpSwitchExpression JumpSwitch(YExpression target, IFastEnumerable<YLabelTarget> cases)
        => new(target, cases);

    public static YLambdaExpression Lambda(
        Type type,
        YExpression body,
        in FunctionName name, YParameterExpression[] parameters) => new(type, name, body, null, parameters);

    public static YBinaryExpression GreaterOrEqual(YExpression left, YExpression right)
         => YExpression.Binary(left, YOperator.GreaterOrEqual, right);

    public static YBinaryExpression Less(YExpression left, YExpression right)
         => YExpression.Binary(left, YOperator.Less, right);

    public static YBinaryExpression LessOrEqual(YExpression left, YExpression right)
         => YExpression.Binary(left, YOperator.LessOrEqual, right);

    public static YCallExpression Call(YExpression? target, MethodInfo method, IFastEnumerable<YExpression> args) => new(target, method, args);


    public static YCallExpression Call(YExpression? target, MethodInfo method, IEnumerable<YExpression> args) => new(target, method, args.AsSequence());
    public static YCallExpression Call(YExpression? target, MethodInfo method, params YExpression[] args) => new(target, method, args.AsSequence());

    public static YNewExpression New(ConstructorInfo constructor, IFastEnumerable<YExpression> args) => new(constructor, args);


    public static YNewExpression New(ConstructorInfo constructor, IEnumerable<YExpression> args) => new(constructor, args.AsSequence());
    public static YNewExpression New(Type type, params YExpression[] args)
    {
        var constructor = type.GetConstructor(args.Select(x => x.Type).ToArray());
        return new YNewExpression(constructor, args.AsSequence());
    }
    public static YNewExpression New(ConstructorInfo constructor, params YExpression[] args) => New(constructor, (IList<YExpression>)args);

    public static YFieldExpression Field(YExpression target, FieldInfo field) => new(target, field);

    public static YFieldExpression Field(YExpression target, string name)
    {
        var field = target.Type.GetUnderlyingTypeIfRef().GetField(name);

        return new YFieldExpression(target, field);
    }

    public static YInvokeExpression Invoke(YExpression target, IFastEnumerable<YExpression> args)
    {
        var t = target.Type;
        var type = t.GetMethod("Invoke").ReturnType;
        return new YInvokeExpression(target, args, type);
    }


    public static YInvokeExpression Invoke(YExpression target, params YExpression[] args)
    {
        var t = target.Type;
        var type = t.GetMethod("Invoke").ReturnType;
        return new YInvokeExpression(target, args.AsSequence(), type);
    }

    public static YParameterExpression Variable(Type type, string? name = null) => new(type, name);

    public static YPropertyExpression Property(YExpression target, PropertyInfo field) => new(target, field);

    public static YNewArrayExpression NewArray(Type type, IFastEnumerable<YExpression> elements) => new(type, elements);


    public static YNewArrayExpression NewArray(Type type, params YExpression[] elements) => new(type, elements.AsSequence());

    public static YNewArrayExpression NewArrayInit(Type type, IEnumerable<YExpression> elements) => new(type, elements.AsSequence());


    public static YNewArrayExpression NewArrayInit(Type type, IFastEnumerable<YExpression> elements) => new(type, elements);

    public static YNewArrayBoundsExpression NewArrayBounds(Type type, YExpression size) => new(type, size);

    public static YMemberElementInit ListBind(MemberInfo member, YElementInit[] elements) => new(member, elements);

    public static YExpression ListInit(YNewExpression newExp, IFastEnumerable<YElementInit> elements)
        => new YListInitExpression(newExp, elements);

    public static YExpression ListInit(YNewExpression newExp, IEnumerable<YElementInit> elements)
        => new YListInitExpression(newExp, elements.AsSequence());

    [Obsolete("Use Sequence<T>")]
    public static YExpression ListInit(YNewExpression newExp, YElementInit[] elements)
        => new YListInitExpression(newExp, elements.AsSequence());

    public static YElementInit ElementInit(MethodInfo addMethod, params YExpression[] arguments)
        => new(addMethod, arguments);

    public static YLabelTarget Label(Type type, string? name = null) => new(name, type ?? typeof(void));

    public static YLabelTarget Label(string? name = null,
        Type? type = null) => new(name, type ?? typeof(void));

    public static YLabelExpression Label(YLabelTarget target, YExpression? defaultValue = null) => new(target, defaultValue);

    public static YExpression Condition(YExpression yExpression, YExpression def, YExpression target, Type? type = null) => Conditional(yExpression, def, target, type);

    public static YConstantExpression Null = new(null, typeof(object));

    public static YGoToExpression Goto(YLabelTarget target, YExpression? defaultValue = null) => new(target, defaultValue);

    public static YGoToExpression GoTo(YLabelTarget target, YExpression? defaultValue = null) => new(target, defaultValue);

    public static YReturnExpression Return(YLabelTarget target, YExpression? defaultValue = null) => new(target, defaultValue);

    public static YLoopExpression Loop(YExpression body, YLabelTarget @break, YLabelTarget? @continue = null) => new(body, @break, @continue ?? Label("continue", @break.LabelType));

    public static YExpression<T> Lambda<T>(in FunctionName name, YExpression body, params YParameterExpression[] parameters) => new(name, body, null, parameters, null);

    public static YExpression<T> InstanceLambda<T>(
        in FunctionName name,
        YExpression body,
        YParameterExpression @this,
        YParameterExpression[] parameters) => new(name, body, @this, parameters, null);


    public static YLambdaExpression Lambda(
        Type delegateType,
        in FunctionName name,
        YExpression body,
        YParameterExpression[] parameters) => new(delegateType, name, body, null, parameters, body.Type);

    public static YTypeAsExpression TypeAs(YExpression target, Type type) => new(target, type);

    public static YTypeIsExpression TypeIs(YExpression target, Type type) => new(target, type);

    public static YUnboxExpression Unbox(YExpression target, Type type) => new(target, type);

    public static YCatchBody Catch(YParameterExpression parameter, YExpression body) => new(parameter, body);
    public static YCatchBody Catch(YExpression body) => new(null, body);


    public static YTryCatchFinallyExpression TryCatch(
        YExpression @try,
        YCatchBody @catch) => new(@try, @catch, null);

    public static YTryCatchFinallyExpression TryFinally(
        YExpression @try,
        YExpression @finally) => new(@try, null, @finally);

    public static YTryCatchFinallyExpression TryCatchFinally(
        YExpression @try,
        YExpression @finally,
        YCatchBody? catchBody)
    {
        if (catchBody == null && @finally == null)
            throw new ArgumentNullException($"Both finally and catch cannot be null");
        return new YTryCatchFinallyExpression(@try, catchBody, @finally);
    }

    public static YTryCatchFinallyExpression TryCatchFinally(
        YExpression @try,
        YCatchBody? catchBody,
        YExpression? @finally = null)
    {
        if (catchBody == null && @finally == null)
            throw new ArgumentNullException($"Both finally and catch cannot be null");
        return new YTryCatchFinallyExpression(@try, catchBody, @finally);
    }

    public static YArrayIndexExpression ArrayIndex(YExpression target, YExpression index) => new(target, index);

    public static YArrayLengthExpression ArrayLength(YExpression target) => new(target);

    public static YIndexExpression Index(YExpression target, PropertyInfo propertyInfo, IFastEnumerable<YExpression> args) => new(target, propertyInfo, args);


    public static YIndexExpression Index(YExpression target, PropertyInfo propertyInfo, params YExpression[] args) => new(target, propertyInfo, args.AsSequence());

    public static YIndexExpression Index(YExpression target, IFastEnumerable<YExpression> args)
    {
        var types = args.Select(x => x.Type).ToArray();
        PropertyInfo propertyInfo =
            target.Type.GetType()
                .GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.GetProperty)
                .FirstOrDefault(x => x.GetIndexParameters().Select(p => p.ParameterType).SequenceEqual(types));
        if (propertyInfo == null)
        {
            throw new NotSupportedException($"Index[{string.Join(",", types.Select(n => n.Name))}] not found on {target.Type.GetFriendlyName()}");
        }
        return new YIndexExpression(target, propertyInfo, args);
    }

    public static YIndexExpression Index(YExpression target, params YExpression[] args)
    {
        var types = args.Select(x => x.Type).ToArray();
        PropertyInfo propertyInfo =
            target.Type.GetType()
                .GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.GetProperty)
                .FirstOrDefault(x => x.GetIndexParameters().Select(p => p.ParameterType).SequenceEqual(types));
        if (propertyInfo == null)
        {
            throw new NotSupportedException($"Index[{string.Join(",", types.Select(n => n.Name))}] not found on {target.Type.GetFriendlyName()}");
        }
        return new YIndexExpression(target, propertyInfo, args.AsSequence());
    }

    public static YUnaryExpression Not(YExpression exp) => new(exp, YUnaryOperator.Not);

    public static YUnaryExpression Negative(YExpression exp) => new(exp, YUnaryOperator.Negative);

    public static YUnaryExpression OnesComplement(YExpression exp) => new(exp, YUnaryOperator.OnesComplement);
    public static YUnaryExpression Negate(YExpression exp) => new(exp, YUnaryOperator.Negative);
    public static YExpression UnaryPlus(YExpression exp) => exp;
    public static YTypeIsExpression TypeEqual(YExpression exp, Type type) => new(exp, type);

    public static YThrowExpression Throw(YExpression exp) => new(exp);
    internal static YLambdaExpression InlineLambda(
        Type delegateType,
        in FunctionName name,
        YExpression body,
        YParameterExpression @this,
        YParameterExpression[] parameters,
        YExpression? repository,
        Type returnType) => new(delegateType, name, body, @this, parameters, returnType, repository);

    internal static YLambdaExpression InlineLambda(
        Type delegateType,
        in FunctionName name,
        YExpression body,
        List<YParameterExpression> parameters,
        YExpression? repository) => new(delegateType, name, body, null, parameters.ToArray(), null, repository);

    //internal static YRelayExpression Relay(
    //    IFastEnumerable<YExpression> box,
    //    YLambdaExpression inner)
    //{
    //    return new YRelayExpression(box, inner);
    //}

    public static YSwitchExpression Switch(
        YExpression target,
        params YSwitchCaseExpression[] cases) => new(target, null, null, cases);

    public static YSwitchExpression Switch(
        YExpression target,
        YExpression? defaultBody,
        params YSwitchCaseExpression[] cases) => new(target, null, defaultBody, cases);

    public static YSwitchExpression Switch(
        YExpression target,
        YExpression? defaultBody,
        MethodInfo method,
        IEnumerable<YSwitchCaseExpression> cases) => new(target, method, defaultBody, cases.ToArray());


    public static YSwitchCaseExpression SwitchCase(YExpression body, params YExpression[] testValues) => new(body, testValues);

    public static YSwitchCaseExpression SwitchCase(YExpression body, IEnumerable<YExpression> testValues) => new(body, testValues.ToArray());


    public static YYieldExpression Yield(YExpression arg, bool @delegate = false) => new(arg, @delegate);
}
