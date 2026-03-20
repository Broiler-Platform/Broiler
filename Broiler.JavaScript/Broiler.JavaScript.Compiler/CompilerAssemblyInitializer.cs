using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.Compiler;

internal static class CompilerAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Register the FastCompiler-based compilation pipeline.
        DefaultJSCompiler.Register(
            (code, location, argsList, codeCache) =>
            {
                var compiler = new FastCompiler(code, location, argsList, codeCache);
                return compiler.Method;
            });
    }
}
