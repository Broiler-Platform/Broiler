using System;
using System.Reflection;
using YantraJS.Core;
using Expression = YantraJS.Expressions.YExpression;
namespace YantraJS.ExpHelper;

public static class JSArgumentsBuilder
{
    private static Type type = typeof(JSArguments);
    private static ConstructorInfo _New
        = type.Constructor([typeof(Arguments).MakeByRefType()]);

    public static Expression New(Expression args) => Expression.New(_New, args);
}
