using YantraJS.Core;

namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstProgram(FastToken token, FastToken end, IFastEnumerable<AstStatement> statements, bool isAsync) : AstBlock(token, FastNodeType.Program, end, statements)
{
    public readonly bool IsAsync = isAsync;
}
