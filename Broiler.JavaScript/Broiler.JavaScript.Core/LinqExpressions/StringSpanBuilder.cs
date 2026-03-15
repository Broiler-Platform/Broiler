using YantraJS.Core;
using YantraJS.Core.LambdaGen;
using Expression = YantraJS.Expressions.YExpression;

namespace YantraJS.ExpHelper;

public static class StringSpanBuilder
{
    // public static Type type = typeof(StringSpan);

    //private static ConstructorInfo _new =
    //    type.Constructor(typeof(string), typeof(int), typeof(int));

    internal static Expression New(Expression code, int start, int v) =>
        //return Expression.New(_new, code, Expression.Constant(start), Expression.Constant(v));
        NewLambdaExpression.NewExpression<StringSpan>(() => () => new StringSpan("", 0, 0)
            , code
            , Expression.Constant(start)
            , Expression.Constant(v)
        );

    internal static Expression New(in StringSpan code) => NewLambdaExpression.NewExpression<StringSpan>(() => () => new StringSpan("", 0, 0)
            , Expression.Constant(code.Source)
            , Expression.Constant(code.Offset)
            , Expression.Constant(code.Length)
        );//return Expression.New(_new, //    Expression.Constant(code.Source), //    Expression.Constant(code.Offset), //    Expression.Constant(code.Length));


    public static readonly Expression Empty =
        NewLambdaExpression.StaticFieldExpression<StringSpan>(() => () => StringSpan.Empty);
        // Expression.Field(null, type.GetField(nameof(StringSpan.Empty)));
}
