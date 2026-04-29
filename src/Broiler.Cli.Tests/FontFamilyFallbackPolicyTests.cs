using Broiler.HTML.Adapters;

namespace Broiler.Cli.Tests;

public class FontFamilyFallbackPolicyTests
{
    [Fact]
    public void ResolveDefaultMappings_Maps_Generic_Families_And_Helvetica_Alias()
    {
        var mappings = FontFamilyFallbackPolicy.ResolveDefaultMappings(
        [
            "Arial",
            "Times New Roman",
            "Courier New",
            "Comic Sans MS",
            "Impact"
        ]);

        Assert.Equal("Arial", mappings["sans-serif"]);
        Assert.Equal("Times New Roman", mappings["serif"]);
        Assert.Equal("Courier New", mappings["monospace"]);
        Assert.Equal("Comic Sans MS", mappings["cursive"]);
        Assert.Equal("Impact", mappings["fantasy"]);
        Assert.Equal("Arial", mappings["Helvetica"]);
    }

    [Fact]
    public void ResolveDefaultMappings_Uses_First_Available_Fallbacks_And_Skips_Redundant_Helvetica_Alias()
    {
        var mappings = FontFamilyFallbackPolicy.ResolveDefaultMappings(
        [
            "Helvetica",
            "Liberation Sans",
            "DejaVu Serif",
            "DejaVu Sans Mono"
        ]);

        Assert.Equal("Helvetica", mappings["sans-serif"]);
        Assert.Equal("DejaVu Serif", mappings["serif"]);
        Assert.Equal("DejaVu Sans Mono", mappings["monospace"]);
        Assert.False(mappings.ContainsKey("cursive"));
        Assert.False(mappings.ContainsKey("fantasy"));
        Assert.False(mappings.ContainsKey("Helvetica"));
    }
}
