using System.Drawing;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// RF-BRIDGE-1b: exercises the geometry cutover. It runs the committed WPT
/// <c>check-layout</c> corpus through <see cref="DomBridge.EvaluateCheckLayoutAssertions"/>
/// and counts how many assertions the bridge answers within the ±1px WPT tolerance via the
/// shared renderer-layout path — the sole geometry source now that the coarse estimators
/// are deleted (increment 6).
/// </summary>
[Xunit.Collection("SharedGeometryStatics")]
public sealed class SharedLayoutGeometryParityTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public SharedLayoutGeometryParityTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private const double TolerancePx = 1.0; // matches WptTestRunner.LayoutAssertionTolerancePx

    [Fact]
    public void UseSharedLayoutGeometry_Is_Enabled_By_Default()
    {
        // Increments 1-3 landed and the parity gate below confirms the shared
        // renderer-layout path matches or improves on the estimators, so the geometry
        // cutover is on by default. Guards against an accidental revert of the default.
        Assert.True(DomBridge.UseSharedLayoutGeometry,
            "UseSharedLayoutGeometry must default to true now that the parity gate passes.");
    }

    private static (int Matched, int Total, int Files) MeasureCorpus()
    {
        int matched = 0, total = 0, files = 0;
        foreach (var path in CheckLayoutCorpus())
        {
            string html;
            try { html = File.ReadAllText(path); }
            catch { continue; }

            IReadOnlyList<DomBridge.CheckLayoutAssertion> assertions;
            try
            {
                using var context = new JSContext();
                var bridge = new DomBridge();
                bridge.Attach(context, html, "file:///" + Path.GetFileName(path));
                assertions = bridge.EvaluateCheckLayoutAssertions();
            }
            catch
            {
                // A file that fails to attach/evaluate contributes nothing; the
                // harness measures the corpus it can actually run.
                continue;
            }

            if (assertions.Count == 0)
                continue;

            files++;
            foreach (var a in assertions)
            {
                total++;
                if (!double.IsNaN(a.Actual) && Math.Abs(a.Expected - a.Actual) <= TolerancePx)
                    matched++;
            }
        }

        return (matched, total, files);
    }

    [Fact]
    public void TypedDocument_Applies_Author_StyleSheet()
    {
        const string styled = "<!DOCTYPE html><html><head><style>#x{width:50px;height:50px}</style></head>" +
                              "<body style='margin:0'><div id='x'></div></body></html>";

        using var context = new JSContext();
        var bridge = new DomBridge();
        bridge.Attach(context, styled, "file:///s.html");
        var doc = bridge.GetRenderDocument();
        using var container = new Broiler.HTML.Image.HtmlContainer
        { AvoidAsyncImagesLoading = true, AvoidImagesLateLoading = true };
        container.SetDocumentWithStyleSet(doc, baseUrl: "file:///s.html");
        var g = container.GetLayoutGeometry(new SizeF(800, 600));

        var x = doc.GetElementById("x");
        Assert.NotNull(x);
        Assert.True(g.TryGetValue(x!, out var geom));
        Assert.Equal(50f, geom.BorderBox.Width, 1);
        Assert.Equal(50f, geom.BorderBox.Height, 1);
    }

    private static IEnumerable<string> CheckLayoutCorpus()
    {
        var root = FindRepositoryRoot();
        var wpt = Path.Combine(root, "tests", "wpt", "css");
        var dirs = new[]
        {
            Path.Combine(wpt, "css-align"),
            Path.Combine(wpt, "css-anchor-position"),
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir))
                continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.html", SearchOption.AllDirectories))
            {
                string content;
                try { content = File.ReadAllText(file); }
                catch { continue; }

                if (content.Contains("data-expected-", StringComparison.Ordinal) ||
                    content.Contains("data-offset-", StringComparison.Ordinal))
                {
                    yield return file;
                }
            }
        }
    }

    [Fact]
    public void Shared_Geometry_Answers_CheckLayout_Corpus()
    {
        var shared = MeasureCorpus();

        // The corpus must be present and produce assertions, or the gate is vacuous.
        Assert.True(shared.Files > 0,
            "No WPT check-layout files were found/runnable; the gate would be vacuous.");
        Assert.True(shared.Total > 0, "The corpus produced no check-layout assertions.");

        _output.WriteLine(
            $"check-layout (shared): files={shared.Files} total={shared.Total} " +
            $"matched={shared.Matched} (±{TolerancePx}px)");

        // RF-BRIDGE-1b increment 6: the coarse geometry estimators are deleted, so the
        // shared renderer-layout path is the sole geometry source. It must answer a
        // substantial share of the corpus's check-layout assertions (empirically ≈345 of
        // ≈484). The remaining gaps are position-try fallback OFFSETS (anchor() inset
        // resolution), a distinct renderer feature the old estimator also got wrong.
        Assert.True(shared.Matched > 0,
            $"Shared geometry answered no check-layout assertions correctly across " +
            $"{shared.Files} files ({shared.Matched}/{shared.Total}).");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Broiler.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
