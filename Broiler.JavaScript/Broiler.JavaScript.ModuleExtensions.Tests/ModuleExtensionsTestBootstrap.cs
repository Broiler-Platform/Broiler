using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.ModuleExtensions.Tests;

/// <summary>
/// Ensures the <c>Broiler.JavaScript.Clr</c> and
/// <c>Broiler.JavaScript.Compiler</c> assemblies are loaded before
/// any test code runs so that their module initializers register
/// CLR interop and the FastCompiler pipeline.
/// </summary>
internal static class ModuleExtensionsTestBootstrap
{
    [ModuleInitializer]
    internal static void EnsureAssembliesLoaded()
    {
        _ = Broiler.JavaScript.Clr.DefaultClrInterop.Instance;
        RuntimeHelpers.RunModuleConstructor(typeof(FastCompiler).Module.ModuleHandle);
    }
}
