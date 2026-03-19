using Broiler.JavaScript.Core.Core;
using System;
using System.Reflection;
using Expression = YantraJS.Expressions.YExpression;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class JSArgumentsBuilder
{
    private static readonly Type type = typeof(JSArguments);
    private static readonly ConstructorInfo _New = type.Constructor([typeof(Arguments).MakeByRefType()]);

    public static Expression New(Expression args) => Expression.New(_New, args);
}
