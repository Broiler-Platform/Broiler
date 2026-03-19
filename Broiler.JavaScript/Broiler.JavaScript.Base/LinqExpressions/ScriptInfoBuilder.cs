using Broiler.JavaScript.Core.CodeGen;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LambdaGen;
using YantraJS.Core;
using Expression = YantraJS.Expressions.YExpression;
using Broiler.JavaScript.Core.TypeQuery;

namespace Broiler.JavaScript.Core.LinqExpressions;

public static class ScriptInfoBuilder
{
    public static Expression New(string fileName, string code)
    {
        var _code = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceField<ScriptInfo, string>(() => (x) => x.Code);
        var _fileName = Broiler.JavaScript.Core.TypeQuery.TypeQuery.QueryInstanceField<ScriptInfo, string>(() => (x) => x.FileName);

        return Expression.MemberInit(NewLambdaExpression.NewExpression<ScriptInfo>(() => () =>
        new ScriptInfo()), Expression.Bind(_code, Expression.Constant(code)), Expression.Bind(_fileName, Expression.Constant(fileName)));
    }

    public static Expression Code(Expression scriptInfo) => scriptInfo.FieldExpression<ScriptInfo, string>(() => (x) => x.Code);

    public static Expression FileName(Expression scriptInfo) => scriptInfo.FieldExpression<ScriptInfo, string>(() => (x) => x.FileName);

    public static Expression KeyString(Expression scriptInfo, int index) =>
        Expression.ArrayIndex(scriptInfo.FieldExpression<ScriptInfo, KeyString[]>(() => (x) => x.Indices), Expression.Constant(index));

    public static Expression Build(Expression scriptInfo, StringArray keyStrings)
    {
        Sequence<Expression> list = new(keyStrings.List.Count);
        foreach (var item in keyStrings.List)
        {
            var code = Code(scriptInfo);
            var key = item.Offset > 0 ? 
                KeyStringsBuilder.GetOrCreate(StringSpanBuilder.New(code, item.Offset, item.Length)) : 
                KeyStringsBuilder.GetOrCreate(StringSpanBuilder.New(item.Value));
            
            list.Add(key);
        }

        return Expression.Assign(scriptInfo.FieldExpression<ScriptInfo, KeyString[]>(() => (x) => x.Indices), Expression.NewArrayInit(typeof(KeyString), list));
    }
}
