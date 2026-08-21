using Broiler.Code.Core.Diagnostics;
using Broiler.Code.Workspaces.Text;
using Broiler.UI.CodeEditor;

namespace Broiler.Code.Core.Tests;

public sealed class DiagnosticsTests
{
    private static MergedDiagnostic Live(string code, int start, int version, string document = "A.cs") =>
        new(DiagnosticOrigin.Live, document,
            new CodeDiagnosticAdornment(start, 4, CodeDiagnosticSeverity.Error, $"{code} live", code),
            version);

    private static MergedDiagnostic Build(
        string code, int start, int version, string buildId, string document = "A.cs") =>
        new(DiagnosticOrigin.Build, document,
            new CodeDiagnosticAdornment(start, 4, CodeDiagnosticSeverity.Error, $"{code} build", code),
            version, buildId);

    [Fact(Timeout = 600000)]
    public void An_Older_Build_Cannot_Replace_Newer_Live_Results()
    {
        var merge = new DocumentDiagnosticMerge();

        // A build started three edits ago finishes now.
        Assert.True(merge.TryApplyLive(10, [Live("CS0103", 0, 10)]));
        Assert.True(merge.TryApplyBuild("build-1", 7, [Build("CS7036", 100, 7, "build-1")]));

        Assert.True(merge.BuildIsStale);

        // Its spans describe text that has moved, so they are not shown. The
        // alternative is resurrecting an error at a position the user has
        // already edited past.
        IReadOnlyList<MergedDiagnostic> merged = merge.GetMerged();
        Assert.Single(merged);
        Assert.Equal(DiagnosticOrigin.Live, merged[0].Origin);
    }

    [Fact(Timeout = 600000)]
    public void A_Current_Build_Contributes_What_Live_Did_Not_Find()
    {
        var merge = new DocumentDiagnosticMerge();
        merge.TryApplyLive(10, [Live("CS0103", 0, 10)]);

        // The build compiled the same snapshot, and found something the live
        // service cannot see — an analyzer result, say.
        merge.TryApplyBuild("build-2", 10, [Build("CA1510", 200, 10, "build-2")]);

        Assert.False(merge.BuildIsStale);
        Assert.Equal(2, merge.GetMerged().Count);
    }

    [Fact(Timeout = 600000)]
    public void The_Same_Diagnostic_From_Both_Origins_Appears_Once()
    {
        var merge = new DocumentDiagnosticMerge();
        merge.TryApplyLive(5, [Live("CS7036", 42, 5)]);
        merge.TryApplyBuild("build-3", 5, [Build("CS7036", 42, 5, "build-3")]);

        // Identity is code, document, and span — never the message, which
        // differs because the two run different compiler builds.
        MergedDiagnostic single = Assert.Single(merge.GetMerged());
        Assert.Equal(DiagnosticOrigin.Live, single.Origin);
    }

    [Fact(Timeout = 600000)]
    public void Live_Results_For_An_Older_Snapshot_Are_Refused()
    {
        var merge = new DocumentDiagnosticMerge();
        Assert.True(merge.TryApplyLive(10, [Live("CS0103", 0, 10)]));

        // Analysis runs concurrently and completes out of order.
        Assert.False(merge.TryApplyLive(9, [Live("CS0246", 0, 9)]));
        Assert.Equal("CS0103", Assert.Single(merge.GetMerged()).Adornment.Code);
    }

    [Fact(Timeout = 600000)]
    public void An_Older_Build_Finishing_Last_Does_Not_Win()
    {
        var merge = new DocumentDiagnosticMerge();
        Assert.True(merge.TryApplyBuild("newer", 10, [Build("CS1", 0, 10, "newer")]));
        Assert.False(merge.TryApplyBuild("older", 8, [Build("CS2", 0, 8, "older")]));

        Assert.Equal("newer", merge.BuildId);
    }

    [Fact(Timeout = 600000)]
    public void A_Project_Level_Diagnostic_Survives_A_Stale_Build()
    {
        var merge = new DocumentDiagnosticMerge();
        merge.TryApplyLive(10, []);

        // Zero length: no source span, so nothing to rebase and nothing to get
        // wrong. "The SDK is missing" must not vanish because the user typed.
        var toolchain = new MergedDiagnostic(
            DiagnosticOrigin.Build, "p.csproj",
            new CodeDiagnosticAdornment(0, 0, CodeDiagnosticSeverity.Error, "SDK not found", "NETSDK1045"),
            3, "build-4");
        merge.TryApplyBuild("build-4", 3, [toolchain]);

        Assert.True(merge.BuildIsStale);
        Assert.Equal("NETSDK1045", Assert.Single(merge.GetMerged()).Adornment.Code);
    }

    [Fact(Timeout = 600000)]
    public void Counts_Ignore_The_Filter_So_Hiding_Warnings_Is_Not_A_Clean_Bill()
    {
        var snapshot = new SourceBufferDocument(new SourceBuffer("one\ntwo\nthree\n")).Snapshot;
        var model = new ProblemsModel { Mode = CodeAnalysisMode.LiveSemantic };

        model.SetDocumentEntries("A.cs", ProblemsModel.ToEntries(
        [
            Live("CS1", 0, 1),
            new MergedDiagnostic(DiagnosticOrigin.Live, "A.cs",
                new CodeDiagnosticAdornment(4, 3, CodeDiagnosticSeverity.Warning, "w", "CS2"), 1),
            new MergedDiagnostic(DiagnosticOrigin.Live, "A.cs",
                new CodeDiagnosticAdornment(8, 5, CodeDiagnosticSeverity.Information, "i", "CS3"), 1),
        ], snapshot));

        model.Filter = new ProblemFilter(ShowWarnings: false, ShowInformation: false);

        Assert.Single(model.GetVisible());
        Assert.Equal(new ProblemCounts(1, 1, 1), model.Counts);
    }

    [Fact(Timeout = 600000)]
    public void The_Status_Line_States_The_Analysis_Mode()
    {
        var counts = new ProblemCounts(0, 0, 0);

        // Zero errors means something different when nothing has looked.
        Assert.Contains("classification only", counts.Describe(CodeAnalysisMode.ClassificationOnly), StringComparison.Ordinal);
        Assert.Contains("last build", counts.Describe(CodeAnalysisMode.BuildDiagnostics), StringComparison.Ordinal);
        Assert.EndsWith("information", counts.Describe(CodeAnalysisMode.LiveSemantic), StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void Entries_Resolve_To_The_Line_And_Column_Of_Their_Own_Snapshot()
    {
        var snapshot = new SourceBufferDocument(new SourceBuffer("alpha\nbeta\ngamma\n")).Snapshot;

        ProblemEntry entry = ProblemsModel.ToEntries([Live("CS1", 8, 1)], snapshot).Single();

        // Offset 8 is on line 1 ("beta"), column 2.
        Assert.Equal(1, entry.Line);
        Assert.Equal(2, entry.Column);
        Assert.Contains("line 2, column 3", entry.AccessibleDescription, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void A_Project_Level_Entry_Has_No_Position_And_Still_Lists()
    {
        var model = new ProblemsModel();
        model.SetProjectEntries([
            ProblemsModel.ProjectEntry("p.csproj", "BRC1000", "This workspace has not been trusted."),
        ]);

        ProblemEntry entry = Assert.Single(model.GetVisible());
        Assert.True(entry.IsProjectLevel);
        Assert.DoesNotContain("line", entry.AccessibleDescription, StringComparison.Ordinal);
    }

    [Fact(Timeout = 600000)]
    public void Replacing_One_Documents_Entries_Leaves_The_Others_Alone()
    {
        var snapshot = new SourceBufferDocument(new SourceBuffer("a\nb\n")).Snapshot;
        var model = new ProblemsModel();

        model.SetDocumentEntries("A.cs", ProblemsModel.ToEntries([Live("CS1", 0, 1, "A.cs")], snapshot));
        model.SetDocumentEntries("B.cs", ProblemsModel.ToEntries([Live("CS2", 0, 1, "B.cs")], snapshot));
        model.SetDocumentEntries("A.cs", ProblemsModel.ToEntries([Live("CS3", 0, 1, "A.cs")], snapshot));

        Assert.Equal(["CS2", "CS3"], model.GetVisible().Select(e => e.Code).Order());
    }

    [Fact(Timeout = 600000)]
    public void Grouping_And_Search_Narrow_The_List()
    {
        var snapshot = new SourceBufferDocument(new SourceBuffer("a\nb\n")).Snapshot;
        var model = new ProblemsModel();
        model.SetDocumentEntries("A.cs", ProblemsModel.ToEntries([Live("CS7036", 0, 1, "A.cs")], snapshot));
        model.SetDocumentEntries("B.cs", ProblemsModel.ToEntries([Live("CS0103", 0, 1, "B.cs")], snapshot));

        Assert.Equal(2, model.GetGroupedByDocument().Count);

        // Searching by code, which is what users actually type.
        model.Filter = new ProblemFilter(SearchText: "7036");
        Assert.Equal("A.cs", Assert.Single(model.GetVisible()).DocumentPath);
    }
}
