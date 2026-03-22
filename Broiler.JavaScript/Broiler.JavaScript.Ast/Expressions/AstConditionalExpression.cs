using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstConditionalExpression(AstExpression previous, AstExpression @true, AstExpression @false) : AstExpression(previous.Start, FastNodeType.ConditionalExpression, @false.End)
{
    public readonly AstExpression Test = previous;
    public readonly AstExpression True = @true;
    public readonly AstExpression False = @false;

    public override string ToString() => $"{Test} ? {True} : {False}";
}
