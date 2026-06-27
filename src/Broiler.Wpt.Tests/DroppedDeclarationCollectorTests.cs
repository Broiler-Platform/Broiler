namespace Broiler.Wpt.Tests;

public sealed class DroppedDeclarationCollectorTests
{
    [Fact]
    public void Aggregates_Counts_And_Orders_By_Frequency()
    {
        var collector = new DroppedDeclarationCollector();
        collector.Record("text-align", "-webkit-right");
        collector.Record("text-align", "-webkit-right");
        collector.Record("text-align", "-webkit-right");
        collector.Record("position", "wobble");

        Assert.Equal(4, collector.TotalDropped);

        var top = collector.Top(10);
        Assert.Equal("text-align: -webkit-right", top[0].Declaration);
        Assert.Equal(3, top[0].Count);
        Assert.Equal("position: wobble", top[1].Declaration);
        Assert.Equal(1, top[1].Count);
    }

    [Fact]
    public void Top_Respects_Limit()
    {
        var collector = new DroppedDeclarationCollector();
        collector.Record("a", "1");
        collector.Record("b", "2");
        collector.Record("c", "3");

        Assert.Single(collector.Top(1));
    }

    [Fact]
    public void FormatKey_Lowercases_Property_And_Caps_Value_Length()
    {
        Assert.Equal("text-align: -webkit-right",
            DroppedDeclarationCollector.FormatKey("TEXT-ALIGN", "  -webkit-right  "));

        var longValue = new string('x', 200);
        var key = DroppedDeclarationCollector.FormatKey("background", longValue);
        Assert.StartsWith("background: ", key);
        Assert.EndsWith("…", key);
        // property + ": " + 80 value chars + ellipsis
        Assert.True(key.Length < 200);
    }
}
