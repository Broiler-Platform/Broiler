using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Storage;
using Broiler.JavaScript.Core.LambdaGen;
using System;
using System.Reflection;
using YantraJS.Expressions;
using Expression = YantraJS.Expressions.YExpression;

namespace Broiler.JavaScript.Core.LinqExpressions;

internal class KeyStringsBuilder
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
