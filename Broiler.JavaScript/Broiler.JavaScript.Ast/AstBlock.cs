using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;
using Broiler.JavaScript.ExpressionCompiler.Core;

namespace Broiler.JavaScript.Ast;


public class AstBlock : AstStatement
{
    public IFastEnumerable<StringSpan>? HoistingScope;
    public readonly IFastEnumerable<AstStatement> Statements;

    protected AstBlock(FastToken start, FastNodeType type, FastToken end, IFastEnumerable<AstStatement> statements) : base(start, type, end) => Statements = statements;

    public AstBlock(FastToken start, FastToken end, IFastEnumerable<AstStatement> list) : base(start, FastNodeType.Block, end) => Statements = list;

    public override string ToString() => Statements.Join("\n\t");
}
