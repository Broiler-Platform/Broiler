using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Array;
using System;
using System.CodeDom.Compiler;
using System.Reflection;
using Expression = YantraJS.Expressions.YExpression;

namespace Broiler.JavaScript.Core.LinqExpressions;


internal class JSSpreadValueBuilder
{
    internal static Type type = typeof(JSSpreadValue);
    internal static ConstructorInfo _new = type.Constructor(typeof(JSValue));

    public static Expression New(Expression target) => Expression.New(_new, target);
}

public class ClrSpreadExpression(Expression argument) : Expression(YantraJS.Expressions.YExpressionType.Constant, argument.Type)
{
    public Expression Argument { get; } = JSSpreadValueBuilder.New(argument);

    public override void Print(IndentedTextWriter writer) { }
}
