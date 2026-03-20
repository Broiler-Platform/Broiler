using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.Compiler.Tests;

/// <summary>
/// Ensures the <c>Broiler.JavaScript.Compiler</c> assembly is loaded
/// before any test code runs so that its module initializer registers
/// the FastCompiler pipeline via <see cref="DefaultJSCompiler.Register"/>.
/// </summary>
internal static class CompilerTestBootstrap
{
    [ModuleInitializer]
    internal static void EnsureCompilerLoaded()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(FastCompiler).Module.ModuleHandle);
    }
}
