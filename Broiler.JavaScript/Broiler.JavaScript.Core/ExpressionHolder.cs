using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Core;

public class ExpressionHolder
{
    public bool Static;
    public YExpression Key;
    public YExpression Value;
    public YExpression Getter;
    public YExpression Setter;
    public bool Spread;
}
