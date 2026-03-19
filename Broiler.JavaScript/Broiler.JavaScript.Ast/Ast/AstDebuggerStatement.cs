namespace Broiler.JavaScript.Ast;

public class AstDebuggerStatement(FastToken token) : AstStatement(token, FastNodeType.DebuggerStatement, token)
{
    public override string ToString() => "debugger;";
}