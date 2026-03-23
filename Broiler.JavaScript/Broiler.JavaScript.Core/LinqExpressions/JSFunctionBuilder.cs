using System;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSFunctionBuilder
{
    static Type type;

    public static FieldInfo _prototype;

    private static FieldInfo _f;

    private static MethodInfo invokeFunction;

    private static MethodInfo _invokeSuperConstructor;

    private static ConstructorInfo _newFull;

    /// <summary>
    /// Initializes the builder with the concrete JSFunction type.
    /// Called by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static void Initialize(Type functionType)
    {
        type = functionType;
        _prototype = type.GetField("prototype");
        _f = type.GetField("f", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        invokeFunction = typeof(JSValue).GetMethod(nameof(JSValue.InvokeFunction), [typeof(Arguments).MakeByRefType()]);
        _invokeSuperConstructor = type.GetMethod("InvokeSuperConstructor",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null, [typeof(JSValue), typeof(JSValue), typeof(Arguments).MakeByRefType()], null);
        _newFull = type.GetConstructor([typeof(JSFunctionDelegate), typeof(string), typeof(string), typeof(int), typeof(bool)]);
    }

    public static Expression Prototype(Expression target) => Expression.Field(target, _prototype);

    public static Expression InvokeSuperConstructor(Expression super, Expression returnValue, Expression args)
    {
        // InvokeSuper is an instance method on JSFunction
        var invokeSuperMethod = type.GetMethod("InvokeSuper",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            null, [typeof(Arguments).MakeByRefType()], null);
        return Expression.Assign(returnValue, Expression.Call(super, invokeSuperMethod, args));
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
        Expression.New(_newFull, del, name, code, Expression.Constant(length), Expression.Constant(true));
}
