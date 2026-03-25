using System;
using System.Reflection;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Storage;
using Broiler.JavaScript.LinqExpressions.LambdaGen;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions;

public class KeyStringsBuilder
{
    public static readonly Type RefType = typeof(KeyString).MakeByRefType();

    public static Expression GetOrCreate(Expression text) => NewLambdaExpression.StaticCallExpression<KeyString>(() => () => KeyStrings.GetOrCreate((StringSpan)""), text);

    public readonly static StringMap<YFieldExpression> Fields = ToStringMap(typeof(KeyStrings).GetFields());

    private static StringMap<YFieldExpression> ToStringMap(FieldInfo[] fields)
    {
        StringMap<YFieldExpression> map = new();

        foreach (var field in fields)
            map.Put(field.Name) = Expression.Field(null, field);

        return map;
    }
}
