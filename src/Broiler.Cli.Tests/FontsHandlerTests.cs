
using Broiler.Graphics;

namespace Broiler.Cli.Tests;

public class FontsHandlerTests
{
    [Fact]
    public void IsFontExists_With_CommaSeparated_List_Uses_Mapped_Loaded_Family()
    {
        var fontCreator = new TestFontCreator();
        var handler = new FontsHandler(fontCreator);
        handler.AddFontFamily(new TestFontFamily("LoadedAhem"));
        handler.AddFontFamily(new TestFontFamily("Arial"));
        handler.AddFontFamilyMapping("Ahem", "LoadedAhem");

        Assert.True(handler.IsFontExists("Missing, Ahem, Arial"));
        Assert.True(handler.IsFontExists("'Missing', \"Ahem\""));
        Assert.False(handler.IsFontExists("Missing, Unknown"));
    }

    [Fact]
    public void GetCachedFont_With_CommaSeparated_List_Uses_First_Available_Mapped_Family_And_Caches_By_Resolved_Name()
    {
        var fontCreator = new TestFontCreator();
        var handler = new FontsHandler(fontCreator);
        handler.AddFontFamily(new TestFontFamily("LoadedAhem"));
        handler.AddFontFamily(new TestFontFamily("Arial"));
        handler.AddFontFamilyMapping("Ahem", "LoadedAhem");

        var font = handler.GetCachedFont("Missing, Ahem, Arial", 12, FontStyle.Regular);
        var cachedFont = handler.GetCachedFont("Ahem, Arial", 12, FontStyle.Regular);

        Assert.Equal("LoadedAhem", ((TestFont)font).FamilyName);
        Assert.Same(font, cachedFont);
        Assert.Equal(["LoadedAhem"], fontCreator.CreatedFamilies);
    }

    [Fact]
    public void GetCachedFont_With_Only_Missing_List_Falls_Back_To_First_Candidate_Not_Whole_List()
    {
        var fontCreator = new TestFontCreator();
        var handler = new FontsHandler(fontCreator);

        var font = handler.GetCachedFont("Missing, Arial", 12, FontStyle.Regular);

        Assert.Equal("Missing", ((TestFont)font).FamilyName);
        Assert.Equal(["Missing"], fontCreator.CreatedFamilies);
    }

    private sealed class TestFontCreator : IFontCreator
    {
        public List<string> CreatedFamilies { get; } = [];

        public RFont CreateFont(string family, double size, FontStyle style)
        {
            CreatedFamilies.Add(family);
            return new TestFont(family, size);
        }

        public RFont CreateFont(RFontFamily family, double size, FontStyle style)
        {
            CreatedFamilies.Add(family.Name);
            return new TestFont(family.Name, size);
        }
    }

    private sealed class TestFontFamily(string name) : RFontFamily
    {
        public override string Name => name;
    }

    private sealed class TestFont : RFont
    {
        public TestFont(string familyName, double size)
        {
            FamilyName = familyName;
            Size = size;
        }

        public string FamilyName { get; }
        public override double Size { get; }
        public override double Height => Size;
        public override double UnderlineOffset => 0;
        public override double LeftPadding => 0;
        public override double GetWhitespaceWidth(RGraphics graphics) => 0;
    }
}
