using Broiler.JavaScript.Ast.Misc;

namespace Broiler.JavaScript.Ast.Statements;

public class AstDebuggerStatement(FastToken token) : AstStatement(token, FastNodeType.DebuggerStatement, token)
{
    public override string ToString() => "debugger;";
}
