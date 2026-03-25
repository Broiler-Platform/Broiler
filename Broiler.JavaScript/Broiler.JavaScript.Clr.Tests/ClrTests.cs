using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Clr;
using Broiler.JavaScript.Engine;

namespace Broiler.JavaScript.Clr.Tests;

public class ClrTests
{
    [Fact]
    public void ClrProxy_From_WrapsObject()
    {
        using var ctx = new JSContext();
        var list = new System.Collections.Generic.List<int> { 1, 2, 3 };
        var proxy = ClrProxy.From(list);
        Assert.NotNull(proxy);
    }

    [Fact]
    public void ClrType_From_WrapsType()
    {
        using var ctx = new JSContext();
        var clrType = ClrType.From(typeof(string));
        Assert.NotNull(clrType);
    }

    [Fact]
    public void ClrProxy_Marshal_Int_ReturnsJSNumber()
    {
        using var ctx = new JSContext();
        var result = ClrProxy.Marshal(42);
        Assert.True(result.IsNumber);
        Assert.Equal(42.0, result.DoubleValue);
    }
}
