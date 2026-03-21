using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;

namespace Broiler.JavaScript.Core;

/// <summary>
/// Holds a compiled expression tree entry representing a class or object
/// member (property, getter, setter, or spread element).
/// </summary>
public class ExpressionHolder
{
    public bool Static;
    public Exp Key;
    public Exp Value;
    public Exp Getter;
    public Exp Setter;
    public bool Spread;
}
