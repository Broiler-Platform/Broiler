namespace Broiler.Cli.Tests;

public class DiagRegexTest
{
    [Fact]
    public void Diag_RegexEmptyCharacterClass()
    {
        var html = @"<!DOCTYPE html><html><body><div id=""result""></div><script>
var r = [];
var ok = true;
try {
    eval('/TA[])]/.exec(""TA]"")');
    ok = false;
} catch (e) {
    r.push('error1: ' + e.message);
}
r.push('unmatched_paren=' + ok);
ok = true;
try {
    var m = eval('/[]/.exec("""")');
    r.push('match_result=' + JSON.stringify(m));
    if (m)
        ok = false;
} catch (e) {
    r.push('error2: ' + e.message);
    ok = false;
}
r.push('empty_class=' + ok);
document.getElementById('result').textContent = r.join('|');
</script></body></html>";
        var result = CaptureService.ExecuteScriptsWithDom(html, "http://test/test.html");
        var match = System.Text.RegularExpressions.Regex.Match(result, @"id=""result""[^>]*>(.*?)</div>");
        Assert.True(match.Success, "No result div found");
        var output = match.Groups[1].Value;
        // Just output the result for diagnosis
        throw new Exception($"DIAG OUTPUT: {output}");
    }
}
