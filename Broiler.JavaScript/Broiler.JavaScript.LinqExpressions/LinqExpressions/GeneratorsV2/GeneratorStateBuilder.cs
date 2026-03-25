using Broiler.JavaScript.LinqExpressions.LambdaGen;
using Expression = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.LinqExpressions.LinqExpressions.GeneratorsV2;

public class GeneratorStateBuilder
{
    public static Expression New(Expression value, int id, bool @delegate = false) => NewLambdaExpression.NewExpression<GeneratorState>(() => () =>
    new GeneratorState(null, 0, false), value, Expression.Constant(id), Expression.Constant(@delegate));

    public static Expression New(int id) => NewLambdaExpression.NewExpression<GeneratorState>(() => () =>
    new GeneratorState(null, 0, false), JSUndefinedBuilder.Value, Expression.Constant(id), Expression.Constant(false));
}
