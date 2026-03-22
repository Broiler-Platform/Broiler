using Broiler.JavaScript.Ast.Expressions;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast.Misc;

public readonly struct Case(AstExpression test, IFastEnumerable<AstStatement> last)
{
    public readonly AstExpression Test = test;
    public readonly IFastEnumerable<AstStatement> Statements = last;
}
