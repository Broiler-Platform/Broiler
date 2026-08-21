namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for <c>--evaluate-page</c>: the expressions the CLI evaluates against a page once its own
/// scripts have run. The privacy test page suite reads a page's <c>results</c> global through this
/// path, so what it has to get right is the global a page's scripts left behind, in the order the
/// caller asked for, with the page's asynchronous work settled between expressions.
/// </summary>
public sealed class PageEvaluationTests
{
    private static List<PageEvaluation> Evaluate(string html, params string[] expressions)
    {
        var evaluations = new List<PageEvaluation>();
        CaptureService.ExecuteScriptsWithDom(
            html, "file:///evaluate.html", localResourceBasePath: null,
            postScriptExpressions: expressions, evaluations: evaluations);
        return evaluations;
    }

    [Fact(Timeout = 600000)]
    public void Reads_A_Global_The_Page_Declared_With_Const()
    {
        // The privacy test pages publish `const results = {...}` at the top level of a classic
        // script. That is a lexical binding rather than a property of `window`, so reading it is
        // only possible from an evaluation that shares the page's global scope.
        const string html = @"<!DOCTYPE html><html><body><script>
const results = { page: 'demo', results: [{ id: 'a', value: 1 }] };
</script></body></html>";

        var evaluation = Assert.Single(Evaluate(html, "JSON.stringify(results)"));

        Assert.Null(evaluation.Error);
        Assert.Equal("string", evaluation.Type);
        Assert.Equal("{\"page\":\"demo\",\"results\":[{\"id\":\"a\",\"value\":1}]}", evaluation.Value);
    }

    [Fact(Timeout = 600000)]
    public void Evaluates_Expressions_In_Order_And_Indexes_Them()
    {
        const string html = @"<!DOCTYPE html><html><body><script>let n = 0;</script></body></html>";

        var evaluations = Evaluate(html, "++n", "++n", "n");

        Assert.Equal([0, 1, 2], evaluations.Select(e => e.Index));
        Assert.Equal(["1", "2", "2"], evaluations.Select(e => e.Value));
    }

    [Fact(Timeout = 600000)]
    public void Settles_Async_Work_Started_By_An_Earlier_Expression()
    {
        // The pair the suite actually uses: one expression starts the page's run, the next reads
        // what it produced. Without the drain between them the second would see the initial value.
        const string html = @"<!DOCTYPE html><html><body><script>
const results = { results: [] };
function runTests () {
    setTimeout(() => { results.results.push({ id: 'late', value: 'done' }); }, 10);
}
</script></body></html>";

        var evaluations = Evaluate(html, "(runTests(), 'started')", "JSON.stringify(results)");

        Assert.Equal("started", evaluations[0].Value);
        Assert.Contains("\"value\":\"done\"", evaluations[1].Value);
    }

    [Fact(Timeout = 600000)]
    public void Reports_A_Missing_Global_As_Undefined_Rather_Than_Throwing()
    {
        const string html = "<!DOCTYPE html><html><body><script>var present = 1;</script></body></html>";

        var evaluation = Assert.Single(Evaluate(html, "typeof results === 'undefined' ? null : results"));

        Assert.Null(evaluation.Error);
        Assert.Equal("null", evaluation.Type);
        Assert.Null(evaluation.Value);
    }

    [Fact(Timeout = 600000)]
    public void Records_A_Thrown_Expression_As_An_Error_Without_Stopping_The_Rest()
    {
        const string html = "<!DOCTYPE html><html><body><script>var ok = 'fine';</script></body></html>";

        var evaluations = Evaluate(html, "missingFunction()", "ok");

        Assert.NotNull(evaluations[0].Error);
        Assert.Null(evaluations[0].Value);
        Assert.Equal("fine", evaluations[1].Value);
    }

    [Fact(Timeout = 600000)]
    public void Evaluates_Against_A_Page_That_Has_No_Scripts_Of_Its_Own()
    {
        // The early return for a script-free document must not skip the evaluation: "this page ran
        // nothing" is an answer the caller has to be able to report.
        const string html = "<!DOCTYPE html><html><body><p id=\"only\">text</p></body></html>";

        var evaluation = Assert.Single(Evaluate(html, "document.getElementById('only').textContent"));

        Assert.Null(evaluation.Error);
        Assert.Equal("text", evaluation.Value);
    }

    [Fact(Timeout = 600000)]
    public void Serializing_The_Dom_Is_Unaffected_By_An_Evaluation()
    {
        const string html = "<!DOCTYPE html><html><body><div id=\"t\"></div><script>" +
                            "document.getElementById('t').textContent = 'from script';</script></body></html>";

        var evaluations = new List<PageEvaluation>();
        var serialized = CaptureService.ExecuteScriptsWithDom(
            html, "file:///evaluate.html", localResourceBasePath: null,
            postScriptExpressions: ["1 + 1"], evaluations: evaluations);

        Assert.Equal("2", Assert.Single(evaluations).Value);
        Assert.Contains("from script", serialized);
    }
}
