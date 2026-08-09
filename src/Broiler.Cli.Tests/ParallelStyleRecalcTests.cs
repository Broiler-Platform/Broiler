using System;
using System.Collections.Generic;
using System.Text;
using Broiler.HTML.Image;
using Broiler.Layout.Engine;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// The exit gate for multithreading item #12: resolving the cascade on a thread budget renders the
/// same document the sequential cascade does, at every budget.
/// </summary>
/// <remarks>
/// <para>
/// <b>Whole documents and byte comparison</b>, for the reasons <c>RasterTileParallelismTests</c>
/// gives: the roadmap's global exit gate is exact reproduction, and a cascade defect does not show
/// up as a slightly different colour — it shows up as one element out of hundreds taking the wrong
/// rule, which a tolerance would forgive.
/// </para>
/// <para>
/// <b>The documents are chosen for what makes an element's cascade depend on something other than
/// itself.</b> A page of independent elements would pass under any threading, including a broken
/// one. What the warm pass can plausibly get wrong is the shared work: the ancestor recursion two
/// threads can race (inherited values, relative font weights), custom properties that resolve
/// against an ancestor's value, and the engine's memo being read by one thread while another fills
/// it. So every document here makes an element's answer depend on an ancestor's.
/// </para>
/// <para>
/// <b>Eight threads is deliberate on a smaller machine.</b> The roadmap's gate names 1 and 8, and
/// oversubscribing the cores is what makes the ancestor recursion actually interleave rather than
/// run to completion one chunk at a time.
/// </para>
/// </remarks>
public class ParallelStyleRecalcTests
{
    private const int Width = 400;
    private const int Height = 600;

    /// <summary>
    /// Documents under test, named so a failure says which cascade feature diverged.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Documents => new Dictionary<string, string>
    {
        // Deep inheritance: every level's computed style is the previous level's input, so this is
        // the chain two threads race when they start in different subtrees of the same ancestor.
        ["inheritance chain"] = Nested(
            depth: 30,
            css: "body{color:#123;font-size:20px}div{font-size:0.95em;padding-left:1px}"
                 + "div div div{font-weight:bold}"),

        // Custom properties resolve against the ancestor chain and are cached separately from the
        // declarations that reference them.
        ["custom properties"] = Nested(
            depth: 24,
            css: "body{--tone:#204080;--step:2px}div{color:var(--tone);margin-left:var(--step)}"
                 + "div:nth-child(odd){--tone:#802040}"),

        // Relative font-weight keywords are resolved from the inherited numeric weight, which is the
        // one place the cascade reads a *parent's computed* value rather than its declarations.
        ["relative font weight"] = Nested(
            depth: 24,
            css: "body{font-weight:100}div{font-weight:bolder}div:nth-child(2n){font-weight:lighter}"),

        // Many elements against many rules — the shape item #11's rule index and item #12's warm
        // pass both target, and the only document here big enough for the warm pass's own
        // element-count threshold to be irrelevant.
        ["many rules"] = ManyRules(elements: 400, rules: 300),

        // Descendant and child combinators make matching depend on the ancestor chain in the
        // *selector* rather than in the inheritance, which is a different traversal.
        ["combinators"] = ManyRules(elements: 200, rules: 120, combinators: true),
    };

    public static TheoryData<string, int> Cases
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var name in Documents.Keys)
            {
                foreach (var threads in new[] { 2, 3, 4, 8 })
                    data.Add(name, threads);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Parallel_Style_Recalc_Renders_What_The_Sequential_Cascade_Renders(string document, int threads)
    {
        var sequential = Render(Documents[document], threads: 1);
        var parallel = Render(Documents[document], threads);

        Assert.Equal(sequential, parallel);
    }

    /// <summary>
    /// Guards the guard: a document below the warm pass's element threshold takes the sequential
    /// path whatever the budget, so a suite made only of small documents would compare the
    /// sequential render with itself and pass no matter what the warm pass did.
    /// </summary>
    [Fact]
    public void Every_Document_Is_Large_Enough_For_The_Warm_Pass_To_Run()
    {
        foreach ((string name, string html) in Documents)
        {
            // Element count is bounded below by the number of '<' that open a non-closing tag; the
            // documents are generated above, so counting the generated element markers is exact
            // enough to make the threshold assertion mean something.
            var elements = CountElements(html);
            Assert.True(
                elements >= CssStyleRecalc.MinimumParallelElements,
                $"'{name}' has {elements} element(s), below the warm pass's threshold of "
                + $"{CssStyleRecalc.MinimumParallelElements}, so its equality test proves nothing.");
        }
    }

    /// <summary>
    /// A budget of one is the code that shipped before this item, not a one-thread walk through the
    /// new one: <c>Warm</c> returns before collecting anything.
    /// </summary>
    [Fact]
    public void A_Budget_Of_One_Renders_Identically_To_The_Default_Budget()
    {
        var one = Render(Documents["many rules"], threads: 1);
        var many = Render(Documents["many rules"], threads: Environment.ProcessorCount);

        Assert.Equal(one, many);
    }

    private static byte[] Render(string html, int threads)
    {
        var previous = CssStyleRecalc.MaxDegreeOfParallelism;
        CssStyleRecalc.MaxDegreeOfParallelism = threads;
        try
        {
            using var bitmap = HtmlRender.RenderToImageWithStyleSet(
                html, Width, Height, backgroundColor: new BColor(255, 255, 255, 255));
            return bitmap.Encode();
        }
        finally
        {
            CssStyleRecalc.MaxDegreeOfParallelism = previous;
        }
    }

    private static int CountElements(string html)
    {
        var count = 0;
        for (var i = 0; i < html.Length - 1; i++)
        {
            if (html[i] == '<' && char.IsLetter(html[i + 1]))
                count++;
        }

        return count;
    }

    /// <summary>A single chain of nested divs, each carrying text so the styles are visible.</summary>
    private static string Nested(int depth, string css)
    {
        var sb = new StringBuilder(depth * 128);
        sb.Append("<html><head><style>body{margin:0;font-family:sans-serif}")
          .Append(css).Append("</style></head><body>");

        // Three sibling chains rather than one: a single chain is a straight line the warm pass
        // walks in order, where siblings are what different threads actually take at the same time.
        for (var chain = 0; chain < 3; chain++)
        {
            for (var i = 0; i < depth; i++)
                sb.Append("<div>").Append('a' + (char)(i % 26)).Append(' ');
            for (var i = 0; i < depth; i++)
                sb.Append("</div>");
        }

        return sb.Append("</body></html>").ToString();
    }

    /// <summary>Many elements against a sheet most of whose rules cannot match them.</summary>
    private static string ManyRules(int elements, int rules, bool combinators = false)
    {
        var sb = new StringBuilder(rules * 48 + elements * 64);
        sb.Append("<html><head><style>body{margin:0;font:12px sans-serif}");
        for (var i = 0; i < rules; i++)
        {
            var cls = i % 16;
            if (combinators && i % 3 == 0)
                sb.Append("section div .c").Append(cls).Append('{');
            else if (combinators && i % 3 == 1)
                sb.Append("section > div > span.c").Append(cls).Append('{');
            else
                sb.Append(".c").Append(cls).Append('{');

            sb.Append("color:#").Append((i % 9) + 1).Append((i % 7) + 1).Append((i % 5) + 1)
              .Append(";padding-left:").Append(i % 5).Append("px}");
        }

        sb.Append("</style></head><body><section>");
        for (var i = 0; i < elements; i++)
        {
            sb.Append("<div class=\"c").Append(i % 16).Append("\"><span class=\"c")
              .Append((i * 5) % 16).Append("\">x").Append(i).Append("</span></div>");
        }

        return sb.Append("</section></body></html>").ToString();
    }
}
