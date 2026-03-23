using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Function;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using System;
using System.Reflection;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class JSClassBuilder
{
    private static Type _type;
    private static MethodInfo _addConstructor;
    private static ConstructorInfo _ctor;

    /// <summary>
    /// The concrete JSClass type, set during assembly initialization.
    /// Used by the Compiler to allocate temp variables without a direct type reference.
    /// </summary>
    public static Type Type => _type;

    /// <summary>
    /// Initializes the builder with the concrete JSClass type.
    /// Called by the BuiltIns assembly via <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static void Initialize(Type classType)
    {
        _type = classType;
        _addConstructor = classType.GetMethod(nameof(AddConstructorName), [typeof(JSFunction)]);
        _ctor = classType.GetConstructor([typeof(JSFunctionDelegate), typeof(JSFunction), typeof(string), typeof(string)]);
    }

    private const string AddConstructorName = "AddConstructor";

    public static YElementInit AddConstructor(Expression exp) => Expression.ElementInit(_addConstructor, exp);

    public static YNewExpression New(Expression constructor, Expression super, string name, string code = "") =>
        Expression.New(_ctor,
            constructor ?? Expression.Null, super ?? Expression.Null, Expression.Constant(name), Expression.Constant(code));
}
