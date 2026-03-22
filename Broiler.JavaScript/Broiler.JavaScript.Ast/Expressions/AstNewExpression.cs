using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast.Expressions;

public class AstNewExpression(FastToken begin, AstExpression node, IFastEnumerable<AstExpression> arguments) : AstExpression(begin, FastNodeType.NewExpression, node.End)
{
    public readonly AstExpression Callee = node;
    public readonly IFastEnumerable<AstExpression> Arguments = arguments;

    public override string ToString() => $"new {Callee}({Arguments.Join()})";
}
