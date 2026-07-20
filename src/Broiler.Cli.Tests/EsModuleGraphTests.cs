using System;
using System.Collections.Generic;
using System.IO;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;
using Broiler.JavaScript.Engine;

namespace Broiler.Cli.Tests;

/// <summary>
/// Phase 7 item 6: end-to-end tests for the ES module graph — <see cref="EsModuleScanner"/> +
/// <see cref="EsModuleLinker"/> + <see cref="ModuleGraphLoader"/> — driven through a real
/// <see cref="JSContext"/>. Each test builds a small module graph from in-memory sources, links it, evals
/// the resulting programs in dependency order, and asserts on the observable <c>globalThis</c> state, so
/// the actual import/export wiring (not just the string transform) is verified.
/// </summary>
public sealed class EsModuleGraphTests
{
    /// <summary>Links the graph rooted at <paramref name="entries"/> over the in-memory <paramref name="files"/>
    /// and evaluates every program in order in a fresh context; returns the context for assertions.</summary>
    private static JSContext RunGraph(
        List<ModuleGraphLoader.GraphModule> entries,
        Dictionary<string, string> files)
    {
        ModuleGraphLoader.ResolveAndFetch fetch = (specifier, baseUrl) =>
        {
            var resolved = UrlResolver.Resolve(specifier, baseUrl);
            if (resolved == null) return null;
            var key = resolved.AbsoluteUri;
            return files.TryGetValue(key, out var src) ? (key, src) : null;
        };

        var result = ModuleGraphLoader.Load(entries, fetch);
        var ctx = new JSContext();
        foreach (var program in result.Programs)
            ctx.Eval(program);
        return ctx;
    }

    private static string Eval(JSContext ctx, string expr) => ctx.Eval(expr).ToString();

    [Fact]
    public void Named_Import_Export_Across_Two_Modules()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/util.mjs"] = "export const answer = 42; export function twice(x){ return x*2; }",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { answer, twice } from './util.mjs'; globalThis.out = answer + twice(4);",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("50", Eval(ctx, "globalThis.out")); // 42 + 8
    }

    [Fact]
    public void Default_Import_And_Export()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/greet.mjs"] = "export default function(name){ return 'hi ' + name; }",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import greet from './greet.mjs'; globalThis.msg = greet('bo');",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("hi bo", Eval(ctx, "globalThis.msg"));
    }

    [Fact]
    public void Namespace_Import_Binds_Live_Exports_Object()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/lib.mjs"] = "export const a = 1; export const b = 2;",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import * as lib from './lib.mjs'; globalThis.sum = lib.a + lib.b;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("3", Eval(ctx, "globalThis.sum"));
    }

    [Fact]
    public void Aliased_Named_Import_And_Export()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/m.mjs"] = "const internal = 7; export { internal as value };",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { value as v } from './m.mjs'; globalThis.v = v;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("7", Eval(ctx, "globalThis.v"));
    }

    [Fact]
    public void ReExport_Named_And_Star()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/a.mjs"] = "export const x = 10; export const y = 20;",
            ["file:///app/b.mjs"] = "export { x } from './a.mjs'; export * from './a.mjs';",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { x, y } from './b.mjs'; globalThis.xy = x + '/' + y;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("10/20", Eval(ctx, "globalThis.xy"));
    }

    [Fact]
    public void Diamond_Dependency_Evaluates_Shared_Module_Once()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/base.mjs"] = "globalThis.baseEvals = (globalThis.baseEvals||0)+1; export const v = 5;",
            ["file:///app/left.mjs"] = "import { v } from './base.mjs'; export const l = v + 1;",
            ["file:///app/right.mjs"] = "import { v } from './base.mjs'; export const r = v + 2;",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { l } from './left.mjs'; import { r } from './right.mjs'; globalThis.lr = l + r;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("13", Eval(ctx, "globalThis.lr")); // (5+1)+(5+2)
        Assert.Equal("1", Eval(ctx, "globalThis.baseEvals")); // base evaluated exactly once
    }

    [Fact]
    public void Circular_Import_Does_Not_Crash()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/a.mjs"] = "import { fromB } from './b.mjs'; export const fromA = 'A'; globalThis.aSawB = typeof fromB;",
            ["file:///app/b.mjs"] = "import { fromA } from './a.mjs'; export const fromB = 'B'; globalThis.bSawA = typeof fromA;",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/a.mjs", files["file:///app/a.mjs"], "file:///app/a.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        // One side of the cycle sees the other's export populated; the back-edge side sees undefined
        // (snapshot semantics). Either way, no crash and both modules ran.
        Assert.Contains(Eval(ctx, "globalThis.aSawB"), new[] { "string", "undefined" });
        Assert.Contains(Eval(ctx, "globalThis.bSawA"), new[] { "string", "undefined" });
    }

    [Fact]
    public void SideEffect_Import_Runs_Dependency()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/fx.mjs"] = "globalThis.sideEffect = 'done';",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs", "import './fx.mjs'; globalThis.after = globalThis.sideEffect;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("done", Eval(ctx, "globalThis.after"));
    }

    [Fact]
    public void Module_Top_Level_Declarations_Do_Not_Leak_To_Global()
    {
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs", "const secret = 99; export const shown = 1; globalThis.check = typeof secret;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, new Dictionary<string, string>());
        Assert.Equal("number", Eval(ctx, "globalThis.check")); // visible inside the module
        Assert.Equal("undefined", Eval(ctx, "typeof secret"));  // not leaked to global
    }

    // ── scanner robustness ──────────────────────────────────────────────────

    [Fact]
    public void Import_Export_Keywords_Inside_Strings_And_Comments_Are_Not_Module_Syntax()
    {
        var syntax = EsModuleScanner.Scan(
            "const s = 'import x from \\'y\\''; // export const z = 1\n" +
            "/* import a from 'b' */ const exports_like = importScripts;");
        Assert.True(syntax.Supported);
        Assert.False(syntax.HasModuleSyntax); // nothing real to transform
    }

    [Fact]
    public void Dynamic_Import_And_Import_Meta_Are_Left_As_Expressions()
    {
        var syntax = EsModuleScanner.Scan(
            "const p = import('./x.mjs'); const u = import.meta.url; export const ok = 1;");
        Assert.True(syntax.Supported);
        Assert.Empty(syntax.Imports);          // import(...) / import.meta are not import statements
        Assert.Single(syntax.Exports);         // only the real export was recorded
    }

    [Fact]
    public void Destructuring_Export_Is_Unsupported_And_Falls_Back()
    {
        var syntax = EsModuleScanner.Scan("export const { a, b } = obj;");
        Assert.False(syntax.Supported);
    }

    [Fact]
    public void Real_Import_Adjacent_To_A_String_Mentioning_Import_Is_Transformed()
    {
        var files = new Dictionary<string, string> { ["file:///app/d.mjs"] = "export const val = 3;" };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { val } from './d.mjs'; globalThis.note = 'use import wisely'; globalThis.val = val;",
                "file:///app/main.mjs"),
        };
        using var ctx = RunGraph(entries, files);
        Assert.Equal("3", Eval(ctx, "globalThis.val"));
        Assert.Equal("use import wisely", Eval(ctx, "globalThis.note"));
    }

    // ── full ExtractAll integration ─────────────────────────────────────────

    [Fact]
    public void ExtractAll_Links_An_Inline_Module_Import_Graph_From_Disk()
    {
        var dir = Path.Combine(Path.GetTempPath(), "brmod_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "util.mjs"),
                "import { base } from './base.mjs'; export const answer = base + 2;");
            File.WriteAllText(Path.Combine(dir, "base.mjs"), "export const base = 40;");
            var pageUrl = new Uri(Path.Combine(dir, "index.html")).AbsoluteUri;

            const string html =
                "<script type=\"module\">import { answer } from './util.mjs'; globalThis.result = answer;</script>";

            var result = ScriptExtractionService.ExtractAll(html, pageUrl);

            // Three modules in the graph (base ← util ← inline), ordered dependency-first.
            Assert.Equal(3, result.ModuleScripts.Count);

            using var ctx = new JSContext();
            foreach (var program in result.ModuleScripts)
                ctx.Eval(program);
            Assert.Equal("42", ctx.Eval("globalThis.result").ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Unresolvable_Dependency_Renders_Runtime_Throw_Not_Silent_Undefined()
    {
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs", "import { x } from './missing.mjs'; globalThis.x = x;",
                "file:///app/main.mjs"),
        };

        // The missing module can't be fetched; the linked program throws at runtime (caught by Eval here).
        var result = ModuleGraphLoader.Load(entries, (_, _) => null);
        using var ctx = new JSContext();
        var threw = false;
        try { foreach (var p in result.Programs) ctx.Eval(p); }
        catch { threw = true; }
        Assert.True(threw);
    }

    [Fact]
    public void Import_Meta_Url_Resolves_To_The_Module_Key()
    {
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs", "globalThis.here = import.meta.url;", "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, []);
        Assert.Equal("file:///app/main.mjs", Eval(ctx, "globalThis.here"));
    }

    [Fact]
    public void Import_Meta_Works_Alongside_Static_Imports()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/util.mjs"] = "export const answer = 42;",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { answer } from './util.mjs';\n" +
                "globalThis.combo = answer + '@' + import.meta.url;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("42@file:///app/main.mjs", Eval(ctx, "globalThis.combo"));
    }

    [Fact]
    public void Import_Meta_Is_Rewritten_Inside_A_Nested_Function_Scope()
    {
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "function whereAmI(){ return import.meta.url; } globalThis.deep = whereAmI();",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, []);
        Assert.Equal("file:///app/main.mjs", Eval(ctx, "globalThis.deep"));
    }

    [Fact]
    public void Import_Meta_Text_In_A_String_Or_Comment_Is_Not_Rewritten()
    {
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "// import.meta in a comment must be ignored\n" +
                "globalThis.literal = 'import.meta'; globalThis.real = import.meta.url;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, []);
        Assert.Equal("import.meta", Eval(ctx, "globalThis.literal")); // the string literal is untouched
        Assert.Equal("file:///app/main.mjs", Eval(ctx, "globalThis.real"));
    }
}
