#nullable enable
namespace YantraJS.Core.FastParser;

public class AstBinaryExpression(AstExpression node, TokenTypes type, AstExpression right) : AstExpression(node.Start, FastNodeType.BinaryExpression, right.End)
{
    public readonly AstExpression Left = node;
    public readonly TokenTypes Operator = type;
    public readonly AstExpression Right = right;

    private string OperatorToString(TokenTypes type)
    {
        switch(type)
        {
            case TokenTypes.BooleanAnd:
                return "&&";
            case TokenTypes.BooleanOr:
                return "||";
            case TokenTypes.BitwiseAnd:
                return "&";
            case TokenTypes.BitwiseOr:
                return "|";
            case TokenTypes.Plus:
                return "+";
            case TokenTypes.Minus:
                return "-";
            case TokenTypes.Mod:
                return "%";
            case TokenTypes.Multiply:
                return "*";
            case TokenTypes.NotEqual:
                return "!=";
            case TokenTypes.Equal:
                return "==";
            case TokenTypes.StrictlyNotEqual:
                return "!==";
            case TokenTypes.StrictlyEqual:
                return "===";
            case TokenTypes.Assign:
                return "=";
        }
        return type.ToString();
    }

    public override string ToString() => $"({Left} {OperatorToString(Operator)} {Right})";
}