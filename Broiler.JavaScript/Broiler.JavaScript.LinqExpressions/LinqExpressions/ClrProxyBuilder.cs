using Broiler.JavaScript.Runtime;
using System;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

/// <summary>
/// Thin dispatcher that delegates CLR expression building to a registered
/// implementation.  The concrete implementation lives in the
/// <c>Broiler.JavaScript.Clr</c> assembly and is registered via
/// <see cref="Register"/>.
/// </summary>
public static class ClrProxyBuilder
{
    private static Func<Expression, Expression> _marshalImpl;
    private static Func<Expression, Expression> _fromImpl;

    /// <summary>
    /// Registers the expression builder implementation.
    /// Called by the Clr assembly during its module initializer.
    /// </summary>
    public static void Register(
        Func<Expression, Expression> marshal,
        Func<Expression, Expression> from)
    {
        _marshalImpl = marshal ?? throw new ArgumentNullException(nameof(marshal));
        _fromImpl = from ?? throw new ArgumentNullException(nameof(from));
    }

    public static Expression Marshal(Expression target)
    {
        if (typeof(JSValue).IsAssignableFrom(target.Type))
            return target;

        if (_marshalImpl == null)
            throw new InvalidOperationException(
                "CLR expression builder not registered. " +
                "Ensure the Broiler.JavaScript.Clr assembly is loaded.");

        return _marshalImpl(target);
    }

    public static Expression From(Expression target)
    {
        if (typeof(JSValue).IsAssignableFrom(target.Type))
            return target;

        if (_fromImpl == null)
            throw new InvalidOperationException(
                "CLR expression builder not registered. " +
                "Ensure the Broiler.JavaScript.Clr assembly is loaded.");

        return _fromImpl(target);
    }
}
