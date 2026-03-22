
using Broiler.JavaScript.Ast.Misc;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Parser;


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
