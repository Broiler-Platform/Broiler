#if !WEBATOMS
// using FastExpressionCompiler;
#endif
using System.Collections.Generic;
using YantraJS.Core;

namespace YantraJS.Emit;

public readonly struct JSCode(string location, in StringSpan code, IList<string> args, JSCodeCompiler compiler)
{
    public readonly string Location = location;

    public readonly StringSpan Code = code;

    public readonly IList<string> Arguments = args;

    public readonly JSCodeCompiler Compiler = compiler;

    public JSCode Clone() => new(Location, Code, Arguments, Compiler);

    public string Key
    {
        get
        {
            if (Arguments != null)
            {
                return $"`ARGS:{string.Join(",", Arguments)}\r\n{Code}";
            }
            return $"`ARGS:\r\n{Code}";
        }
    }
}
