using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Broiler.JavaScript.ExpressionCompiler.Expressions;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Engine.FastParser.Compiler;

/// <summary>
/// Default implementation of <see cref="IJSCompiler"/> that delegates to
/// a registered compilation function.  The static constructor proactively
/// loads the <c>Broiler.JavaScript.Compiler</c> assembly (if available) to
/// trigger its <c>[ModuleInitializer]</c>, which calls <see cref="Register"/>
/// to install the real <c>FastCompiler</c>-based pipeline.
/// </summary>
public class DefaultJSCompiler : IJSCompiler
{
    /// <summary>
    /// The registered compilation delegate.  Set by the Compiler assembly's
    /// module initializer via <see cref="Register"/>.
    /// </summary>
    private static Func<StringSpan, string, IList<string>, ICodeCache, YExpression<JSFunctionDelegate>> _compileFunc;

    /// <summary>
    /// Static constructor that ensures the Compiler assembly is loaded and
    /// its <c>[ModuleInitializer]</c> has run.  Without this, the assembly
    /// may never be loaded by the runtime (assemblies are loaded lazily),
    /// leaving <see cref="_compileFunc"/> null even when the Compiler
    /// assembly is deployed alongside the application.
    /// </summary>
    static DefaultJSCompiler()
    {
        EnsureCompilerAssemblyLoaded();
    }

    /// <summary>
    /// Attempts to load the <c>Broiler.JavaScript.Compiler</c> assembly and
    /// run its module constructor so that the <c>[ModuleInitializer]</c>
    /// registers the compilation pipeline via <see cref="Register"/>.
    /// If the assembly is not available the failure is silently ignored;
    /// <see cref="Compile"/> will throw an informative exception instead.
    /// </summary>
    private static void EnsureCompilerAssemblyLoaded()
    {
        if (_compileFunc != null)
            return;

        try
        {
            var assembly = Assembly.Load("Broiler.JavaScript.Compiler");
            RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        }
        catch (Exception ex) when (
            ex is System.IO.FileNotFoundException
            or System.IO.FileLoadException
            or BadImageFormatException)
        {
            // Compiler assembly is not available.  _compileFunc remains null
            // and Compile() will throw an informative InvalidOperationException.
        }
    }

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
