using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast;


public class AstProgram(FastToken token, FastToken end, IFastEnumerable<AstStatement> statements, bool isAsync) : AstBlock(token, FastNodeType.Program, end, statements)
{
    public readonly bool IsAsync = isAsync;
}
