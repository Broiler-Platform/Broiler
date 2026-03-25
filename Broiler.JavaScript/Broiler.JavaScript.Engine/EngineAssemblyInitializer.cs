using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Core.Core;

/// <summary>
/// Module initializer for the Engine assembly.
/// Wires factory delegates that were previously set by Core's module initializer
/// but now live in Engine (ArgumentsCoreExtensions, JSDynamicMetaData).
/// Also wires the new.target helper delegates on JSEngine.
/// </summary>
internal static class EngineAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Wire new.target access delegates so Core's JSEngine can reach
        // IJSExecutionContext-specific properties without referencing Engine.
        JSEngine.GetNewTargetFromTop = ctx =>
            (ctx as IJSExecutionContext)?.Top?.NewTarget;

        JSEngine.GetNewTargetPrototypeFromTop = ctx =>
            ((ctx as IJSExecutionContext)?.Top?.NewTarget as IJSFunction)?.Prototype as JSObject;

        // Wire JSObject factory delegate for ObjectPrototype access
        JSObject.GetCurrentObjectPrototype = static () =>
            (JSEngine.Current as IJSExecutionContext)?.ObjectPrototype;

        // Wire stack trace walking delegate
        JSEngine.AppendStackTrace = static (sb, trace) =>
        {
            var top = (JSEngine.Current as IJSExecutionContext)?.Top;
            while (top != null)
            {
                var fx = top.Function;
                var file = top.FileName;

                if (fx.IsNullOrWhiteSpace())
                    fx = "native";

                if (string.IsNullOrWhiteSpace(file))
                    file = "file";

                sb.AppendLine($"    at {fx}:{file}:{top.Line},{top.Column}");
                trace.Add((fx, file, top.Line, top.Column));
                top = top.Parent;
            }
        };

        // Wire delegates that depend on types now living in Engine.
        JSValue.CreateDynamicMetaObject = (param, value) => new JSDynamicMetaData(param, value);
        Arguments.ForApplyImpl = ArgumentsCoreExtensions.ForApplyCore;
        Arguments.RestFromImpl = ArgumentsCoreExtensions.RestFromCore;
        Arguments.GetStringImpl = ArgumentsCoreExtensions.GetStringCore;
        Arguments.GetSpreadTarget = ArgumentsCoreExtensions.GetSpreadTargetCore;
    }
}
