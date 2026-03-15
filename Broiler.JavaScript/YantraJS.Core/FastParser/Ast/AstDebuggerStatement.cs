namespace YantraJS.Core.FastParser;

public class AstDebuggerStatement(FastToken token) : AstStatement(token, FastNodeType.DebuggerStatement, token)
{
    public override string ToString() => "debugger;";
}