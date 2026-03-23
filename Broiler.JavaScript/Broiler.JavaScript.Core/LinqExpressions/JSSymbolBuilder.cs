using System;
using System.Reflection;
using Broiler.JavaScript.Core.Core;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core.LinqExpressions;

public class JSSymbolBuilder
{
    private static Type type;

    /// <summary>
    /// Initializes the builder with the concrete JSSymbol type from the
    /// BuiltIns assembly.  Called by the BuiltIns assembly via
    /// <c>[ModuleInitializer]</c>.
    /// </summary>
    internal static void Initialize(Type symbolType)
    {
        type = symbolType;
    }

    /// <summary>
    /// Returns a <see cref="MethodInfo"/> for the static
    /// <c>GlobalSymbol(string)</c> method on the concrete JSSymbol type.
    /// </summary>
    public static MethodInfo GlobalSymbolMethod =>
        type?.GetMethod("GlobalSymbol", [typeof(string)]);
}
