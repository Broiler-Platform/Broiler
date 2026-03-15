namespace YantraJS.Core.FastParser;

public class AstTryStatement(FastToken token, FastToken previousToken, AstStatement body, AstIdentifier id, AstStatement @catch, AstStatement @finally) : AstStatement(token, FastNodeType.TryStatement, previousToken)
{
    public readonly AstStatement Block = body;
    public readonly AstIdentifier Identifier = id;
    public readonly AstStatement Catch = @catch;
    public readonly AstStatement Finally = @finally;
}