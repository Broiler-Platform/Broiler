using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast;

public readonly struct AstCase(AstExpression test, IFastEnumerable<AstStatement> last)
{
    public readonly AstExpression Test = test;
    public readonly IFastEnumerable<AstStatement> Statements = last;
}
