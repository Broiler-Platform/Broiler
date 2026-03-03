using Broiler.App.Rendering;

namespace Broiler.App.Tests;

[Trait("Category", "Unit")]
[Trait("Engine", "Broiler")]
public class CssAnimationsTests
{
    [Fact]
    public void CssTransition_Parse_SimpleTransition()
    {
        var t = CssTransition.Parse("opacity 0.3s ease-in");
        Assert.Equal("opacity", t.Property);
        Assert.Equal(TimeSpan.FromMilliseconds(300), t.Duration);
        Assert.Equal(CssTimingFunction.EaseIn, t.TimingFunction);
    }

    [Fact]
    public void CssTransition_Parse_WithDelay()
    {
        var t = CssTransition.Parse("transform 1s linear 0.5s");
        Assert.Equal("transform", t.Property);
        Assert.Equal(TimeSpan.FromSeconds(1), t.Duration);
        Assert.Equal(CssTimingFunction.Linear, t.TimingFunction);
        Assert.Equal(TimeSpan.FromMilliseconds(500), t.Delay);
    }

    [Fact]
    public void CssTransition_ParseMultiple_ReturnsMultipleTransitions()
    {
        var transitions = CssTransition.ParseMultiple("opacity 0.3s, transform 1s");
        Assert.Equal(2, transitions.Count);
        Assert.Equal("opacity", transitions[0].Property);
        Assert.Equal("transform", transitions[1].Property);
    }

    [Fact]
    public void CssTransition_Parse_MillisecondDuration()
    {
        var t = CssTransition.Parse("color 300ms");
        Assert.Equal("color", t.Property);
        Assert.Equal(TimeSpan.FromMilliseconds(300), t.Duration);
    }

    [Fact]
    public void CssTransitionEngine_Interpolate_MidPoint()
    {
        var result = CssTransitionEngine.Interpolate(0f, 100f, 0.5f);
        Assert.Equal(50f, result);
    }

    [Fact]
    public void CssTransitionEngine_Interpolate_Start()
    {
        var result = CssTransitionEngine.Interpolate(10f, 90f, 0f);
        Assert.Equal(10f, result);
    }

    [Fact]
    public void CssTransitionEngine_Interpolate_End()
    {
        var result = CssTransitionEngine.Interpolate(10f, 90f, 1f);
        Assert.Equal(90f, result);
    }

    [Fact]
    public void CssTransitionEngine_ApplyTimingFunction_Linear()
    {
        var result = CssTransitionEngine.ApplyTimingFunction(CssTimingFunction.Linear, 0.5f);
        Assert.Equal(0.5f, result, 3);
    }

    [Fact]
    public void CssTransitionEngine_ApplyTimingFunction_EaseIn_SlowerAtStart()
    {
        var result = CssTransitionEngine.ApplyTimingFunction(CssTimingFunction.EaseIn, 0.25f);
        Assert.True(result < 0.25f, "EaseIn should produce a value below the linear progress at early stages");
    }

    [Fact]
    public void CssKeyframe_Properties()
    {
        var kf = new CssKeyframe
        {
            Selector = 0.5f,
            Declarations = new Dictionary<string, string> { { "opacity", "0.5" } }
        };
        Assert.Equal(0.5f, kf.Selector);
        Assert.Equal("0.5", kf.Declarations["opacity"]);
    }

    [Fact]
    public void CssAnimationDefinition_Parse_FromTo()
    {
        var body = "from { opacity: 0; } to { opacity: 1; }";
        var def = CssAnimationDefinition.Parse("fadeIn", body);
        Assert.Equal("fadeIn", def.Name);
        Assert.True(def.Keyframes.Count >= 2);
    }

    [Fact]
    public void CssAnimation_Parse_Simple()
    {
        var a = CssAnimation.Parse("fadeIn 1s ease-in");
        Assert.Equal("fadeIn", a.Name);
        Assert.Equal(TimeSpan.FromSeconds(1), a.Duration);
        Assert.Equal(CssTimingFunction.EaseIn, a.TimingFunction);
    }
}
