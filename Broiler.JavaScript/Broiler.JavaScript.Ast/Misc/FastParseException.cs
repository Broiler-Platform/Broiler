using System;

namespace Broiler.JavaScript.Ast.Misc;

public class FastParseException(FastToken token, string message) : Exception(message)
{
    public readonly FastToken Token = token;
}
