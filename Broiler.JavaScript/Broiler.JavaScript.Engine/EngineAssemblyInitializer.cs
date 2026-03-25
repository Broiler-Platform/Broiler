using System.Runtime.CompilerServices;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.Engine.Internal;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.Engine;

/// <summary>
/// Module initializer for the Engine assembly.
/// Wires factory delegates for JSEngine, JSObject, JSException, JSVariable,
/// UriHelper, JSValue, Arguments, and other delegate-based extension points.
/// Consolidates initialization previously split across Core and Engine assemblies.
/// </summary>
internal static class EngineAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // ── Core class registrations & Object class factory ─────────
        JSEngine.CoreClassRegistrations = static ctx => ctx.RegisterGeneratedClasses();
        JSEngine.CreateObjectClass = ObjectClassFactory.CreateObjectClass;

        // ── JSObject factory delegates ──────────────────────────────
        JSObject.NewTypeError = static msg => JSEngine.NewTypeError(msg);
        JSObject.CoerceToNumber = static str => NumberParser.CoerceToNumber(str);
        JSObject.CreatePrimitiveObject = static p => new JSPrimitiveObject(p);
        JSObject.TryGetClrEnumeratorFunc = CoreInternalHelpers.TryGetClrEnumerator;
        JSObject.TryUnmarshalObject = CoreInternalHelpers.TryUnmarshal;

        // ── JSException delegates ───────────────────────────────────
        JSException.NewSyntaxErrorFactory = static msg => JSEngine.NewSyntaxError(msg);
        JSException.NewTypeErrorFactory = static msg => JSEngine.NewTypeError(msg);
        JSException.AppendStackTraceHelper = static (sb, trace) => JSEngine.AppendStackTrace?.Invoke(sb, trace);

        // ── JSVariable delegate ─────────────────────────────────────
        JSVariable.GetCurrentContext = static () => JSEngine.Current;

        // ── UriHelper delegate ──────────────────────────────────────
        UriHelper.NewURIError = static message => JSEngine.NewURIError(message);

        // ── JSValue.MarshalObject delegate ──────────────────────────
        JSValue.MarshalObject = static obj => JSEngine.ClrInterop.Marshal(obj);

        // ── new.target access delegates ─────────────────────────────
        JSEngine.GetNewTargetFromTop = ctx =>
            (ctx as IJSExecutionContext)?.Top?.NewTarget;

        JSEngine.GetNewTargetPrototypeFromTop = ctx =>
            ((ctx as IJSExecutionContext)?.Top?.NewTarget as IJSFunction)?.Prototype as JSObject;

        // ── JSObject factory delegate for ObjectPrototype access ────
        JSObject.GetCurrentObjectPrototype = static () =>
            (JSEngine.Current as IJSExecutionContext)?.ObjectPrototype;

        // ── Stack trace walking delegate ────────────────────────────
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

        // ── Delegates that depend on types living in Engine ─────────
        JSValue.CreateDynamicMetaObject = (param, value) => new JSDynamicMetaData(param, value);
        Arguments.ForApplyImpl = ArgumentsCoreExtensions.ForApplyCore;
        Arguments.RestFromImpl = ArgumentsCoreExtensions.RestFromCore;
        Arguments.GetStringImpl = ArgumentsCoreExtensions.GetStringCore;
        Arguments.GetSpreadTarget = ArgumentsCoreExtensions.GetSpreadTargetCore;
    }
}
