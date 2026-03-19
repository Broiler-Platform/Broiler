using Broiler.JavaScript.Core.FastParser.Ast;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Core.FastParser;


partial class FastParser
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool Identitifer(out AstIdentifier node)
    {
        if (stream.CheckAndConsume(TokenTypes.Identifier, out var token))
        {
            node = new AstIdentifier(token);
            return true;
        }

        node = null;
        return false;
    }
}
