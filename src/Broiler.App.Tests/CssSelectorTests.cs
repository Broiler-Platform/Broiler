using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
[Trait("Feature", "Selector")]
public class CssSelectorTests
{
    private readonly ScriptEngine _engine = new();

    [Fact]
    public void QuerySelector_ChildCombinator_SelectsDirectChild()
    {
        var html = "<html><body><div id=\"parent\"><span id=\"child\">Direct</span></div></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.querySelector('div > span');
                if (!el) throw new Error('expected element');
                if (el.id !== 'child') throw new Error('wrong element');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_AdjacentSiblingCombinator_SelectsNextSibling()
    {
        var html = "<html><body><h1 id=\"h\">Title</h1><p id=\"first\">First</p><p id=\"second\">Second</p></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.querySelector('h1 + p');
                if (!el) throw new Error('expected element');
                if (el.id !== 'first') throw new Error('wrong element');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_GeneralSiblingCombinator_SelectsSibling()
    {
        var html = "<html><body><h1 id=\"h\">Title</h1><div>Spacer</div><p id=\"later\">Later</p></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.querySelector('h1 ~ p');
                if (!el) throw new Error('expected element');
                if (el.id !== 'later') throw new Error('wrong element');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_DescendantCombinator_SelectsNestedElement()
    {
        var html = "<html><body><div id=\"outer\"><section><span id=\"deep\">Deep</span></section></div></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.querySelector('div span');
                if (!el) throw new Error('expected element');
                if (el.id !== 'deep') throw new Error('wrong element');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_NthChild2_ReturnsSecondChild()
    {
        var html = "<html><body><ul><li id=\"a\">1</li><li id=\"b\">2</li><li id=\"c\">3</li></ul></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.querySelector('li:nth-child(2)');
                if (!el) throw new Error('expected element');
                if (el.id !== 'b') throw new Error('wrong element');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_NthChildOdd_MatchesOddElements()
    {
        var html = "<html><body><ul><li id=\"a\">1</li><li id=\"b\">2</li><li id=\"c\">3</li></ul></body></html>";
        var result = _engine.Execute(
            [ @"
                var els = document.querySelectorAll('li:nth-child(odd)');
                if (els.length < 2) throw new Error('expected at least 2 odd elements');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_NthChildEven_MatchesEvenElements()
    {
        var html = "<html><body><ul><li id=\"a\">1</li><li id=\"b\">2</li><li id=\"c\">3</li></ul></body></html>";
        var result = _engine.Execute(
            [ @"
                var els = document.querySelectorAll('li:nth-child(even)');
                if (els.length < 1) throw new Error('expected at least 1 even element');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_FirstChild_ReturnsFirstChild()
    {
        var html = "<html><body><ul><li id=\"a\">1</li><li id=\"b\">2</li></ul></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.querySelector('li:first-child');
                if (!el) throw new Error('expected element');
                if (el.id !== 'a') throw new Error('wrong element');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_LastChild_ReturnsLastChild()
    {
        var html = "<html><body><ul><li id=\"a\">1</li><li id=\"b\">2</li></ul></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.querySelector('li:last-child');
                if (!el) throw new Error('expected element');
                if (el.id !== 'b') throw new Error('wrong element');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_FirstOfType_ReturnsFirstOfType()
    {
        var html = "<html><body><div><span id=\"s1\">A</span><p id=\"p1\">B</p><span id=\"s2\">C</span></div></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.querySelector('span:first-of-type');
                if (!el) throw new Error('expected element');
                if (el.id !== 's1') throw new Error('wrong element');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_NotPseudoClass_ExcludesMatching()
    {
        var html = "<html><body><div class=\"a\">A</div><div class=\"excluded\">B</div><div class=\"c\">C</div></body></html>";
        var result = _engine.Execute(
            [ @"
                var els = document.querySelectorAll('div:not(.excluded)');
                for (var i = 0; i < els.length; i++) {
                    if (els[i].className === 'excluded') throw new Error('excluded element found');
                }
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_PseudoElements_DoNotCauseErrors()
    {
        var html = "<html><body><p id=\"test\">Hello</p></body></html>";
        // Pseudo-elements like ::before and ::after should not throw errors
        var result = _engine.Execute(
            [ @"
                var el = document.querySelector('p');
                if (!el) throw new Error('expected element');
            " ], html);
        Assert.True(result);
    }

    [Fact]
    public void QuerySelector_CompoundSelectorWithMultiplePseudoClasses()
    {
        var html = "<html><body><ul><li id=\"a\" class=\"item\">1</li><li id=\"b\" class=\"item\">2</li><li id=\"c\" class=\"item\">3</li></ul></body></html>";
        var result = _engine.Execute(
            [ @"
                var el = document.querySelector('li.item:first-child');
                if (!el) throw new Error('expected element');
                if (el.id !== 'a') throw new Error('wrong element');
            " ], html);
        Assert.True(result);
    }
}
