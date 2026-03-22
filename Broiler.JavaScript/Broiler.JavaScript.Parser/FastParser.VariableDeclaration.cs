

using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Ast.Statements;

namespace Broiler.JavaScript.Parser;


partial class FastParser
{
    bool VariableDeclaration(out AstStatement node, FastVariableKind kind = FastVariableKind.Var)
    {
        var begin = stream.Current;

        node = default;
        stream.Consume();

        if (!Parameters(out var declarators, TokenTypes.SemiColon, false, kind))
            throw stream.Unexpected();

        node = new AstVariableDeclaration(begin, PreviousToken, declarators, kind);
        return true;
    }

    bool VariableDeclarationStatement(out AstVariableDeclaration node, FastVariableKind kind = FastVariableKind.Var)
    {
        var begin = stream.Current;

        node = default;
        stream.Consume();

        if (!Parameters(out var declarators, TokenTypes.SemiColon, false, kind))
            throw stream.Unexpected();

        node = new AstVariableDeclaration(begin, PreviousToken, declarators, kind);
        return true;
    }


}
