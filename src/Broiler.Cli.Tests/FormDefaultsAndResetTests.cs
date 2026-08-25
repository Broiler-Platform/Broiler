namespace Broiler.Cli.Tests;

/// <summary>
/// A form control's <b>default</b> state, the <b>reset</b> that restores it, and the radio-group
/// invariant a reset has to re-impose (HTML §4.10.11, §4.10.10, §4.10.21.4).
/// </summary>
/// <remarks>
/// <para>
/// This closes the roadmap's "form-control dirty/default/reset/radio semantics" retest entry, which
/// was carried as uncharacterized. Characterizing it found the dirty half already correct — a
/// property write does not reflect to the content attribute, and once the dirty flag is set a later
/// <c>setAttribute</c> no longer changes the value — and the default half absent outright:
/// <c>defaultValue</c>, <c>defaultChecked</c> and <c>form.reset()</c> were all <c>undefined</c>, an
/// untouched <c>&lt;textarea&gt;</c> reported <c>""</c> rather than its contents, and
/// <c>option.defaultSelected</c> was <c>false</c> for an option the markup had selected.
/// </para>
/// <para>
/// <b>Every expectation here is a Chromium answer, not a reading of the specification.</b> The same
/// probes were run against the pinned Chromium through Playwright first, and the values below are
/// what it returned — including the two that a reading could plausibly have got backwards: which
/// member of a group with two checked radios survives (the last, not the first), and whether
/// appending an already-checked radio into a group re-imposes exclusivity at all (it does).
/// </para>
/// </remarks>
public class FormDefaultsAndResetTests
{
    private static string Run(string body)
    {
        var html = $"<!doctype html><html><body>{body}<div id=\"result\"></div></body></html>";
        var serialized = CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
        const string start = "<div id=\"result\">";
        var open = serialized.IndexOf(start, StringComparison.Ordinal);
        Assert.True(open >= 0, $"probe did not run; document was:\n{serialized}");
        open += start.Length;
        var close = serialized.IndexOf("</div>", open, StringComparison.Ordinal);
        Assert.True(close > open, $"probe wrote nothing; document was:\n{serialized}");
        return serialized[open..close];
    }

    // ───────────────────────────── the default state ─────────────────────────────

    /// <summary>
    /// <c>defaultValue</c> and <c>defaultChecked</c> exist and report the markup. They were
    /// <c>undefined</c>, so a page comparing the current value against the original to decide
    /// whether a field is unsaved compared against <c>undefined</c> and concluded "changed" for every
    /// field — including ones it had just reset.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Defaults_Report_The_Markup()
    {
        var result = Run("""
<input id="t" type="text" value="a"><textarea id="a">orig</textarea><input id="c" type="checkbox" checked>
<script>
var t = document.getElementById('t'), a = document.getElementById('a'), c = document.getElementById('c');
document.getElementById('result').textContent =
  t.defaultValue + '|' + a.defaultValue + '|' + c.defaultChecked;
</script>
""");
        Assert.Equal("a|orig|true", result);
    }

    /// <summary>
    /// An untouched <c>&lt;textarea&gt;</c>'s value is its child text content, not <c>""</c>. It
    /// reported the empty string, so a form read before the user typed anything submitted an empty
    /// field, and a page that pre-filled a textarea through its markup could not read back what it
    /// had written.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Untouched_Textarea_Reports_Its_Contents()
    {
        var result = Run("""
<textarea id="a">orig</textarea>
<script>
var a = document.getElementById('a');
var initial = a.value;
a.value = 'edited';
document.getElementById('result').textContent =
  initial + '|' + a.value + '|' + a.textContent;
</script>
""");
        // Writing `value` sets the dirty value flag and does NOT rewrite the children — which is
        // exactly what separates it from writing `defaultValue` below.
        Assert.Equal("orig|edited|orig", result);
    }

    /// <summary>
    /// Writing <c>defaultValue</c> writes the default: the <c>value</c> attribute for an input, the
    /// child text for a textarea. A control with no dirty value flag then reports the new default as
    /// its value too.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Writing_A_Default_Writes_The_Markup_It_Reflects()
    {
        var result = Run("""
<input id="t" type="text" value="a"><textarea id="a">orig</textarea><input id="c" type="checkbox">
<script>
var t = document.getElementById('t'), a = document.getElementById('a'), c = document.getElementById('c');
t.defaultValue = 'z'; a.defaultValue = 'newdefault'; c.defaultChecked = true;
document.getElementById('result').textContent =
  t.getAttribute('value') + '|' + t.value + '|' +
  a.textContent + '|' + a.value + '|' +
  c.hasAttribute('checked') + '|' + c.checked;
</script>
""");
        Assert.Equal("z|z|newdefault|newdefault|true|true", result);
    }

    /// <summary>
    /// <c>option.defaultSelected</c> reflects the <c>selected</c> content attribute. It read the
    /// bridge's runtime slot alone, so it was <c>false</c> for every option the markup had selected —
    /// including the one the select was showing, so a page asking "is this the original selection?"
    /// was told no about the option it had just been handed.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void An_Options_DefaultSelected_Reflects_Its_Markup()
    {
        var result = Run("""
<select id="s"><option value="x">X</option><option value="y" selected>Y</option></select>
<script>
var s = document.getElementById('s');
document.getElementById('result').textContent =
  s.options[0].defaultSelected + '|' + s.options[1].defaultSelected + '|' + s.value + '/' + s.selectedIndex;
</script>
""");
        Assert.Equal("false|true|y/1", result);
    }

    // ─────────────────────────────────── reset ───────────────────────────────────

    /// <summary>
    /// <c>form.reset()</c> did not exist, so the call a "clear this form" control is written as was a
    /// TypeError on <c>undefined</c>: it aborted the handler rather than clearing anything, and every
    /// edited control kept its edited state.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Reset_Restores_Every_Control_To_Its_Markup()
    {
        var result = Run("""
<form id="f"><input id="t" type="text" value="a"><textarea id="a">orig</textarea>
<input id="c" type="checkbox" checked><input id="u" type="checkbox">
<select id="s"><option value="x">X</option><option value="y" selected>Y</option></select></form>
<script>
var f = document.getElementById('f'), t = document.getElementById('t'), a = document.getElementById('a');
var c = document.getElementById('c'), u = document.getElementById('u'), s = document.getElementById('s');
t.value = 'typed'; a.value = 'edited'; c.checked = false; u.checked = true; s.selectedIndex = 0;
var threw = 'no';
try { f.reset(); } catch (e) { threw = e.name || String(e); }
document.getElementById('result').textContent =
  threw + '|' + t.value + '|' + a.value + '|' + c.checked + '|' + u.checked + '|' + s.value + '/' + s.selectedIndex;
</script>
""");
        Assert.Equal("no|a|orig|true|false|y/1", result);
    }

    /// <summary>
    /// A reset restores the <em>default</em>, which is whatever the default currently is — so a
    /// <c>defaultValue</c> written by script survives the reset that discards the value written
    /// beside it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Reset_Restores_A_Script_Written_Default()
    {
        var result = Run("""
<form id="f"><textarea id="a">orig</textarea></form>
<script>
var f = document.getElementById('f'), a = document.getElementById('a');
a.defaultValue = 'newdefault';
a.value = 'edited';
f.reset();
document.getElementById('result').textContent = a.value;
</script>
""");
        Assert.Equal("newdefault", result);
    }

    // ───────────────────────────────── radio groups ─────────────────────────────────

    /// <summary>
    /// The half that already worked, kept so it stays working: setting <c>checked</c> through the
    /// property unchecks the rest of that group and leaves other groups alone.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Setting_Checked_Unchecks_Only_That_Group()
    {
        var result = Run("""
<form id="f"><input id="r1" type="radio" name="g" checked><input id="r2" type="radio" name="g">
<input id="o" type="radio" name="other" checked></form>
<script>
var r1 = document.getElementById('r1'), r2 = document.getElementById('r2'), o = document.getElementById('o');
r2.checked = true;
document.getElementById('result').textContent = r1.checked + '|' + r2.checked + '|' + o.checked;
</script>
""");
        Assert.Equal("false|true|true", result);
    }

    /// <summary>
    /// An already-checked radio joining a group re-imposes the invariant, and the newcomer wins. The
    /// property setter's exclusivity walk never runs for it — the element was checked while still
    /// detached, in a group of one — so the group was left holding two checked members: a state no
    /// interaction can produce, and one that submits two values for a single field.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Inserting_A_Checked_Radio_Unchecks_The_Group()
    {
        var result = Run("""
<form id="f"><input id="r1" type="radio" name="g" checked></form>
<script>
var f = document.getElementById('f'), r1 = document.getElementById('r1');
var extra = document.createElement('input');
extra.type = 'radio'; extra.name = 'g'; extra.setAttribute('checked', '');
f.appendChild(extra);
document.getElementById('result').textContent =
  r1.checked + '|' + extra.checked + '|' + document.querySelectorAll('input[name=g]:checked').length;
</script>
""");
        Assert.Equal("false|true|1", result);
    }

    /// <summary>
    /// A reset restores each radio from its own <c>checked</c> attribute, so markup carrying two
    /// leaves two momentarily checked — and the invariant is re-imposed afterwards, keeping the
    /// <b>last</b> in tree order. Last rather than first because the rule fires whenever a radio
    /// becomes checked, so restoring them in order leaves each unchecking the ones before it; the
    /// reference confirms it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Reset_Leaves_At_Most_One_Radio_Checked_Per_Group()
    {
        var result = Run("""
<form id="f"><input id="r1" type="radio" name="g" checked><input id="r2" type="radio" name="g">
<input id="r3" type="radio" name="g" checked></form>
<script>
var f = document.getElementById('f');
document.getElementById('r2').checked = true;
f.reset();
document.getElementById('result').textContent =
  document.getElementById('r1').checked + '|' + document.getElementById('r2').checked + '|' +
  document.getElementById('r3').checked + '|' +
  document.querySelectorAll('input[name=g]:checked').length;
</script>
""");
        Assert.Equal("false|false|true|1", result);
    }

    // ────────────────────────────── the dirty half, unchanged ──────────────────────────────

    /// <summary>
    /// Characterization of what was already correct, pinned so this change cannot quietly alter it:
    /// a property write does not reflect to the content attribute, and once the dirty flag is set a
    /// later <c>setAttribute</c> no longer moves the value.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Dirty_Flags_Decouple_The_Value_From_The_Attribute()
    {
        var result = Run("""
<form id="f"><input id="t" type="text" value="a"><input id="c" type="checkbox" checked></form>
<script>
var t = document.getElementById('t'), c = document.getElementById('c');
t.value = 'typed'; c.checked = false;
var afterSet = t.value + '|' + t.getAttribute('value') + '|' + c.checked + '|' + c.hasAttribute('checked');
t.setAttribute('value', 'b');
document.getElementById('result').textContent = afterSet + '|' + t.value;
</script>
""");
        Assert.Equal("typed|a|false|true|typed", result);
    }
}
