using System;
using System.Collections.Generic;
using System.IO;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Scripting;
using Broiler.HtmlBridge.Internal.Scripting;
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

    /// <summary>Links the graph over <paramref name="files"/>, then evaluates the dependency programs and
    /// drives the last (entry) program through <see cref="JSContext.Execute"/> so the event loop pumps the
    /// microtask that settles a dynamic <c>import()</c>; returns the context for assertions.</summary>
    private static JSContext RunGraphDriving(
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
        for (int i = 0; i < result.Programs.Count - 1; i++)
            ctx.Eval(result.Programs[i]);
        ctx.Execute(result.Programs[^1]);
        return ctx;
    }

    [Fact]
    public void Dynamic_Import_Resolves_To_The_Linked_Module_Namespace()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/util.mjs"] = "export const answer = 42;",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "globalThis.out = 'pending';\n" +
                "import('./util.mjs').then(function (m) { globalThis.out = m.answer; });",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraphDriving(entries, files);
        Assert.Equal("42", Eval(ctx, "''+globalThis.out"));
    }

    [Fact]
    public void Dynamic_Import_Of_Unresolvable_Module_Rejects()
    {
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "globalThis.state = 'pending';\n" +
                "import('./missing.mjs').then(\n" +
                "  function () { globalThis.state = 'resolved'; },\n" +
                "  function () { globalThis.state = 'rejected'; });",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraphDriving(entries, new Dictionary<string, string>());
        Assert.Equal("rejected", Eval(ctx, "globalThis.state"));
    }

    [Fact]
    public void Dynamic_Import_Shares_One_Instance_With_A_Static_Import()
    {
        // util is imported statically (main) and dynamically (main); both must see the same singleton
        // instance from the module registry, so a mutation through one is visible through the other.
        var files = new Dictionary<string, string>
        {
            ["file:///app/util.mjs"] =
                "export const tag = {}; globalThis.evalCount = (globalThis.evalCount || 0) + 1;",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { tag } from './util.mjs';\n" +
                "import('./util.mjs').then(function (m) { globalThis.same = (m.tag === tag); });",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraphDriving(entries, files);
        Assert.Equal("true", Eval(ctx, "''+globalThis.same"));
        Assert.Equal("1", Eval(ctx, "''+globalThis.evalCount")); // evaluated exactly once
    }

    // ── scope-accurate live named bindings (EsModuleLiveRefs) ─────────────────────────────────

    [Fact]
    public void Named_Import_Stays_Live_Through_A_Module_That_Has_Local_Functions()
    {
        // The lift: a consumer with its own function + params (that do NOT shadow the imports) must still
        // get live named bindings — the old rewriter aborted the whole module on any `function`.
        var files = new Dictionary<string, string>
        {
            ["file:///app/counter.mjs"] =
                "export let count = 0;\n" +
                "export function bump() { count += 5; }",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { count, bump } from './counter.mjs';\n" +
                "function helper(n) { return n + 1; }\n" +
                "bump();\n" +
                "globalThis.out = helper(count);",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("6", Eval(ctx, "''+globalThis.out")); // count live → 5, helper(5) → 6 (snapshot would give 1)
    }

    [Fact]
    public void A_Function_Parameter_Shadow_Is_Not_Rewritten()
    {
        var files = new Dictionary<string, string> { ["file:///app/lib.mjs"] = "export let v = 100;" };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { v } from './lib.mjs';\n" +
                "function f(v) { return v; }\n" +      // param v shadows the import
                "globalThis.shadowed = f(7);\n" +      // must be 7, not the import 100
                "globalThis.top = v;",                 // unshadowed → import → 100
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("7", Eval(ctx, "''+globalThis.shadowed"));
        Assert.Equal("100", Eval(ctx, "''+globalThis.top"));
    }

    [Fact]
    public void An_Arrow_Parameter_Shadow_Is_Not_Rewritten()
    {
        var files = new Dictionary<string, string> { ["file:///app/lib.mjs"] = "export let v = 100;" };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { v } from './lib.mjs';\n" +
                "var f = (v) => v;\n" +   // multi-form arrow param
                "var g = v => v + 1;\n" + // single-form arrow param
                "globalThis.a = f(3); globalThis.b = g(4); globalThis.top = v;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("3", Eval(ctx, "''+globalThis.a"));
        Assert.Equal("5", Eval(ctx, "''+globalThis.b"));
        Assert.Equal("100", Eval(ctx, "''+globalThis.top"));
    }

    [Fact]
    public void A_Block_Scoped_Let_Shadow_Is_Not_Rewritten()
    {
        var files = new Dictionary<string, string> { ["file:///app/lib.mjs"] = "export let v = 100;" };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { v } from './lib.mjs';\n" +
                "var out = 0;\n" +
                "{ let v = 9; out = v; }\n" +   // block-scoped shadow
                "globalThis.blocked = out; globalThis.top = v;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("9", Eval(ctx, "''+globalThis.blocked"));
        Assert.Equal("100", Eval(ctx, "''+globalThis.top"));
    }

    [Fact]
    public void A_Destructuring_Binding_Shadow_Is_Not_Rewritten()
    {
        var files = new Dictionary<string, string> { ["file:///app/lib.mjs"] = "export let v = 100;" };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { v } from './lib.mjs';\n" +
                "function f() { var { v } = { v: 7 }; return v; }\n" +
                "globalThis.d = f(); globalThis.top = v;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("7", Eval(ctx, "''+globalThis.d"));
        Assert.Equal("100", Eval(ctx, "''+globalThis.top"));
    }

    [Fact]
    public void A_Catch_Parameter_Shadow_Is_Not_Rewritten()
    {
        var files = new Dictionary<string, string> { ["file:///app/lib.mjs"] = "export let v = 100;" };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { v } from './lib.mjs';\n" +
                "var r; try { throw 5; } catch (v) { r = v; }\n" +
                "globalThis.caught = r; globalThis.top = v;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("5", Eval(ctx, "''+globalThis.caught"));
        Assert.Equal("100", Eval(ctx, "''+globalThis.top"));
    }

    [Fact]
    public void A_Module_With_A_Class_Falls_Back_To_A_Correct_Snapshot()
    {
        var files = new Dictionary<string, string> { ["file:///app/lib.mjs"] = "export let v = 100;" };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { v } from './lib.mjs';\n" +
                "class C { m() { return 1; } }\n" +
                "globalThis.out = v + new C().m();",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("101", Eval(ctx, "''+globalThis.out")); // snapshot v=100 (+1); module not corrupted
    }

    [Fact]
    public void Named_Import_Is_A_Live_Binding_Not_A_Snapshot()
    {
        // The canonical live-binding case: an exported counter mutated by an exported function must be
        // observed through a *named* import, not frozen at import time.
        var files = new Dictionary<string, string>
        {
            ["file:///app/counter.mjs"] =
                "export let count = 0;\n" +
                "export function increment() { count++; }",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { count, increment } from './counter.mjs';\n" +
                "increment(); increment();\n" +
                "globalThis.out = count;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("2", Eval(ctx, "''+globalThis.out")); // live: 2 (a snapshot would read 0)
    }

    [Fact]
    public void Namespace_Import_Reflects_Post_Evaluation_Mutation()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/counter.mjs"] =
                "export let count = 0;\n" +
                "export function increment() { count++; }",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import * as c from './counter.mjs';\n" +
                "c.increment();\n" +
                "globalThis.out = c.count;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("1", Eval(ctx, "''+globalThis.out"));
    }

    [Fact]
    public void Named_Import_Live_Read_Works_Through_A_Call()
    {
        var files = new Dictionary<string, string>
        {
            ["file:///app/util.mjs"] = "export function twice(x) { return x * 2; }",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { twice } from './util.mjs'; globalThis.out = twice(21);",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("42", Eval(ctx, "''+globalThis.out"));
    }

    [Fact]
    public void Named_Import_Read_Through_A_Local_Function_Call_Is_Handled()
    {
        // A non-shadowing local function whose parameter is unrelated to the import: the scope-accurate
        // rewriter keeps the import live and does not confuse the parameter `x` with the import `answer`.
        var files = new Dictionary<string, string>
        {
            ["file:///app/util.mjs"] = "export const answer = 42;",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { answer } from './util.mjs';\n" +
                "function id(x) { return x; }\n" +
                "globalThis.out = id(answer);",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("42", Eval(ctx, "''+globalThis.out"));
    }

    [Fact]
    public void Live_Rewrite_Does_Not_Corrupt_An_Object_Key_Matching_An_Import_Name()
    {
        // `{ answer: 5 }` has a *literal key* that collides with the import name; the rewriter must not turn
        // it into a computed key. (It aborts the module's live rewrite on the ambiguity — snapshot stands.)
        var files = new Dictionary<string, string>
        {
            ["file:///app/util.mjs"] = "export const answer = 42;",
        };
        var entries = new List<ModuleGraphLoader.GraphModule>
        {
            new("file:///app/main.mjs",
                "import { answer } from './util.mjs';\n" +
                "var o = { answer: 5 };\n" +
                "globalThis.key = o.answer;\n" +
                "globalThis.imp = answer;",
                "file:///app/main.mjs"),
        };

        using var ctx = RunGraph(entries, files);
        Assert.Equal("5", Eval(ctx, "''+globalThis.key"));  // literal key untouched
        Assert.Equal("42", Eval(ctx, "''+globalThis.imp")); // import still bound (snapshot)
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
