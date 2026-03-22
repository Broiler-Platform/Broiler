using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstBinaryExpression(AstExpression node, TokenTypes type, AstExpression right) : AstExpression(node.Start, FastNodeType.BinaryExpression, right.End)
{
    public readonly AstExpression Left = node;
    public readonly TokenTypes Operator = type;
    public readonly AstExpression Right = right;

    private static string OperatorToString(TokenTypes type) => type switch
    {
        TokenTypes.BooleanAnd => "&&",
        TokenTypes.BooleanOr => "||",
        TokenTypes.BitwiseAnd => "&",
        TokenTypes.BitwiseOr => "|",
        TokenTypes.Plus => "+",
        TokenTypes.Minus => "-",
        TokenTypes.Mod => "%",
        TokenTypes.Multiply => "*",
        TokenTypes.NotEqual => "!=",
        TokenTypes.Equal => "==",
        TokenTypes.StrictlyNotEqual => "!==",
        TokenTypes.StrictlyEqual => "===",
        TokenTypes.Assign => "=",
        _ => type.ToString(),
    };

    public override string ToString() => $"({Left} {OperatorToString(Operator)} {Right})";
}
