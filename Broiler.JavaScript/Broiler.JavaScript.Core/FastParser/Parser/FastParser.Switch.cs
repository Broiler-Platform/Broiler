namespace YantraJS.Core.FastParser;


public readonly struct AstCase(AstExpression test, IFastEnumerable<AstStatement> last)
{
    public readonly AstExpression Test = test;
    public readonly IFastEnumerable<AstStatement> Statements = last;
}

partial class FastParser
{



    bool Switch(out AstStatement node)
    {
        var begin = stream.Current;
        stream.Consume();
        node = null;

        stream.Expect(TokenTypes.BracketStart);
        if (!Expression(out var target))
            throw stream.Unexpected();
        stream.Expect(TokenTypes.BracketEnd);

        stream.Expect(TokenTypes.CurlyBracketStart);
        var nodes = new Sequence<AstCase>();
        var statements = new Sequence<AstStatement>();
        AstExpression test = null;
        bool hasDefault = false;
        try
        {
            while (!stream.CheckAndConsume(TokenTypes.CurlyBracketEnd))
            {
                if(stream.CheckAndConsume(FastKeywords.@case))
                {
                    if (test != null)
                    {
                        nodes.Add(new AstCase(test, statements));
                        statements = [];
                    }
                    if (!Expression(out test))
                        throw stream.Unexpected();
                    stream.Expect(TokenTypes.Colon);
                } else if(stream.CheckAndConsume(FastKeywords.@default))
                {
                    stream.Expect(TokenTypes.Colon);
                    if (test != null) {
                        nodes.Add(new AstCase(test, statements));
                        statements = [];
                    }
                    test = null;
                    hasDefault = true;
                } else if (Statement(out var stmt))
                    statements.Add(stmt);
            }

            if(test != null || hasDefault)
            {
                nodes.Add(new AstCase(test, statements));
                // statements = new Sequence<AstStatement>();
            }

            node = new AstSwitchStatement(begin, PreviousToken, target, nodes);
            return true;

        } finally
        {
            // nodes.Clear();
            // statements.Clear();
        }
    }


}
