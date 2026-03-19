namespace Broiler.JavaScript.Core.FastParser.Ast;

public class AstDebuggerStatement(FastToken token) : AstStatement(token, FastNodeType.DebuggerStatement, token)
{
    public override string ToString() => "debugger;";
}