using System;
using System.Collections.Generic;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;


public class JSValueBuilder
{
    private static readonly Type type = typeof(JSValue);

    public static Expression AddString(Expression target, Expression @string) => target.CallExpression<JSValue, string, JSValue>(() => (x, a) => x.AddValue(a), @string);

    public static Expression AddDouble(Expression target, Expression @double) => target.CallExpression<JSValue, double, JSValue>(() => (x, a) => x.AddValue(a), @double);

    public static Expression ToKey(Expression exp) => exp.CallExpression<JSValue, PropertyKey>(() => (x) => x.ToKey(true), Expression.Constant(true));

    public static Expression IsNumber(Expression exp) => exp.PropertyExpression<JSValue, bool>(() => (x) => x.IsNumber);

    public static Expression IsString(Expression exp) => exp.PropertyExpression<JSValue, bool>(() => (x) => x.IsString);

    public static Expression IsBoolean(Expression exp) => exp.PropertyExpression<JSValue, bool>(() => (x) => x.IsBoolean);

    public static Expression IsSymbol(Expression exp) => exp.PropertyExpression<JSValue, bool>(() => (x) => x.IsSymbol);

    public static Expression IsFunction(Expression exp) => exp.PropertyExpression<JSValue, bool>(() => (x) => x.IsFunction);

    public static Expression IsObjectType(Expression exp) =>
        Expression.And(exp.PropertyExpression<JSValue, bool>(() => (x) => x.IsObject), Expression.Not(exp.PropertyExpression<JSValue, bool>(() => (x) => x.IsFunction)));

    public static Expression IsNullOrUndefined(Expression target)
    {
        if (target.Type == typeof(JSVariable))
            target = JSVariable.ValueExpression(target);

        return target.PropertyExpression<JSValue, bool>(() => (x) => x.IsNullOrUndefined);
    }

    private static PropertyInfo _lengthProperty = type.Property(nameof(JSValue.Length));

    public static Expression Length(Expression target) => Expression.Property(target, _lengthProperty);

    public static Expression DoubleValue(Expression exp) => exp.PropertyExpression<JSValue, double>(() => (x) => x.DoubleValue);

    public static Expression IntValue(Expression exp) => exp.PropertyExpression<JSValue, int>(() => (x) => x.IntValue);

    public static Expression BigIntValue(Expression exp) => exp.PropertyExpression<JSValue, long>(() => (x) => x.BigIntValue);

    public static Expression UIntValue(Expression exp) => exp.PropertyExpression<JSValue, uint>(() => (x) => x.UIntValue);

    public static Expression PrototypeChain(Expression exp) =>
        exp.FieldExpression<JSValue, IJSPrototype>(() => (x) => x.prototypeChain).PropertyExpression<IJSPrototype, JSValue>(() => (x) => x.Object);

    public static Expression Negate(Expression exp) => exp.CallExpression<JSValue, JSValue>(() => (x) => x.Negate());

    public static Expression Power(Expression left, Expression right) => left.CallExpression<JSValue, JSValue, JSValue>(() => (x, a) => x.Power(a), right);

    public static Expression BooleanValue(Expression exp)
    {
        if (exp.NodeType == YExpressionType.Conditional && exp is YConditionalExpression ce)
        {
            if (ce.@true == JSBooleanBuilder.True && ce.@false == JSBooleanBuilder.False)
                return ce.test;

            if (ce.@true == JSBooleanBuilder.False && ce.@false == JSBooleanBuilder.True)
                return Expression.Not(ce.test);
        }

        if (exp == JSBooleanBuilder.True)
            return Expression.Constant(true);

        if (exp == JSBooleanBuilder.False)
            return Expression.Constant(false);

        return exp.PropertyExpression<JSValue, bool>(() => (x) => x.BooleanValue);
    }

    public static Expression Add(Expression target, Expression value) => target.CallExpression<JSValue, JSValue, JSValue>(() => (x, a) => x.AddValue(a), value);

    private static MethodInfo _TypeOf = type.GetMethod(nameof(JSValue.TypeOf));

    public static Expression TypeOf(Expression target) => Expression.Call(target, _TypeOf);

    private static PropertyInfo _IndexKeyString = type.IndexProperty(typeof(KeyString));
    private static PropertyInfo _IndexUInt = type.IndexProperty(typeof(uint));
    private static PropertyInfo _Index = type.IndexProperty(typeof(JSValue));
    private static PropertyInfo _SuperIndexKeyString = type.PublicIndex(typeof(JSValue), typeof(KeyString));
    private static PropertyInfo _SuperIndexUInt = type.PublicIndex(typeof(JSValue), typeof(uint));
    private static PropertyInfo _SuperIndex = type.PublicIndex(typeof(JSValue), typeof(JSValue));

    private static MethodInfo _PropertyOrUndefinedKeyString = type.PublicMethod(nameof(JSValue.PropertyOrUndefined), KeyStringsBuilder.RefType);
    private static MethodInfo _PropertyOrUndefinedUInt = type.PublicMethod(nameof(JSValue.PropertyOrUndefined), typeof(uint));
    private static MethodInfo _PropertyOrUndefined = type.PublicMethod(nameof(JSValue.PropertyOrUndefined), typeof(JSValue));

    public static Expression InvokeMethod(Expression targetTemp, Expression methodTemp, Expression target, Expression name, IFastEnumerable<Expression> args, bool spread, bool coalesce)
    {
        if (!coalesce)
            return JSValueExtensionsBuilder.InvokeMethod(target, name, args, spread);

        var method = _Index;

        if (name.Type == typeof(KeyString))
        {
            method = _IndexKeyString;
        }
        else if (name.Type == typeof(uint))
        {
            method = _IndexUInt;
        }
        else if (name.Type == typeof(int))
        {
            method = _IndexUInt;
            name = Expression.Convert(name, typeof(uint));
        }

        return Expression.Block(Expression.Assign(targetTemp, target), Expression.Assign(methodTemp, Expression.MakeIndex(targetTemp, method, name)),
            Expression.Condition(IsNullOrUndefined(methodTemp), JSUndefinedBuilder.Value, JSFunctionBuilder.InvokeFunction(methodTemp, ArgumentsBuilder.New(targetTemp, args, spread))));
    }

    public static Expression Index(Expression target, Expression super, uint i, bool coalesce = false)
    {
        if (super == null)
            return Index(target, i, coalesce);

        return Expression.MakeIndex(target, _SuperIndexUInt, [super, Expression.Constant(i)]);
    }

    public static Expression Index(Expression target, uint i, bool coalesce = false) => Expression.MakeIndex(target, _IndexUInt, [Expression.Constant(i)]);

    public static Expression Index(Expression target, Expression super, Expression property, bool coalesce = false)
    {
        if (super == null)
            return Index(target, property, coalesce);

        if (property.Type == typeof(KeyString))
            return Expression.MakeIndex(target, _SuperIndexKeyString, [super, property]);

        if (property.Type == typeof(uint))
            return Expression.MakeIndex(target, _SuperIndexUInt, [super, property]);

        if (property.Type == typeof(int))
            return Expression.MakeIndex(target, _SuperIndexUInt, [super, Expression.Convert(property, typeof(uint))]);

        return Expression.MakeIndex(target, _SuperIndex, [super, property]);
    }

    public static Expression Index(Expression target, Expression property, bool coalesce = false)
    {
        if (property.Type == typeof(KeyString))
        {
            if (coalesce)
                return Expression.Call(target, _PropertyOrUndefinedKeyString, property);

            return Expression.MakeIndex(target, _IndexKeyString, [property]);
        }

        if (property.Type == typeof(uint))
        {
            if (coalesce)
                return Expression.Call(target, _PropertyOrUndefinedUInt, property);

            return Expression.MakeIndex(target, _IndexUInt, [property]);
        }

        if (property.Type == typeof(int))
        {
            if (coalesce)
                return Expression.Call(target, _PropertyOrUndefinedUInt, Expression.Convert(property, typeof(uint)));

            return Expression.MakeIndex(target, _IndexUInt, [Expression.Convert(property, typeof(uint))]);
        }

        if (coalesce)
        {
            // we need to use a block...
            var pes = Expression.Parameters(typeof(JSValue));
            var pe = pes[0];
            return Expression.Block(pes.AsSequence(), Expression.Assign(pe, target), Expression.Condition(IsNullOrUndefined(pe),
                    JSUndefinedBuilder.Value, Expression.Call(target, _Index.GetMethod, property)));
        }

        return Expression.MakeIndex(target, _Index, [property]);
    }

    private static MethodInfo _DeleteKeyString = type.InternalMethod(nameof(JSValue.Delete), KeyStringsBuilder.RefType);
    private static MethodInfo _DeleteUInt = type.InternalMethod(nameof(JSValue.Delete), typeof(uint));
    private static MethodInfo _DeleteJSValue = type.InternalMethod(nameof(JSValue.Delete), typeof(JSValue));

    public static Expression Delete(Expression target, Expression method)
    {
        if (method.Type == typeof(KeyString))
            return Expression.Call(target, _DeleteKeyString, method);

        if (method.Type == typeof(uint))
            return Expression.Call(target, _DeleteUInt, method);

        if (method.Type == typeof(int))
            return Expression.Call(target, _DeleteUInt, Expression.Convert(method, typeof(uint)));

        return Expression.Call(target, _DeleteJSValue, method);
    }

    internal static MethodInfo _CreateInstance = type.GetMethod(nameof(JSValue.CreateInstance));

    public static Expression CreateInstance(Expression target, Expression args) => Expression.Call(target, _CreateInstance, args);

    public static MethodInfo StaticEquals = type.PublicMethod(nameof(JSValue.StaticEquals), typeof(JSValue), typeof(JSValue));

    private static MethodInfo _Equals = type.PublicMethod(nameof(JSValue.Equals), typeof(JSValue));

    public static Expression Equals(Expression target, Expression value)
    {
        if (value.Type == typeof(string))
            return JSBooleanBuilder.NewFromCLRBoolean(target.CallExpression<JSValue, string, bool>(() => (x, a) => x.EqualsLiteral(a), value));

        if (value.Type == typeof(double))
            return JSBooleanBuilder.NewFromCLRBoolean(target.CallExpression<JSValue, double, bool>(() => (x, a) => x.EqualsLiteral(a), value));

        return JSBooleanBuilder.NewFromCLRBoolean(Expression.Call(target, _Equals, value));
    }

    public static Expression NotEquals(Expression target, Expression value)
    {
        if (value.Type == typeof(string))
            return JSBooleanBuilder.NewFromCLRBoolean(Expression.Not(target.CallExpression<JSValue, string, bool>(() => (x, a) => x.EqualsLiteral(a), value)));

        if (value.Type == typeof(double))
            return JSBooleanBuilder.NewFromCLRBoolean(Expression.Not(target.CallExpression<JSValue, double, bool>(() => (x, a) => x.EqualsLiteral(a), value)));

        return JSBooleanBuilder.NewFromCLRBoolean(Expression.Not(Expression.Call(target, _Equals, value)));
    }

    private static MethodInfo _StrictEquals = type.InternalMethod(nameof(JSValue.StrictEquals), typeof(JSValue));

    public static Expression StrictEquals(Expression target, Expression value)
    {
        if (value.Type == typeof(string))
            return JSBooleanBuilder.NewFromCLRBoolean(target.CallExpression<JSValue, string, bool>(() => (x, a) => x.StrictEqualsLiteral(a), value));

        if (value.Type == typeof(double))
            return JSBooleanBuilder.NewFromCLRBoolean(target.CallExpression<JSValue, double, bool>(() => (x, a) => x.StrictEqualsLiteral(a), value));

        return JSBooleanBuilder.NewFromCLRBoolean(Expression.Call(target, _StrictEquals, value));
    }

    public static Expression NotStrictEquals(Expression target, Expression value)
    {
        if (value.Type == typeof(string))
            return JSBooleanBuilder.NewFromCLRBoolean(Expression.Not(target.CallExpression<JSValue, string, bool>(() => (x, a) => x.StrictEqualsLiteral(a), value)));

        if (value.Type == typeof(double))
            return JSBooleanBuilder.NewFromCLRBoolean(Expression.Not(target.CallExpression<JSValue, double, bool>(() => (x, a) => x.StrictEqualsLiteral(a), value)));

        return JSBooleanBuilder.NewFromCLRBoolean(Expression.Not(Expression.Call(target, _StrictEquals, value)));
    }

    private static MethodInfo _Less = type.PublicMethod(nameof(JSValue.Less), typeof(JSValue));

    public static Expression Less(Expression target, Expression value) => JSBooleanBuilder.NewFromCLRBoolean(Expression.Call(ValueOf(target), _Less, ValueOf(value)));

    private static MethodInfo _LessOrEqual = type.PublicMethod(nameof(JSValue.LessOrEqual), typeof(JSValue));

    public static Expression LessOrEqual(Expression target, Expression value) => JSBooleanBuilder.NewFromCLRBoolean(Expression.Call(ValueOf(target), _LessOrEqual, ValueOf(value)));

    private static MethodInfo _Greater = type.PublicMethod(nameof(JSValue.Greater), typeof(JSValue));
    public static Expression Greater(Expression target, Expression value) => JSBooleanBuilder.NewFromCLRBoolean(Expression.Call(ValueOf(target), _Greater, ValueOf(value)));

    private static MethodInfo _GreaterOrEqual = type.PublicMethod(nameof(JSValue.GreaterOrEqual), typeof(JSValue));
    public static Expression GreaterOrEqual(Expression target, Expression value) => JSBooleanBuilder.NewFromCLRBoolean(Expression.Call(ValueOf(target), _GreaterOrEqual, ValueOf(value)));

    public static Expression ValueOf(Expression target) => target.CallExpression<JSValue, JSValue>(() => (x) => x.ValueOf());

    public static Expression LogicalAnd(Expression target, Expression value) => Expression.Coalesce(JSValueExtensionsBuilder.NullIfTrue(target), value);

    public static Expression LogicalOr(Expression target, Expression value) => Expression.Coalesce(JSValueExtensionsBuilder.NullIfFalse(target), value);

    private static MethodInfo _GetAllKeys = type.PublicMethod(nameof(JSValue.GetAllKeys), typeof(bool), typeof(bool));

    private static MethodInfo _GetEnumerator = typeof(IEnumerable<JSValue>).GetMethod(nameof(IEnumerable<JSValue>.GetEnumerator));

    public static Expression GetAllKeys(Expression target) => Expression.Call(target, _GetAllKeys, Expression.Constant(true), Expression.Constant(false));

    public static Expression Coalesce(Expression target, Expression def) => Expression.Condition(IsNullOrUndefined(target), def, target);
}
