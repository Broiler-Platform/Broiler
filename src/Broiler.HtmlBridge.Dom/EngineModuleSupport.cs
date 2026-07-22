using System;
using System.Threading.Tasks;
using Broiler.HtmlBridge.Logging;

namespace Broiler.HtmlBridge.Scripting;

/// <summary>
/// One-time, cached probe for whether the underlying JS engine can actually drive ES modules end-to-end
/// (a static import binds its value). This is true only once the engine carries the top-level-await codegen
/// fix (submodule patch 0010) and the module-orchestration completion fix (patch 0011). On an engine
/// without them a static import resolves to <c>undefined</c> or the module body stalls, so the bridge must
/// keep its own <see cref="EsModuleLinker"/>.
///
/// The probe runs a real module (a static <c>data:</c> import) through a <see cref="BridgeModuleContext"/>
/// and checks the binding. It is guarded by a timeout because, on an un-patched engine, the module init can
/// <em>hang</em> (its top-level-await continuation is never pumped) rather than fail fast — a hang must be
/// treated as "not supported", never propagated to the caller.
/// </summary>
public static class EngineModuleSupport
{
    private static readonly Lazy<bool> _available = new(Probe);

    /// <summary>True when the engine binds static ES-module imports (patches 0010+0011 present).</summary>
    public static bool Available => _available.Value;

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private static bool Probe()
    {
        try
        {
            // Run on a worker so a hung (un-pumped) module init cannot block this thread; the worker is
            // abandoned on timeout — a one-time cost paid at most once per process.
            var probe = Task.Run(RunProbe);
            if (!probe.Wait(ProbeTimeout))
            {
                RenderLogger.LogDebug(LogCategory.JavaScript, nameof(EngineModuleSupport),
                    "Engine module probe timed out; using EsModuleLinker fallback.");
                return false;
            }

            var supported = probe.Result;
            RenderLogger.LogDebug(LogCategory.JavaScript, nameof(EngineModuleSupport),
                supported
                    ? "Engine binds ES-module imports; using engine-driven module execution."
                    : "Engine does not bind ES-module imports; using EsModuleLinker fallback.");
            return supported;
        }
        catch (Exception ex)
        {
            RenderLogger.LogDebug(LogCategory.JavaScript, nameof(EngineModuleSupport),
                $"Engine module probe failed ({ex.GetType().Name}); using EsModuleLinker fallback.");
            return false;
        }
    }

    private static bool RunProbe()
    {
        try
        {
            using var ctx = new BridgeModuleContext();
            ctx.RunScriptAsync(
                    "import { x } from 'data:text/javascript,export const x = 41;';\n" +
                    "globalThis.__brEngineModuleProbe = x + 1;",
                    "file:///probe/", uniqueModuleID: "file:///probe/main.js")
                .GetAwaiter().GetResult();
            return ctx.Eval("globalThis.__brEngineModuleProbe|0").DoubleValue == 42;
        }
        catch
        {
            return false;
        }
    }
}
