using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Statements;


public class AstIfStatement(FastToken start, FastToken end, AstExpression test, AstStatement @true, AstStatement? @false = null) : AstStatement(start, FastNodeType.IfStatement, end)
{
    public readonly AstExpression Test = test;
    public readonly AstStatement True = @true;
    public readonly AstStatement? False = @false;

    public override string ToString() => False != null ? $"if({Test}) {True} else {False}" : $"if({Test}) {True}";
}
