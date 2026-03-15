using System;
using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Core.LambdaGen;
namespace YantraJS.ExpHelper;

public class ClrStringBuilder

{
    //private static Type type = typeof(string);

    //private static MethodInfo _Compare =
    //    type.StaticMethod(nameof(String.Compare), typeof(string), typeof(string));

    //private static MethodInfo _Equals =
    //    type.PublicMethod(nameof(String.Equals), typeof(string));

    //private static MethodInfo _Concat =
    //    type.StaticMethod(nameof(String.Concat), typeof(string), typeof(string));


    public static Expression Equal(Expression left, Expression right) =>
        // return Expression.Equal( Expression.Call(null, _Compare, left, right), Expression.Constant(0) );
        // return Expression.Call(left, _Equals, right);
        left.CallExpression<string, bool>(() => (x) => x.Equals(""), right);
    public static Expression NotEqual(Expression left, Expression right) => Expression.Not(Equal(left, right));

    public static Expression Concat(Expression left, Expression right) => NewLambdaExpression.StaticCallExpression(() =>
                                                                                       () => String.Concat("", "")
            , left
            , right);// return Expression.Call(null, _Concat, left, right);

}
