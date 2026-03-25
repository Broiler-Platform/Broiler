using System.Runtime.CompilerServices;
using Broiler.JavaScript.LinqExpressions.LinqExpressions;

namespace Broiler.JavaScript.Modules;

internal static class ModulesAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Initialize JSArgumentsBuilder with the concrete JSArguments type so the
        // Compiler can build arguments expression trees without a direct reference.
        JSArgumentsBuilder.Initialize(typeof(JSArguments));
    }
}
