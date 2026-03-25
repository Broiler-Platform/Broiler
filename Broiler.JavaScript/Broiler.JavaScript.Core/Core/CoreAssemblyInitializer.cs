using System.Runtime.CompilerServices;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core.Core;

internal static class CoreAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        JSEngine.CoreClassRegistrations = static ctx => ctx.RegisterGeneratedClasses();
        JSEngine.CreateObjectClass = ObjectClassFactory.CreateObjectClass;

        // Wire JSObject factory delegates for Core dependencies
        JSObject.NewTypeError = static msg => JSEngine.NewTypeError(msg);
        JSObject.CoerceToNumber = static str => Utils.NumberParser.CoerceToNumber(str);
        JSObject.CreatePrimitiveObject = static p => new JSPrimitiveObject(p);
        JSObject.TryGetClrEnumeratorFunc = Internal.CoreInternalHelpers.TryGetClrEnumerator;
        JSObject.TryUnmarshalObject = Internal.CoreInternalHelpers.TryUnmarshal;
    }
}
