
using Broiler.JavaScript.Ast.Misc;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Parser;


public partial class FastParser(FastTokenStream stream) : IParser
{
    private readonly FastTokenStream stream = stream;

    public readonly FastScope variableScope = new FastScope();

    /// <summary>
    /// Disable this inside for brackets...
    /// </summary>
    private bool considerInOfAsOperators = true;
    private bool isAsync = false;

    public StreamLocation BeginUndo() => new(this, stream.Current);

    public StreamLocation Location
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new StreamLocation(this, stream.Current);
    }

    public FastToken PreviousToken
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => stream.Previous;
    }

    public readonly struct StreamLocation(FastParser parser, FastToken token)
    {
        public readonly FastToken Token = token;

        public bool Reset()
        {
            parser.stream.Reset(Token);
            return false;
        }
    }

    public AstProgram ParseProgram()
    {
        if (Program(out var p))
            return p;

        throw stream.Unexpected();
    }

    bool EndOfLine()
    {
        var token = stream.Current;

        if (token.Type == TokenTypes.LineTerminator)
        {
            stream.Consume();
            return true;
        }

        return false;
    }

    bool EndOfStatement()
    {
        var token = stream.Current;

        switch (token.Type)
        {
            case TokenTypes.SemiColon:
            case TokenTypes.EOF:
            case TokenTypes.LineTerminator:
                stream.Consume();
                return true;

            // since Block will expect curly bracket
            // to be present, we will not consume this..
            case TokenTypes.CurlyBracketEnd:
                return true;
        }

        return false;
    }
}
