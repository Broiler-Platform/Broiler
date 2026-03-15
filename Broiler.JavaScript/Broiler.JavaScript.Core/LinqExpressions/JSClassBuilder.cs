using YantraJS.Core;
using Expression = YantraJS.Expressions.YExpression;
using YantraJS.Expressions;
using YantraJS.Core.Types;
using YantraJS.Core.LambdaGen;

namespace YantraJS.ExpHelper;

public static class JSClassBuilder
{
    //static Type type = typeof(JSClass);

    //private static ConstructorInfo _New =
    //    type.Constructor(new Type[] {
    //        typeof(JSFunctionDelegate), typeof(JSFunction), typeof(string), typeof(string)  });

    //public static MethodInfo _AddConstructor =
    //    type.PublicMethod(nameof(JSClass.AddConstructor), typeof(JSFunction));

    public static YElementInit AddConstructor(YExpression exp) =>
        // return YExpression.ElementInit(_AddConstructor, exp);
        YExpression.ElementInit(TypeQuery.QueryInstanceMethod<JSClass>(() =>
            (x) => x.AddConstructor((JSFunction)null))
            , exp
        );


    public static YNewExpression New(
        Expression constructor,
        Expression super,
        string name,
        string code = "") => NewLambdaExpression.NewExpression<JSClass>(
            () => () => new JSClass(
                (JSFunctionDelegate)null,
                (JSFunction)null,
                (string)null,
                (string)null),
            constructor ?? Expression.Null,
            super ?? Expression.Null,
            Expression.Constant(name),
            Expression.Constant(code)
        );//return Expression.New(_New,//    constructor ?? Expression.Null,//    super ?? Expression.Null,//    Expression.Constant(name),//    Expression.Constant(code));
}
