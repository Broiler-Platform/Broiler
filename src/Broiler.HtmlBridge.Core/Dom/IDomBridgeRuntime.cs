using Broiler.HtmlBridge.Scripting;
using Broiler.JavaScript.Engine;
using System;
using System.Collections.Generic;

namespace Broiler.HtmlBridge.Dom;

/// <summary>
/// Narrow runtime surface required by script execution and interactive sessions.
/// </summary>
public interface IDomBridgeRuntime
{
    ContentSecurityPolicy? Csp { get; set; }

    Action? TaskCheckpointCallback { get; set; }

    IReadOnlyList<Broiler.Dom.DomElement> Elements { get; }

    int CurrentScriptIndex { get; set; }

    bool HasPendingTimers { get; }

    void Attach(JSContext context, string html);

    void Attach(JSContext context, string html, string url);

    void FireWindowLoadEvent();

    bool FlushTimerStep();

    void FlushTimers();

    string SerializeToHtml();

    Broiler.Dom.DomDocument GetRenderDocument();
}

/// <summary>
/// Creates bridge runtime instances without coupling script execution to a concrete bridge class.
/// </summary>
public interface IDomBridgeRuntimeFactory
{
    IDomBridgeRuntime Create();
}

public static class DomBridgeRuntimeLimits
{
    public const int AsyncDrainIterationLimit = 1000;
}
