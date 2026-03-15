using System.Collections.Generic;
using YantraJS.Emit;
using YantraJS.Expressions;

namespace YantraJS.Core.FastParser.Compiler;

/// <summary>
/// Default implementation of <see cref="IJSCompiler"/> that delegates to
/// <see cref="FastCompiler"/> for AST-to-expression-tree compilation.
/// </summary>
public class DefaultJSCompiler : IJSCompiler
{
    /// <inheritdoc />
    public YExpression<JSFunctionDelegate> Compile(
        in StringSpan code,
        string location = null,
        IList<string> argsList = null,
        ICodeCache codeCache = null)
    {
        var compiler = new FastCompiler(code, location, argsList, codeCache);
        return compiler.Method;
    }
}
