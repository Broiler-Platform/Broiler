namespace Broiler.Wpt.Tests;

public sealed class ExceptionSignatureTests
{
    [Fact]
    public void TryCompute_Joins_Top_Frame_And_Stripped_Message()
    {
        const string message = "Script execution failed: A prefixed name requires a namespace URI";
        var stackTrace = string.Join('\n', new[]
        {
            "   at Broiler.DOM.DomName..ctor(String name) in D:\\Broiler\\Broiler.DOM\\DomName.cs:line 42",
            "   at Broiler.DOM.DomElement.SetAttribute(String name, String value)",
            "   at System.RuntimeMethodHandle.InvokeMethod(Object target)",
        });

        Assert.Equal(
            "DomName..ctor — A prefixed name requires a namespace URI",
            ExceptionSignature.TryCompute(message, stackTrace));
    }

    [Fact]
    public void TryCompute_Skips_Framework_Frames_To_Find_The_Throw_Site()
    {
        const string message = "Rendering failed: boom";
        var stackTrace = string.Join('\n', new[]
        {
            "   at System.ThrowHelper.ThrowArgumentOutOfRangeException()",
            "   at Microsoft.Something.Internal()",
            "   at Broiler.Layout.CssBox.Measure(Single width)",
        });

        Assert.Equal("CssBox.Measure — boom", ExceptionSignature.TryCompute(message, stackTrace));
    }

    [Fact]
    public void TryCompute_Returns_Null_When_Nothing_Usable()
    {
        Assert.Null(ExceptionSignature.TryCompute(null, null));
        Assert.Null(ExceptionSignature.TryCompute("   ", ""));
    }

    [Fact]
    public void TryCompute_Falls_Back_To_Message_When_No_Application_Frame()
    {
        const string stackTrace = "   at System.IO.File.ReadAllText(String path)";
        Assert.Equal("disk gone", ExceptionSignature.TryCompute("Failed to read test file: disk gone", stackTrace));
    }

    [Fact]
    public void Buckets_Groups_By_Signature_Most_Frequent_First()
    {
        var crash = string.Join('\n', new[]
        {
            "   at Broiler.DOM.DomName..ctor(String name)",
            "   at Broiler.DOM.DomElement.SetAttribute(String n, String v)",
        });
        var other = "   at Broiler.Layout.CssBox.Measure(Single w)";

        var failures = new[]
        {
            Failure(FailureCategory.ScriptError, "Script execution failed: bad name", crash, "/tmp/wpt/a.html"),
            Failure(FailureCategory.ScriptError, "Script execution failed: bad name", crash, "/tmp/wpt/b.html"),
            Failure(FailureCategory.RenderingError, "Rendering failed: overflow", other, "/tmp/wpt/c.html"),
            // No stack trace → excluded (e.g. a pixel mismatch).
            Failure(FailureCategory.PixelMismatch, "mismatch", null),
            // Timeout → excluded (reported in the timeout section).
            Failure(FailureCategory.Timeout, "Timed out", "   at Broiler.Wpt.Program.Run()"),
        };

        var buckets = ExceptionSignature.Buckets(failures, limit: 10);

        Assert.Equal(2, buckets.Count);
        Assert.Equal("DomName..ctor — bad name", buckets[0].Signature);
        Assert.Equal(2, buckets[0].Count);
        // Each signature carries the test paths that hit it, for reproduction.
        Assert.Equal(new[] { "/tmp/wpt/a.html", "/tmp/wpt/b.html" }, buckets[0].Examples);
        Assert.Equal("CssBox.Measure — overflow", buckets[1].Signature);
        Assert.Equal(1, buckets[1].Count);
        Assert.Equal(new[] { "/tmp/wpt/c.html" }, buckets[1].Examples);
    }

    [Fact]
    public void Buckets_Collects_Distinct_Example_Paths_Up_To_A_Cap()
    {
        const string trace = "   at Broiler.DOM.DomName..ctor(String name)";
        var failures = new[]
        {
            Failure(FailureCategory.ScriptError, "boom", trace, "/tmp/wpt/one.html"),
            // Same path repeats — must not be listed twice.
            Failure(FailureCategory.ScriptError, "boom", trace, "/tmp/wpt/one.html"),
            Failure(FailureCategory.ScriptError, "boom", trace, "/tmp/wpt/two.html"),
            Failure(FailureCategory.ScriptError, "boom", trace, "/tmp/wpt/three.html"),
            // Beyond the 3-example cap — counted, but not retained as an example.
            Failure(FailureCategory.ScriptError, "boom", trace, "/tmp/wpt/four.html"),
        };

        var bucket = Assert.Single(ExceptionSignature.Buckets(failures, limit: 10));
        Assert.Equal(5, bucket.Count);
        Assert.Equal(
            new[] { "/tmp/wpt/one.html", "/tmp/wpt/two.html", "/tmp/wpt/three.html" },
            bucket.Examples);
    }

    [Fact]
    public void Buckets_Respects_Limit()
    {
        var failures = new[]
        {
            Failure(FailureCategory.ScriptError, "a", "   at App.One.Go()"),
            Failure(FailureCategory.ScriptError, "b", "   at App.Two.Go()"),
        };

        Assert.Single(ExceptionSignature.Buckets(failures, limit: 1));
    }

    private static WptTestResult Failure(
        FailureCategory category, string message, string? stackTrace,
        string testPath = "/tmp/wpt/test.html") =>
        new()
        {
            TestPath = testPath,
            Passed = false,
            Category = category,
            Message = message,
            StackTrace = stackTrace,
        };
}
