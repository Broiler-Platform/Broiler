using System.Runtime.CompilerServices;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Extensions;

internal static class ExtensionsAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Initialize JSObjectBuilder with the concrete JSObjectFastPropertyExtensions type
        // so the Compiler can build property expression trees without a direct reference.
        JSObjectBuilder.Initialize(typeof(JSObjectFastPropertyExtensions));
    }
}
