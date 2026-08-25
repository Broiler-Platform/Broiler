namespace Broiler.Cli.Tests;

/// <summary>
/// Form association (HTML §4.10.2, §4.10.4): a control's <c>form</c> owner and <c>labels</c>, and a
/// <c>&lt;label&gt;</c>'s <c>control</c> and <c>form</c>.
/// </summary>
/// <remarks>
/// <para>
/// All of it was <see langword="undefined"/>. <c>control.form</c> is how a script reaches the form
/// from a control it was handed, so <c>input.form.submit()</c> threw on the property access rather
/// than the call; <c>control.labels</c> is how accessibility and validation code finds the text
/// describing a field, and <c>labels.length</c> threw rather than answering zero.
/// </para>
/// <para>
/// <b>Every expectation is a Chromium answer</b> taken through Playwright. Two of them contradict
/// the plausible reading and are the reason for checking: <c>label.form</c> follows the label's
/// <em>control</em> rather than the label's own ancestry, and the absence of these properties on a
/// non-form element is itself specified — a <c>&lt;div&gt;</c> has no <c>labels</c> property at all,
/// while an <c>&lt;input type=hidden&gt;</c> has one and it is <c>null</c>.
/// </para>
/// </remarks>
public class FormAssociationTests
{
    private static string Run(string body, string script)
    {
        var html = $"<!doctype html><html><body>{body}<div id=\"result\"></div>\n<script>\n{script}\n</script></body></html>";
        var serialized = CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
        const string start = "<div id=\"result\">";
        var open = serialized.IndexOf(start, StringComparison.Ordinal);
        Assert.True(open >= 0, $"probe did not run; document was:\n{serialized}");
        open += start.Length;
        var close = serialized.IndexOf("</div>", open, StringComparison.Ordinal);
        Assert.True(close > open, $"probe wrote nothing; document was:\n{serialized}");
        return serialized[open..close];
    }

    private const string Ids = "var $ = function (id) { return document.getElementById(id); };\n";

    /// <summary>The form owner: the <c>form</c> content attribute first, then the nearest ancestor
    /// form, then null.</summary>
    [Fact(Timeout = 600000)]
    public void A_Control_Reports_Its_Form_Owner()
    {
        var result = Run(
            "<form id=\"f1\"><input id=\"b\"></form><form id=\"f2\"></form>" +
            "<input id=\"outside\" form=\"f2\"><input id=\"orphan\">" +
            "<input id=\"dangling\" form=\"no-such-form\">",
            Ids + """
document.getElementById('result').textContent = [
  $('b').form.id, $('outside').form.id, String($('orphan').form), String($('dangling').form)
].join('|');
""");
        Assert.Equal("f1|f2|null|null", result);
    }

    /// <summary>A control's labels, in tree order — both spellings, <c>for</c> and wrapping.</summary>
    [Fact(Timeout = 600000)]
    public void A_Control_Reports_Its_Labels()
    {
        var result = Run(
            "<form id=\"f1\"><label id=\"wrap\">Wrapped <input id=\"a\"></label>" +
            "<label id=\"lfor\" for=\"b\">B</label><label id=\"lfor2\" for=\"b\">B again</label>" +
            "<input id=\"b\"><input id=\"c\"></form>",
            Ids + """
function ids(list) { return Array.prototype.map.call(list, function (l) { return l.id; }).join(','); }
document.getElementById('result').textContent = [
  $('b').labels.constructor.name, ids($('b').labels), ids($('a').labels), $('c').labels.length
].join('|');
""");
        Assert.Equal("NodeList|lfor,lfor2|wrap|0", result);
    }

    /// <summary>The list is live, as a <c>NodeList</c> from a non-static source is.</summary>
    [Fact(Timeout = 600000)]
    public void The_Labels_List_Is_Live()
    {
        var result = Run("<input id=\"c\">", Ids + """
var labels = $('c').labels;
var before = labels.length;
var added = document.createElement('label');
added.htmlFor = 'c';
document.body.appendChild(added);
document.getElementById('result').textContent = before + ' then ' + labels.length;
""");
        Assert.Equal("0 then 1", result);
    }

    /// <summary>
    /// A label's control, and its form — which is its <em>control's</em> form owner, not its own
    /// position. A label outside every form pointing into one reports that form.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Label_Reports_Its_Control_And_Its_Controls_Form()
    {
        var result = Run(
            "<form id=\"f1\"><input id=\"inside\"></form><label id=\"out\" for=\"inside\">In f1</label>" +
            "<label id=\"wrap\">W <input id=\"a\"></label><label id=\"empty\" for=\"nothing\">none</label>",
            Ids + """
document.getElementById('result').textContent = [
  $('out').control.id, $('out').form.id,
  $('wrap').control.id, String($('wrap').form),
  String($('empty').control), String($('empty').form)
].join('|');
""");
        Assert.Equal("inside|f1|a|null|null|null", result);
    }

    /// <summary>
    /// The three distinguishable absences. A hidden input is not labelable and answers <c>null</c>;
    /// a non-form element has no such property at all. Answering an empty list everywhere would be
    /// wrong in both.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Absence_Is_Reported_Three_Different_Ways()
    {
        var result = Run("<input id=\"hidden\" type=\"hidden\"><div id=\"plain\"></div><input id=\"plainInput\">",
            Ids + """
document.getElementById('result').textContent = [
  String($('hidden').labels), typeof $('hidden').form,
  typeof $('plain').labels, typeof $('plain').form,
  $('plainInput').labels.length
].join('|');
""");
        Assert.Equal("null|object|undefined|undefined|0", result);
    }
}
