using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.Runtime;
using System;
using System.CodeDom.Compiler;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;


public class JSSpreadValueBuilder
{
    public static Type type = typeof(JSSpreadValue);
    public static ConstructorInfo _new = type.Constructor(typeof(JSValue));

    public static Expression New(Expression target) => Expression.New(_new, target);
}

public class ClrSpreadExpression(Expression argument) : Expression(YExpressionType.Constant, argument.Type)
{
    public Expression Argument { get; } = JSSpreadValueBuilder.New(argument);

    public override void Print(IndentedTextWriter writer) { }
}
