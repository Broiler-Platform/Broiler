namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 4 item 2 (inline-style single authority — the script-observable slice): a JS
/// <c>element.style</c> mutation and <c>getAttribute("style")</c> must observe the same state
/// (roadmap exit criterion). The bridge keeps its kebab-case inline-style dict as the working store,
/// but every CSSOM mutation now writes through to the canonical <c>style=</c> attribute, so a script
/// reading <c>getAttribute("style")</c> after a style mutation sees the current declaration (the
/// CSSOM-serialized form, matching real browsers) rather than the stale author string.
///
/// The <c>MARK=[…]</c> wrapper isolates the value the script observed live during execution — the
/// final serialization also syncs the dict into the <c>style=</c> attribute, so asserting on the
/// serialized <c>&lt;div&gt;</c> alone would not distinguish live from end-of-run state.
/// </summary>
public sealed class InlineStyleWriteThroughTests
{
    private static string Run(string bodyScript)
    {
        var html = $@"<!DOCTYPE html><html><head></head><body>
<div id=""t"" style=""color: red""></div>
<div id=""result""></div>
<script>
var t = document.getElementById('t');
function mark(v) {{ document.getElementById('result').textContent = 'MARK=[' + (v == null ? '' : v) + ']'; }}
{bodyScript}
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");
    }

    [Fact]
    public void CamelCase_Set_Reflects_Live_In_GetAttribute()
    {
        var result = Run("t.style.backgroundColor = 'blue'; mark(t.getAttribute('style'));");
        Assert.Contains("MARK=[color: red; background-color: blue]", result);
    }

    [Fact]
    public void SetProperty_Reflects_Live_In_GetAttribute()
    {
        var result = Run("t.style.setProperty('margin-top', '5px'); mark(t.getAttribute('style'));");
        Assert.Contains("margin-top: 5px]", result);
    }

    [Fact]
    public void CssText_Set_Reflects_Live_In_GetAttribute()
    {
        var result = Run("t.style.cssText = 'width: 10px'; mark(t.getAttribute('style'));");
        Assert.Contains("MARK=[width: 10px]", result);
    }

    [Fact]
    public void WholeStyle_Assign_Reflects_Live_In_GetAttribute()
    {
        var result = Run("t.style = 'height: 20px'; mark(t.getAttribute('style'));");
        Assert.Contains("MARK=[height: 20px]", result);
    }

    [Fact]
    public void RemoveProperty_Reflects_Live_In_GetAttribute()
    {
        // Removing the only property empties the declaration → getAttribute("style") is null/empty live.
        var result = Run("t.style.removeProperty('color'); mark(t.getAttribute('style'));");
        Assert.Contains("MARK=[]", result);
    }

    [Fact]
    public void Emptying_All_Properties_Removes_The_Style_Attribute_Live()
    {
        var result = Run("t.style.removeProperty('color'); mark(t.hasAttribute('style'));");
        Assert.Contains("MARK=[false]", result);
    }

    [Fact]
    public void Unmutated_Element_Returns_Raw_Author_String_Live()
    {
        // No style mutation: getAttribute returns the exact author string (no normalization/seeding).
        var result = Run("mark(t.getAttribute('style'));");
        Assert.Contains("MARK=[color: red]", result);
    }

    [Fact]
    public void GetComputedStyle_Observes_The_Same_State_After_Mutation()
    {
        var result = Run(
            "t.style.setProperty('color', 'rgb(1, 2, 3)');" +
            "var cs = window.getComputedStyle(t);" +
            "mark(cs.getPropertyValue('color'));");
        Assert.Contains("MARK=[rgb(1, 2, 3)]", result);
    }

    [Fact]
    public void Serialization_Reflects_Mutated_Inline_Style()
    {
        var result = Run("t.style.setProperty('padding', '3px'); mark('done');");
        Assert.Contains("padding: 3px", result); // serialized <div style="..."> in the output HTML
    }
}
