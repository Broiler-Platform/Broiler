using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Promise;
using Broiler.JavaScript.Core.Emit;
using Broiler.JavaScript.Core.FastParser;
using Broiler.JavaScript.Core.FastParser.Compiler;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exp = Broiler.JavaScript.ExpressionCompiler.Expressions.YExpression;
using Broiler.JavaScript.Core.Core.Primitive;

namespace Broiler.JavaScript.Core;


/// <summary>
/// Provides the top-level API for compiling and evaluating JavaScript source
/// code within the current <see cref="JSContext"/>.
/// </summary>
public class CoreScript
{
    private static IJSCompiler _compiler = new DefaultJSCompiler();

    /// <summary>
    /// Gets or sets the compiler used by <see cref="Compile"/>.
    /// Defaults to <see cref="DefaultJSCompiler"/>.
    /// </summary>
    public static IJSCompiler Compiler
    {
        get => _compiler;
        set => _compiler = value ?? throw new System.ArgumentNullException(nameof(value));
    }

    internal static JSFunctionDelegate Compile(in StringSpan code, string location = null, IList<string> args = null, ICodeCache codeCache = null)
    {
        try
        {
            codeCache = codeCache ?? DictionaryCodeCache.Current;
            var script = code;
            var compiler = _compiler;
            var jsc = new JSCode(location, code, args, () => compiler.Compile(script, location, args, codeCache));
            return codeCache.GetOrCreate(in jsc);
        }
        catch (FastParseException ex)
        {
            throw JSContext.NewSyntaxError(ex.Message, "Compile", location, ex.Token.Start.Line);
        }
    }

    /// <summary>
    /// Evaluates JavaScript code synchronously, pumping an async message loop
    /// so that microtasks (e.g., resolved promises) are processed before
    /// returning the result.
    /// </summary>
    /// <param name="code">The JavaScript source code to evaluate.</param>
    /// <param name="location">Optional source location for diagnostics.</param>
    /// <returns>The result of evaluating <paramref name="code"/>.</returns>
    public static JSValue EvaluateWithTasks(string code, string location = null)
    {
        var result = JSUndefined.Value;
        var ctx = JSContext.Current;
        var fx = Compile(code, location, codeCache: ctx.CodeCache);
        
        AsyncPump.Run(() =>
        {
            result = fx(new Arguments(ctx));
            return Task.CompletedTask;
        });
        
        return result;
    }

    /// <summary>
    /// Evaluates JavaScript code synchronously in the current context.
    /// </summary>
    /// <param name="code">The JavaScript source code to evaluate.</param>
    /// <param name="location">Optional source location for diagnostics.</param>
    /// <param name="codeCache">Optional code cache for compiled script reuse.</param>
    /// <returns>The result of evaluating <paramref name="code"/>.</returns>
    public static JSValue Evaluate(string code, string location = null, ICodeCache codeCache = null)
    {
        var result = JSUndefined.Value;
        var ctx = JSContext.Current;
        var fx = Compile(code, location, null, codeCache ?? ctx.CodeCache);
        result = fx(new Arguments(ctx));
        return result;
    }

    /// <summary>
    /// Evaluates JavaScript code asynchronously, awaiting any pending
    /// <see cref="JSContext.WaitTask"/> before returning.
    /// </summary>
    /// <param name="code">The JavaScript source code to evaluate.</param>
    /// <param name="location">Optional source location for diagnostics.</param>
    /// <param name="codeCache">Optional code cache for compiled script reuse.</param>
    /// <returns>The result of evaluating <paramref name="code"/>.</returns>
    public static async Task<JSValue> EvaluateAsync(string code, string location = null, ICodeCache codeCache = null)
    {
        var result = JSUndefined.Value;
        var ctx = JSContext.Current;
        var fx = Compile(code, location, null, codeCache ?? ctx.CodeCache);
        
        result = fx(new Arguments(ctx));
        
        if (ctx.WaitTask != null)
            await ctx.WaitTask;
        
        return result;
    }
}

/// <summary>
/// Holds a compiled expression tree entry representing a class or object
/// member (property, getter, setter, or spread element).
/// </summary>
public class ExpressionHolder
{
    public bool Static;
    public Exp Key;
    public Exp Value;
    public Exp Getter;
    public Exp Setter;
    public bool Spread;
}
