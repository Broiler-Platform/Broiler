using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Statements;


public class AstReturnStatement(FastToken token, FastToken previousToken, AstExpression? target = null) : AstStatement(token, FastNodeType.ReturnStatement, previousToken)
{
    public readonly AstExpression? Argument = target;

    public override string ToString()
    {
        var hasSemiColonAtEnd = End.Type == TokenTypes.SemiColon ? ":" : "";
        if (Argument != null)
            return $"return {Argument}{hasSemiColonAtEnd}";

        return $"return {hasSemiColonAtEnd}";
    }
}
