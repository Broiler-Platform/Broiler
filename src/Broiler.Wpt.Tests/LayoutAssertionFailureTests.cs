namespace Broiler.Wpt.Tests;

public sealed class LayoutAssertionFailureTests
{
    [Fact]
    public void Describe_Formats_Element_Property_Expected_And_Actual()
    {
        var failure = new LayoutAssertionFailure("span.abspos[title=start]", "offset-y", 0, 13);

        Assert.Equal("span.abspos[title=start] expected offset-y=0, got 13", failure.Describe());
    }

    [Fact]
    public void Describe_Trims_Trailing_Zeros_On_Fractional_Values()
    {
        var failure = new LayoutAssertionFailure("div#box", "width", 100, 99.5);

        Assert.Equal("div#box expected width=100, got 99.5", failure.Describe());
    }
}
