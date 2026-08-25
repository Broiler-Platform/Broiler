using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;

namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>CharacterData</c> mutation methods raise a real <c>IndexSizeError</c> <c>DOMException</c>
/// for an offset past the end of the data (DOM §4.10, and §4.11 for <c>splitText</c>).
/// </summary>
/// <remarks>
/// They used to throw a plain error whose <em>message</em> was the string <c>"INDEX_SIZE_ERR"</c> —
/// the legacy constant's name used as prose. Nothing a page can branch on came out of that:
/// <c>e instanceof DOMException</c> was false, <c>e.name</c> was <c>"Error"</c> and <c>e.code</c> was
/// <c>0</c>, so both checks a caller actually writes failed and the error read as an internal fault
/// rather than the specified, recoverable one. The bridge already minted correct DOMExceptions for
/// <c>appendChild</c> and <c>createElement</c>; this wired CharacterData to the same helper.
/// </remarks>
public class CharacterDataExceptionTests
{
    private static string ExecJs(string jsCode)
    {
        var html = $@"<!doctype html>
<html><head><title>T</title></head><body><div id=""a"">hello</div><div id=""result""></div>
<script>
{jsCode}
</script>
</body></html>";
        return CaptureService.ExecuteScriptsWithDom(html, "https://example.com/page");
    }

    // Every method that carries the rule, including splitText and the negative-offset spelling.
    [Theory(Timeout = 600000)]
    [InlineData("t.substringData(99, 1)")]
    [InlineData("t.insertData(99, 'z')")]
    [InlineData("t.deleteData(99, 1)")]
    [InlineData("t.replaceData(99, 1, 'z')")]
    [InlineData("t.splitText(99)")]
    [InlineData("t.substringData(-1, 1)")]
    public void An_Out_Of_Range_Offset_Throws_IndexSizeError(string call)
    {
        var result = ExecJs($@"
            var t = document.getElementById('a').firstChild;
            var out = 'NOTHROW';
            try {{ {call}; }}
            catch (e) {{ out = (e instanceof DOMException) + '|' + e.name + '|' + e.code; }}
            document.getElementById('result').textContent = 'V:' + out;
        ");
        Assert.Contains("V:true|IndexSizeError|1", result);
    }

    // The message names the method and the offending offset rather than repeating a constant name.
    [Fact(Timeout = 600000)]
    public void The_Error_Message_Names_The_Method_And_Offset()
    {
        var result = ExecJs(@"
            var t = document.getElementById('a').firstChild;
            try { t.deleteData(99, 1); }
            catch (e) { document.getElementById('result').textContent = 'V:' + e.message; }
        ");
        Assert.Contains("deleteData", result);
        Assert.Contains("99", result);
        Assert.DoesNotContain("INDEX_SIZE_ERR", result);
    }

    // A DOMException is also an Error, so existing `catch (e) { e.message }` handling still works.
    [Fact(Timeout = 600000)]
    public void The_Thrown_Value_Is_Still_An_Error()
    {
        var result = ExecJs(@"
            var t = document.getElementById('a').firstChild;
            try { t.substringData(99, 1); }
            catch (e) { document.getElementById('result').textContent = 'V:' + (e instanceof Error) + ',' + (typeof e.message); }
        ");
        Assert.Contains("V:true,string", result);
    }

    // Nothing about the in-range behaviour changed, including the offset == length boundary, which is
    // in range and returns the empty string rather than throwing.
    [Fact(Timeout = 600000)]
    public void In_Range_Operations_Are_Unchanged()
    {
        var result = ExecJs(@"
            var d = document.getElementById('a');
            var parts = [];
            parts.push(d.firstChild.substringData(0, 3));           // hel
            parts.push('[' + d.firstChild.substringData(5, 2) + ']'); // boundary: offset == length
            d.firstChild.appendData('!');       parts.push(d.firstChild.data);
            d.firstChild.insertData(0, '>');    parts.push(d.firstChild.data);
            d.firstChild.deleteData(0, 1);      parts.push(d.firstChild.data);
            d.firstChild.replaceData(0, 1, 'H');parts.push(d.firstChild.data);
            document.getElementById('result').textContent = 'V:' + parts.join(',');
        ");
        Assert.Contains("V:hel,[],hello!,&gt;hello!,hello!,Hello!", result);
    }

    [Fact(Timeout = 600000)]
    public void SplitText_Still_Splits_In_Range()
    {
        var result = ExecJs(@"
            var t = document.getElementById('a').firstChild;
            var n = t.splitText(2);
            document.getElementById('result').textContent = 'V:' + t.data + '|' + n.data;
        ");
        Assert.Contains("V:he|llo", result);
    }
}
