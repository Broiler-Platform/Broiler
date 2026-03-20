using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.LinqExpressions;

namespace Broiler.JavaScript.Clr;

internal static class ClrAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Register the full CLR interop implementation.
        JSContext.ClrInterop = DefaultClrInterop.Instance;

        // Register the expression tree builder for CLR proxy marshalling.
        ClrProxyBuilder.Register(
            ClrExpressionBuilder.Marshal,
            ClrExpressionBuilder.From);

        // Register the default CLR module provider for JSModuleContext.
        JSContext.ClrModuleProvider = () => ClrModule.Default;
    }
}
