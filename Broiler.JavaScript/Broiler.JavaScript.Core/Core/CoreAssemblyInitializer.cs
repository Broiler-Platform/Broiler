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

        // Wire JSException delegates for engine-level functionality.
        // These use lambdas that defer to JSEngine methods which themselves
        // are delegate-backed and get fully wired when the BuiltIns and Engine
        // assemblies load their module initializers.
        JSException.NewSyntaxErrorFactory = static msg => JSEngine.NewSyntaxError(msg);
        JSException.NewTypeErrorFactory = static msg => JSEngine.NewTypeError(msg);
        JSException.AppendStackTraceHelper = static (sb, trace) => JSEngine.AppendStackTrace?.Invoke(sb, trace);

        // Wire JSVariable delegate for accessing the current execution context
        // without a direct dependency on JSEngine.
        JSVariable.GetCurrentContext = static () => JSEngine.Current;
    }
}
