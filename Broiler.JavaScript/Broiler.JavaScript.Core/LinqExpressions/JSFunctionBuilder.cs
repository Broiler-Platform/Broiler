using System;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSFunctionBuilder
{
    static Type type = typeof(JSFunction);

    public static FieldInfo _prototype = type.PublicField(nameof(JSFunction.prototype));

    public static Expression Prototype(Expression target) => Expression.Field(target, _prototype);

    private static FieldInfo _f = type.InternalField(nameof(JSFunction.f));

    private static MethodInfo invokeFunction = typeof(JSValue).InternalMethod(nameof(JSFunction.InvokeFunction), ArgumentsBuilder.refType);

    private static MethodInfo _invokeSuperConstructor = typeof(JSFunction).PublicMethod(nameof(JSFunction.InvokeSuperConstructor),
            typeof(JSValue), typeof(JSValue), typeof(Arguments).MakeByRefType());

    public static Expression InvokeSuperConstructor(Expression super, Expression returnValue, Expression args) => Expression.Assign(returnValue,
            super.CallExpression<JSFunction, JSValue>(() => (x) => x.InvokeSuper(Arguments.Empty), args));

    public static Expression InvokeFunction(Expression target, Expression args, bool coalesce = false)
    {
        if (coalesce)
        {
            var pes = Expression.Parameters(typeof(JSValue));
            var pe = pes[0];

            return Expression.Block(pes.AsSequence(), Expression.Assign(pe, target), Expression.Condition(JSValueBuilder.IsNullOrUndefined(pe),
                JSUndefinedBuilder.Value, pe.CallExpression<JSFunction, Arguments, JSValue>(() => (x, a) => x.InvokeFunction(a), args)));
        }

        return target.CallExpression<JSFunction, Arguments, JSValue>(() => (x, a) => x.InvokeFunction(a), args);
    }

    public static Expression New(Expression del, Expression name, Expression code, int length) =>
        NewLambdaExpression.NewExpression<JSFunction>(() => () => new JSFunction(null, "", "", 0, false), del, name, code, Expression.Constant(length), Expression.Constant(true));
}
