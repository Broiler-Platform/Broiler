using System;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.ExpressionCompiler.Core;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.LinqExpressions.LambdaGen;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class JSFunctionBuilder
{
    static Type type;

    public static FieldInfo _prototype;

    private static FieldInfo _f;

    private static MethodInfo invokeFunction;

    private static MethodInfo _invokeSuperConstructor;

    /// <summary>
    /// Initializes the builder with the concrete JSFunction type.
    /// Called from BuiltInsAssemblyInitializer.
    /// </summary>
    internal static void Initialize(Type functionType)
    {
        type = functionType;
        _prototype = type.PublicField("prototype")
            ?? throw new InvalidOperationException($"prototype field not found on {type.FullName}");
        _f = type.InternalField("f")
            ?? throw new InvalidOperationException($"f field not found on {type.FullName}");
        invokeFunction = typeof(JSValue).InternalMethod("InvokeFunction", ArgumentsBuilder.refType)
            ?? throw new InvalidOperationException("InvokeFunction method not found on JSValue");
        _invokeSuperConstructor = type.PublicMethod("InvokeSuperConstructor",
            typeof(JSValue), typeof(JSValue), typeof(Arguments).MakeByRefType())
            ?? throw new InvalidOperationException($"InvokeSuperConstructor method not found on {type.FullName}");
    }

    /// <summary>
    /// Gets the concrete <c>JSFunction</c> <see cref="Type"/> registered via
    /// <see cref="Initialize"/>.  Used by the Compiler to avoid a direct
    /// assembly reference to BuiltIns.
    /// </summary>
    public static Type FunctionType => type;

    public static Expression Prototype(Expression target) => Expression.Field(target, _prototype);

    public static Expression InvokeSuperConstructor(Expression super, Expression returnValue, Expression args)
    {
        return Expression.Assign(returnValue,
            Expression.Call(super, type.PublicMethod("InvokeSuper", typeof(Arguments).MakeByRefType()), args));
    }

    public static Expression InvokeFunction(Expression target, Expression args, bool coalesce = false)
    {
        if (coalesce)
        {
            var pes = Expression.Parameters(typeof(JSValue));
            var pe = pes[0];

            return Expression.Block(pes.AsSequence(), Expression.Assign(pe, target), Expression.Condition(JSValueBuilder.IsNullOrUndefined(pe),
                JSUndefinedBuilder.Value, Expression.Call(pe, invokeFunction, args)));
        }

        return Expression.Call(target, invokeFunction, args);
    }

    public static Expression New(Expression del, Expression name, Expression code, int length) =>
        NewLambdaExpression.NewExpression(type, del, name, code, Expression.Constant(length), Expression.Constant(true));
}
