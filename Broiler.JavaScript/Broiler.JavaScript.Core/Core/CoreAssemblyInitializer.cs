using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.Core.Core;

internal static class CoreAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        JSEngine.CoreClassRegistrations = static ctx => ctx.RegisterGeneratedClasses();
    }
}
