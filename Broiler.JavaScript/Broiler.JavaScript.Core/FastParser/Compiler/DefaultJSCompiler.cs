using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Emit;
using System;
using System.Collections.Generic;
using Broiler.JavaScript.ExpressionCompiler.Expressions;

namespace Broiler.JavaScript.Core.FastParser.Compiler;

/// <summary>
/// Default implementation of <see cref="IJSCompiler"/> that delegates to
/// a registered compilation function.  When the
/// <c>Broiler.JavaScript.Compiler</c> assembly is loaded, its module
/// initializer calls <see cref="Register"/> to install the real
/// <c>FastCompiler</c>-based pipeline.
/// </summary>
public class DefaultJSCompiler : IJSCompiler
{
    /// <summary>
    /// The registered compilation delegate.  Set by the Compiler assembly's
    /// module initializer via <see cref="Register"/>.
    /// </summary>
    private static Func<StringSpan, string, IList<string>, ICodeCache, YExpression<JSFunctionDelegate>> _compileFunc;

    /// <summary>
    /// Registers the compilation function.  Called by the Compiler assembly's
    /// module initializer to wire in the real <c>FastCompiler</c> pipeline.
    /// </summary>
    public static void Register(
        Func<StringSpan, string, IList<string>, ICodeCache, YExpression<JSFunctionDelegate>> compileFunc)
    {
        _compileFunc = compileFunc ?? throw new ArgumentNullException(nameof(compileFunc));
    }

    /// <inheritdoc />
    public YExpression<JSFunctionDelegate> Compile(
        in StringSpan code,
        string location = null,
        IList<string> argsList = null,
        ICodeCache codeCache = null)
    {
        var func = _compileFunc ?? throw new InvalidOperationException(
            "The JavaScript compiler is not available. " +
            "Reference the Broiler.JavaScript.Compiler assembly to enable script compilation.");
        return func(code, location, argsList, codeCache);
    }
}
