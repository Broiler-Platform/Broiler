using System;
using Broiler.HtmlBridge;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 8 item 1: <see cref="IScriptEngine"/> is split into four narrow capability contracts —
/// <see cref="IScriptExecutor"/>, <see cref="IInteractiveScriptEngine"/>, <see cref="IScriptProfiling"/>,
/// <see cref="IScriptEventLoop"/> — which it aggregates as the v2 compatibility surface. These lock the
/// segregation: the aggregate still inherits every capability, the concrete engine implements each, and a
/// consumer can depend on just the narrow capability it needs.
/// </summary>
public class ScriptEngineCapabilitySegregationTests
{
    [Fact]
    public void IScriptEngine_Aggregates_The_Four_Capabilities()
    {
        var inherited = typeof(IScriptEngine).GetInterfaces();
        Assert.Contains(typeof(IScriptExecutor), inherited);
        Assert.Contains(typeof(IInteractiveScriptEngine), inherited);
        Assert.Contains(typeof(IScriptProfiling), inherited);
        Assert.Contains(typeof(IScriptEventLoop), inherited);
    }

    [Fact]
    public void ScriptEngine_Implements_Each_Segregated_Capability()
    {
        var engine = new ScriptEngine();
        Assert.IsAssignableFrom<IScriptExecutor>(engine);
        Assert.IsAssignableFrom<IInteractiveScriptEngine>(engine);
        Assert.IsAssignableFrom<IScriptProfiling>(engine);
        Assert.IsAssignableFrom<IScriptEventLoop>(engine);
        Assert.IsAssignableFrom<IScriptEngine>(engine);
    }

    [Fact]
    public void NarrowConsumer_CanDependOnJustTheExecutorCapability()
    {
        // A consumer that only needs execution can accept the narrow capability and still run scripts.
        IScriptExecutor executor = new ScriptEngine();
        Assert.False(executor.StrictModeEnabled);
        Assert.True(executor.Execute(Array.Empty<string>()), "empty script batch executes successfully.");
    }

    [Fact]
    public void NarrowConsumer_CanDependOnJustTheEventLoopCapability()
    {
        IScriptEventLoop loop = new ScriptEngine();
        Assert.NotNull(loop.MicroTasks);
    }
}
