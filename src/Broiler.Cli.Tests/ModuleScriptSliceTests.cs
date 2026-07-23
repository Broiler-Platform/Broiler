using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 7 item 6 (module extraction): the browser module map and the authorised top-level module roots.
/// Covers <see cref="ScriptExtractionService.ExtractAll"/> populating <see cref="ModuleMap"/> and
/// <see cref="ScriptExtractionResult.ModuleRoots"/> — the sole module-execution input since the
/// string-rewriting <c>EsModuleLinker</c> fallback was retired (Phase 7 tail): an executable module yields a
/// <see cref="ModuleRoot"/> carrying its resolved source; a blocked/unresolvable module is mapped but yields
/// no root.
/// </summary>
public sealed class ModuleScriptSliceTests
{
    [Fact]
    public void Inline_Module_Is_Recorded_Executable_And_Exposed_As_A_Root()
    {
        const string html =
            "<script>var a=1;</script>" +
            "<script type=\"module\">globalThis.x = 1;</script>";
        var result = ScriptExtractionService.ExtractAll(html);

        // The inline module is executable — exposed as a root carrying its raw source — and mapped ...
        Assert.Single(result.ModuleRoots);
        Assert.Equal("globalThis.x = 1;", result.ModuleRoots[0].Source);

        Assert.Equal(1, result.ModuleMap.Count);
        var entry = result.ModuleMap.Entries[0];
        Assert.Equal(ScriptSourceKind.Inline, entry.Kind);
        Assert.True(entry.IsExecutable);
        Assert.Equal("globalThis.x = 1;", entry.Source);
        Assert.True(result.ModuleMap.TryGet(entry.Key, out var looked));
        Assert.Equal(entry, looked);

        // ... and the classic buckets/descriptors are unchanged (module stays out of Scripts).
        Assert.Single(result.Scripts);
        Assert.Contains("var a=1;", result.Scripts);
        Assert.DoesNotContain(result.Scripts, s => s.Contains("globalThis"));
    }

    [Fact]
    public void External_Module_With_Unresolvable_Url_Is_Mapped_But_Not_A_Root()
    {
        // No page URL → the relative src cannot be resolved/fetched → mapped but not executable, no root.
        const string html = "<script type=\"module\" src=\"app.mjs\"></script>";
        var result = ScriptExtractionService.ExtractAll(html);

        Assert.Empty(result.ModuleRoots);
        Assert.Equal(1, result.ModuleMap.Count);
        var entry = result.ModuleMap.Entries[0];
        Assert.Equal(ScriptSourceKind.External, entry.Kind);
        Assert.False(entry.IsExecutable);
        Assert.Null(entry.Source);
        Assert.Equal("app.mjs", entry.Url);
        Assert.Equal("app.mjs", entry.Key);
    }

    [Fact]
    public void DataUri_Module_Is_Decoded_And_Exposed_As_A_Root()
    {
        // A data: URI module is decoded through the same path as a classic data script.
        const string html = "<script type=\"module\" src=\"data:text/javascript,globalThis.y%20%3D%202%3B\"></script>";
        var result = ScriptExtractionService.ExtractAll(html);

        Assert.Single(result.ModuleRoots);
        Assert.Equal("globalThis.y = 2;", result.ModuleRoots[0].Source);
        var entry = result.ModuleMap.Entries[0];
        Assert.Equal(ScriptSourceKind.DataUri, entry.Kind);
        Assert.True(entry.IsExecutable);
        Assert.Equal("globalThis.y = 2;", entry.Source);
    }

    [Fact]
    public void External_File_Module_Is_Fetched_And_Exposed_As_A_Root()
    {
        // An external module resolvable to a local file is fetched and exposed as a root.
        var dir = Path.Combine(Path.GetTempPath(), $"broiler-mod-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var modPath = Path.Combine(dir, "m.mjs");
        File.WriteAllText(modPath, "globalThis.z = 3;");
        try
        {
            var pageUrl = new Uri(Path.Combine(dir, "index.html")).AbsoluteUri;
            var result = ScriptExtractionService.ExtractAll("<script type=\"module\" src=\"m.mjs\"></script>", pageUrl);

            Assert.Single(result.ModuleRoots);
            Assert.Equal("globalThis.z = 3;", result.ModuleRoots[0].Source);
            var entry = result.ModuleMap.Entries[0];
            Assert.Equal(ScriptSourceKind.External, entry.Kind);
            Assert.True(entry.IsExecutable);
            Assert.Equal("globalThis.z = 3;", entry.Source);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Repeated_Module_Url_Is_A_Single_Root_Module_Map_Dedup()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"broiler-mod-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "m.mjs"), "globalThis.z = 3;");
        try
        {
            var pageUrl = new Uri(Path.Combine(dir, "index.html")).AbsoluteUri;
            const string html =
                "<script type=\"module\" src=\"m.mjs\"></script>" +
                "<script type=\"module\" src=\"m.mjs\"></script>";
            var result = ScriptExtractionService.ExtractAll(html, pageUrl);

            // The module map holds the URL once and it is exposed as a single root (evaluated once) ...
            Assert.Single(result.ModuleRoots);
            Assert.Equal(1, result.ModuleMap.Count);
            // ... though both occurrences are still recorded in the per-element descriptor list.
            Assert.Equal(2, result.Descriptors.Count(d => d.IsModule));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Csp_Blocked_Inline_Module_Is_Mapped_But_Not_A_Root()
    {
        const string html =
            "<meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'none'\">" +
            "<script type=\"module\">globalThis.x = 1;</script>";
        var result = ScriptExtractionService.ExtractAll(html);

        Assert.Empty(result.ModuleRoots);
        Assert.Equal(1, result.ModuleMap.Count);
        Assert.False(result.ModuleMap.Entries[0].IsExecutable);
        Assert.Null(result.ModuleMap.Entries[0].Source);
    }

    [Fact]
    public void Document_With_No_Modules_Has_An_Empty_Map_And_No_Roots()
    {
        var result = ScriptExtractionService.ExtractAll("<script>var a=1;</script>");
        Assert.Equal(0, result.ModuleMap.Count);
        Assert.Empty(result.ModuleRoots);
    }
}
