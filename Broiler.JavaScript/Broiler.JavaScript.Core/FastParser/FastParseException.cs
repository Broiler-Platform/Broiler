using System;

namespace Broiler.JavaScript.Core.FastParser;

public class FastParseException(FastToken token, string message) : Exception(message)
{
    public readonly FastToken Token = token;
}
