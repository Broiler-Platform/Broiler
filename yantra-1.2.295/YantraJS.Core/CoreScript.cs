// using FastExpressionCompiler;
using Microsoft.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using YantraJS.Core;
using YantraJS.Core.FastParser.Compiler;
using YantraJS.Emit;
using Exp = YantraJS.Expressions.YExpression;
namespace YantraJS;


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
            var jsc = new JSCode(location, code, args, () =>
            {
                return compiler.Compile(script, location, args, codeCache);
            });
            return codeCache.GetOrCreate(in jsc);
        }
        catch (Core.FastParser.FastParseException ex)
        {
            throw JSContext.Current.NewSyntaxError(ex.Message, "Compile", location, ex.Token.Start.Line);
        }
    }

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


    public static JSValue Evaluate(string code, string location = null, ICodeCache codeCache = null)
    {
        var result = JSUndefined.Value;
        var ctx = JSContext.Current;
        var fx = Compile(code, location, null, codeCache ?? ctx.CodeCache);
        result = fx(new Arguments(ctx));
        return result;
    }

    public static async Task<JSValue> EvaluateAsync(
        string code, 
        string location = null, 
        ICodeCache codeCache = null)
    {
        var result = JSUndefined.Value;
        var ctx = JSContext.Current;
        var fx = Compile(code, location, null, codeCache ?? ctx.CodeCache);
        result = fx(new Arguments(ctx));
        if (ctx.WaitTask != null)
        {
            await ctx.WaitTask;
        }
        return result;
    }


}

public class ExpressionHolder
{
    public bool Static;
    public Exp Key;
    public Exp Value;
    public Exp Getter;
    public Exp Setter;
    public bool Spread;
}
