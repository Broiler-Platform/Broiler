using System;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class JSClassBuilder
{
    private static Type type;
    private static MethodInfo _addConstructor;
    private static ConstructorInfo _new;

    /// <summary>
    /// Initializes the builder with the concrete JSClass type.
    /// Called by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static void Initialize(Type classType)
    {
        type = classType;
        _addConstructor = type.GetMethod("AddConstructor");
        _new = type.GetConstructor([typeof(JSFunctionDelegate), typeof(JSValue).Assembly.GetType("Broiler.JavaScript.BuiltIns.Function.JSFunction"), typeof(string), typeof(string)]);
        // Fallback: search by parameter count
        if (_new == null)
        {
            foreach (var ctor in type.GetConstructors())
            {
                var p = ctor.GetParameters();
                if (p.Length == 4 && p[0].ParameterType == typeof(JSFunctionDelegate))
                {
                    _new = ctor;
                    break;
                }
            }
        }
    }

    public static YElementInit AddConstructor(Expression exp) =>
        Expression.ElementInit(_addConstructor, exp);

    public static YNewExpression New(Expression constructor, Expression super, string name, string code = "") =>
        Expression.New(_new,
            constructor ?? Expression.Null, super ?? Expression.Null, Expression.Constant(name), Expression.Constant(code));
}
