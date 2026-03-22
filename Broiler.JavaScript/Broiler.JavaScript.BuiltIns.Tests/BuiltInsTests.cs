using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.BuiltIns.Tests;

public class BuiltInsTests
{
    [Fact]
    public void WeakRef_Construct_And_Deref()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var obj = { value: 42 }; var wr = new WeakRef(obj); wr.deref().value;");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void EventTarget_Construct_Succeeds()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var t = new EventTarget(); typeof t;");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void FinalizationRegistry_Construct_Succeeds()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var fr = new FinalizationRegistry(function(v) {}); typeof fr;");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void WeakRef_TypeOf_IsObject()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var wr = new WeakRef({}); typeof wr;");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void BuiltIns_ModuleInitializer_Registers()
    {
        EnsureBuiltInsLoaded();
        Assert.NotNull(DefaultBuiltInRegistry.AdditionalRegistrations);
    }

    /// <summary>
    /// Forces the BuiltIns assembly to load by referencing a type from it,
    /// which triggers the ModuleInitializer.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnsureBuiltInsLoaded()
    {
        RuntimeHelpers.RunClassConstructor(
            typeof(Broiler.JavaScript.Core.Core.Weak.JSWeakRef).TypeHandle);
    }
}
