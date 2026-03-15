using Broiler.HTML.Core.Core.Entities;
using System.Drawing;

namespace Broiler.HTML.Image.Tests;

public class LinkElementDataTests
{
    [Theory]
    [InlineData("#top", true)]
    [InlineData("#", true)]
    [InlineData("#section-1", true)]
    [InlineData("https://example.com", false)]
    [InlineData("./page.html", false)]
    [InlineData("", false)]
    public void IsAnchor_ReturnsExpected(string href, bool expected)
    {
        var link = new LinkElementData<RectangleF>("id1", href, RectangleF.Empty);
        Assert.Equal(expected, link.IsAnchor);
    }

    [Theory]
    [InlineData("#top", "top")]
    [InlineData("#section-1", "section-1")]
    [InlineData("#", "")]
    [InlineData("https://example.com", "")]
    public void AnchorId_ReturnsExpected(string href, string expectedId)
    {
        var link = new LinkElementData<RectangleF>("id1", href, RectangleF.Empty);
        Assert.Equal(expectedId, link.AnchorId);
    }

    [Theory]
    [InlineData("#", true)]
    [InlineData("#top", false)]
    [InlineData("#section", false)]
    [InlineData("https://example.com", false)]
    [InlineData("./page.html", false)]
    [InlineData("", false)]
    public void IsTopOfPageAnchor_ReturnsExpected(string href, bool expected)
    {
        var link = new LinkElementData<RectangleF>("id1", href, RectangleF.Empty);
        Assert.Equal(expected, link.IsTopOfPageAnchor);
    }

    [Fact]
    public void ToString_ContainsIdAndHref()
    {
        var link = new LinkElementData<RectangleF>("myId", "#top", new RectangleF(0, 0, 100, 50));
        var str = link.ToString();
        Assert.Contains("myId", str);
        Assert.Contains("#top", str);
    }
}
