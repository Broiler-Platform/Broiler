using Broiler.JavaScript.Core.Core;

namespace Broiler.Cli.Tests;

public class Phase1FixTests
{
    [Fact]
    public void ToFixed_NegativeZero_Returns_PositiveString()
    {
        using var c = new JSContext();
        var r = c.Eval("(-0).toFixed(4)");
        Assert.Equal("0.0000", r.ToString());
    }

    [Fact]
    public void Substr_Negative_Start()
    {
        using var c = new JSContext();
        var r = c.Eval("'scathing'.substr(-7, 3)");
        Assert.Equal("cat", r.ToString());
    }

    [Fact]
    public void NullByte_In_Regex_Test()
    {
        using var c = new JSContext();
        // \0 in string should be null char (code 0)
        var r1 = c.Eval("'form\\0div'.charCodeAt(4)");
        Assert.Equal(0.0, r1.DoubleValue);

        // \02 should be octal 2 (char code 2)
        var r2 = c.Eval("'\\02'.charCodeAt(0)");
        Assert.Equal(2.0, r2.DoubleValue);
    }
}
