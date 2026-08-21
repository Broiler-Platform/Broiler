using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Broiler.Graphics;
using Broiler.HTML.Core.Entities;
using Broiler.HTML.Image;
using Broiler.Layout;
using Xunit;
using BBitmap = Broiler.HTML.Image.BBitmap;
using BColor = Broiler.Graphics.BColor;

namespace Broiler.Cli.Tests;

/// <summary>
/// The exit gate for multithreading item #8: issuing a document's image loads concurrently produces
/// the same render the serial inline loads did, at every budget — and claims no image the serial path
/// would not have loaded.
/// </summary>
/// <remarks>
/// <para>
/// <b>Whole documents, and the files they reference.</b> The unit of this item is a document: what it
/// changes is when a page's image loads start relative to the layout pass, so a test that drove a
/// loader directly would exercise the part that did not change. The fixture writes real PNG, JPEG and
/// SVG files to a temporary directory because <c>SetImageFromFile</c> is the path the roadmap's §6
/// identifies as inline and serial; a <c>data:</c> URI decodes through <c>SetFromInlineData</c>
/// instead, so it is present as one document rather than as all of them.
/// </para>
/// <para>
/// <b>Byte comparison, not tolerance.</b> The roadmap's global exit gate is that a parallel path has a
/// single-threaded equivalent reproducing its output <em>exactly</em>. For this item a tolerance would
/// forgive the failure that matters most: an image whose decode landed after the box that needed it
/// was measured, which is a replaced box at the wrong intrinsic size and a page that reflows around
/// it.
/// </para>
/// <para>
/// <b>Half of these cases are about what the walk refuses to claim, and that is deliberate.</b> The
/// walk is one-sided by construction — a box it skips loads its image exactly where it did before, so
/// under-claiming costs speed and over-claiming costs correctness. An image loaded for a box layout
/// never measures is a request the document did not make and, for a broken source, an error the host
/// would never have seen; neither is visible in a pixel comparison. So the counters are asserted
/// directly.
/// </para>
/// </remarks>
public sealed class ImagePrefetchTests : IClassFixture<ImagePrefetchTests.ImageFiles>
{
    private const int Width = 400;
    private const int Height = 500;

    private readonly ImageFiles _files;

    public ImagePrefetchTests(ImageFiles files) => _files = files;

    /// <summary>
    /// Documents under test, one per shape that decides something about the walk. Named so a failure
    /// says which shape diverged.
    /// </summary>
    private IReadOnlyDictionary<string, string> Documents => new Dictionary<string, string>
    {
        // The ordinary case: several replaced images, all of them measured.
        ["several images"] = Document(Images(6)),

        // Background layers reach the walk through CssBox rather than CssBoxImage, so they are a
        // second claim site and not a variant of the first.
        ["background layers"] = Document(Backgrounds(6)),

        // A box that is both: a replaced image with a background of its own is two loads on one box,
        // and CssBoxImage.LoadImagesForPrefetch has to do both.
        ["image with its own background"] = Document(
            $"<img src=\"{Name(0)}\" style=\"width:120px;height:90px;background-image:url({Name(1)})\">"
            + $"<img src=\"{Name(2)}\" style=\"width:120px;height:90px;background-image:url({Name(3)})\">"),

        // Mixed, which is what a real page looks like.
        ["mixed"] = Document(Images(4) + Backgrounds(4)),

        // The same file referenced twice: two boxes, two loaders, two loads — as before. A cache that
        // collapsed them would change how many times the file is read.
        ["duplicate sources"] = Document(
            $"<img src=\"{Name(0)}\" style=\"width:120px;height:90px\">"
            + $"<img src=\"{Name(0)}\" style=\"width:120px;height:90px\">"
            + $"<div style=\"width:120px;height:90px;background-image:url({Name(0)})\"></div>"),

        // Sources that fail, in the two ways a load can: a file that is not there, and inline data
        // that will not decode. Both fail on a worker now, so both have to produce the box's error
        // border and the host's error report exactly as they did when they failed during layout.
        // Only the second reports: a missing file completes with a null image and no report at all,
        // which is pre-existing behaviour in the load handler and is why the malformed data URI is
        // here rather than three missing files.
        ["broken sources"] = Document(
            "<img src=\"does-not-exist-1.png\" style=\"width:120px;height:90px\">"
            + $"<img src=\"{Name(0)}\" style=\"width:120px;height:90px\">"
            + "<img src=\"does-not-exist-2.jpg\" style=\"width:120px;height:90px\">"
            + "<img src=\"data:image/png;base64,not-base64-at-all\" style=\"width:120px;height:90px\">"
            + "<img src=\"data:image/png;base64,\" style=\"width:120px;height:90px\">"),

        // SVG, because it is the one image kind whose "decode" rasterizes — it opens a canvas and
        // draws, including text. That is the case that makes a prefetch worker a render thread.
        ["svg images"] = Document(
            $"<img src=\"{SvgName(0)}\" style=\"width:150px;height:100px\">"
            + $"<img src=\"{SvgName(1)}\" style=\"width:150px;height:100px\">"
            + $"<img src=\"{Name(0)}\" style=\"width:120px;height:90px\">"),

        // A data: URI alongside files: it takes SetFromInlineData rather than SetImageFromFile, so a
        // walk that mishandled it would show up here and nowhere else.
        ["inline data alongside files"] = Document(
            "<img src=\"data:image/svg+xml;utf8,"
            + "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='30'><rect width='40' height='30' fill='%23c00'/></svg>"
            + "\" style=\"width:80px;height:60px\">"
            + Images(4)),

        // display:none subtrees, which the walk must not claim: layout does not measure them, so an
        // image inside one is not loaded — and a prefetch would load it.
        ["hidden images"] = Document(
            Images(3)
            + $"<div style=\"display:none\"><img src=\"{Name(4)}\"><img src=\"{Name(5)}\"></div>"),

        // An auto-width box, where the shrink-to-fit pre-pass measures descendants without consulting
        // Display. Nothing here asserts a count; it is present because it is the case the walk's
        // remarks say it declines, and the render still has to come out right.
        ["shrink to fit around a hidden image"] = Document(
            "<div style=\"float:left\">" + Images(2)
            + $"<span style=\"display:none\"><img src=\"{Name(3)}\"></span></div>"),

        // Images inside a table, whose cells are measured through a different path
        // (CssLayoutEngineTable calls MeasureWordsSize itself).
        ["images in a table"] = Document(
            "<table><tr>"
            + $"<td><img src=\"{Name(0)}\" style=\"width:90px;height:70px\"></td>"
            + $"<td><img src=\"{Name(1)}\" style=\"width:90px;height:70px\"></td>"
            + $"<td style=\"background-image:url({Name(2)})\">cell</td>"
            + "</tr></table>"),
    };

    public static TheoryData<string, int> Cases
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var name in new ImagePrefetchTests(ImageFiles.Shared).Documents.Keys)
            {
                foreach (var loads in new[] { 2, 3, 4, 8 })
                    data.Add(name, loads);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Concurrent_Image_Loads_Render_The_Same_Pixels_As_Serial_Ones(string document, int loads)
    {
        var serial = Render(Documents[document], concurrentLoads: 1);
        var concurrent = Render(Documents[document], concurrentLoads: loads);

        Assert.Equal(serial, concurrent);
    }

    /// <summary>
    /// Guards the guard: every document above must actually reach the walk, or the equality
    /// assertions are comparing the serial path with itself.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void These_Documents_Actually_Reach_The_Walk()
    {
        foreach ((string name, string html) in Documents)
        {
            var counts = RenderCounting(html, concurrentLoads: 4);

            Assert.True(
                counts.DocumentsWalked > 0,
                $"'{name}' was never walked ({counts.DocumentsBelowMinimum} below-minimum, "
                + $"{counts.DocumentsNotSynchronous} async-host skip(s)), so its equality test proves nothing.");
            Assert.True(
                counts.LoadsIssued >= ImagePrefetch.MinimumImagesToOverlap,
                $"'{name}' issued only {counts.LoadsIssued} load(s).");
        }
    }

    /// <summary>
    /// A budget of one is the pre-change path, not a one-wide version of the new one: the walk does
    /// not run, so every box creates its loader in <c>MeasureWordsSize</c> exactly where it used to.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Budget_Of_One_Does_Not_Walk_At_All()
    {
        var counts = RenderCounting(Documents["several images"], concurrentLoads: 1);

        Assert.Equal(0, counts.DocumentsWalked);
        Assert.Equal(0, counts.LoadsIssued);

        // Not even counted as a skip: at a budget of one the walk returns before it can have an
        // opinion about the document, which is what makes this setting the untouched path.
        Assert.Equal(0, counts.DocumentsBelowMinimum);
        Assert.Equal(0, counts.DocumentsNotSynchronous);
    }

    /// <summary>
    /// One image is not worth a worker: there is nothing to overlap it with, because the layout pass
    /// that would otherwise have loaded it has not started.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Document_Naming_One_Image_Is_Left_Alone()
    {
        var counts = RenderCounting(
            Document($"<img src=\"{Name(0)}\" style=\"width:120px;height:90px\">"),
            concurrentLoads: 4);

        Assert.Equal(0, counts.DocumentsWalked);
        Assert.Equal(1, counts.DocumentsBelowMinimum);
    }

    /// <summary>
    /// A document with no images at all is neither walked nor counted as a skip — "nothing to do" is
    /// not one of the two reasons a page can be declined for.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Document_With_No_Images_Is_Not_Counted_At_All()
    {
        var counts = RenderCounting(Document("<p>no images here</p>"), concurrentLoads: 4);

        Assert.Equal(0, counts.DocumentsWalked);
        Assert.Equal(0, counts.DocumentsBelowMinimum);
        Assert.Equal(0, counts.DocumentsNotSynchronous);
    }

    /// <summary>
    /// A host that permits late image loading is skipped, and the reason is recorded separately from
    /// "too few images".
    /// </summary>
    /// <remarks>
    /// <b>This is a safety property, not a policy one.</b> A load that completes while late loading is
    /// permitted calls <c>RequestRefresh</c> from whichever thread it completed on, and handing a host
    /// a relayout request on a worker it never agreed to be called back on is exactly what a
    /// speculation must not do. So the configuration is declined rather than accommodated.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void A_Host_That_Permits_Late_Image_Loading_Is_Skipped()
    {
        var counts = RenderCounting(
            Documents["several images"],
            concurrentLoads: 4,
            avoidImagesLateLoading: false);

        Assert.Equal(0, counts.DocumentsWalked);
        Assert.Equal(1, counts.DocumentsNotSynchronous);
        Assert.Equal(0, counts.DocumentsBelowMinimum);
    }

    /// <summary>
    /// An image inside a <c>display:none</c> subtree is not claimed. Layout does not measure that
    /// subtree, so loading its image would put a request on the wire the document did not make.
    /// </summary>
    /// <remarks>
    /// The count is the assertion because the pixels cannot be: a hidden image draws nothing either
    /// way, so a walk that loaded it would render identically and differ only in what it fetched and
    /// what it reported. Two of the five images in this document are hidden.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void An_Image_In_A_Hidden_Subtree_Is_Not_Claimed()
    {
        var counts = RenderCounting(Documents["hidden images"], concurrentLoads: 4);

        Assert.Equal(3, counts.LoadsIssued);
    }

    /// <summary>
    /// A broken source is reported to the host exactly as often as the serial path reported it: once
    /// per reference, from whichever thread ran the load.
    /// </summary>
    /// <remarks>
    /// <b>A count, because a count is what the event carries</b> —
    /// <c>HtmlRenderErrorEventArgs</c> exposes only the error's <c>Type</c>. The number is still the
    /// property worth pinning: a walk that claimed a box layout would not have measured would report
    /// more, and one that loaded an image twice would too. Order is deliberately not asserted, since
    /// the loads no longer complete in document order and nothing about an image failure's meaning
    /// depends on which of two the host hears about first.
    /// </remarks>
    [Fact(Timeout = 600000)]
    public void A_Broken_Source_Is_Reported_The_Same_Number_Of_Times()
    {
        var serial = CountImageErrors(Documents["broken sources"], concurrentLoads: 1);
        var concurrent = CountImageErrors(Documents["broken sources"], concurrentLoads: 4);

        Assert.True(serial > 0, "the serial render reported no image error, so this proves nothing");
        Assert.Equal(serial, concurrent);
    }

    private byte[] Render(string html, int concurrentLoads)
    {
        var previous = ImagePrefetch.MaxDegreeOfParallelism;
        ImagePrefetch.MaxDegreeOfParallelism = concurrentLoads;
        try
        {
            using var bitmap = HtmlRender.RenderToImageWithStyleSet(
                html, Width, Height,
                backgroundColor: new BColor(255, 255, 255, 255),
                baseUrl: _files.BaseUrl);
            return bitmap.Encode();
        }
        finally
        {
            ImagePrefetch.MaxDegreeOfParallelism = previous;
        }
    }

    private (long DocumentsWalked, long LoadsIssued, long DocumentsBelowMinimum, long DocumentsNotSynchronous)
        RenderCounting(string html, int concurrentLoads, bool avoidImagesLateLoading = true)
    {
        var previous = ImagePrefetch.MaxDegreeOfParallelism;
        ImagePrefetch.MaxDegreeOfParallelism = concurrentLoads;
        ImagePrefetch.ResetDiagnostics();
        ImagePrefetch.CollectDiagnostics = true;
        try
        {
            RenderThroughContainer(html, avoidImagesLateLoading);
            return ImagePrefetch.Diagnostics;
        }
        finally
        {
            ImagePrefetch.CollectDiagnostics = false;
            ImagePrefetch.MaxDegreeOfParallelism = previous;
        }
    }

    /// <summary>
    /// Renders through a container rather than <c>HtmlRender</c>, because the image-loading flags are
    /// what some of these cases vary and <c>HtmlRender</c> sets both of them itself.
    /// </summary>
    private void RenderThroughContainer(string html, bool avoidImagesLateLoading)
    {
        var clip = new RectangleF(0, 0, Width, Height);
        using var bitmap = new BBitmap(Width, Height);
        using var container = new HtmlContainer();
        container.Location = new PointF(0, 0);
        container.MaxSize = new SizeF(Width, Height);
        container.AvoidAsyncImagesLoading = true;
        container.AvoidImagesLateLoading = avoidImagesLateLoading;
        container.SetHtmlWithStyleSet(html, null, _files.BaseUrl);
        bitmap.Clear(BColor.White);
        container.PerformLayout(bitmap, clip);
        container.PerformPaint(bitmap, clip);
    }

    private int CountImageErrors(string html, int concurrentLoads)
    {
        var errors = 0;
        var previous = ImagePrefetch.MaxDegreeOfParallelism;
        ImagePrefetch.MaxDegreeOfParallelism = concurrentLoads;
        try
        {
            var clip = new RectangleF(0, 0, Width, Height);
            using var bitmap = new BBitmap(Width, Height);
            using var container = new HtmlContainer();
            container.Location = new PointF(0, 0);
            container.MaxSize = new SizeF(Width, Height);
            container.AvoidAsyncImagesLoading = true;
            container.AvoidImagesLateLoading = true;
            // Counted with an interlocked increment because the loads that report these now run
            // concurrently, which is the change. A host that keeps a list here needs the same care,
            // and this counter needing it is the test noticing that on the host's behalf.
            container.RenderError += (_, args) =>
            {
                if (args.Type == HtmlRenderErrorType.Image)
                    Interlocked.Increment(ref errors);
            };
            container.SetHtmlWithStyleSet(html, null, _files.BaseUrl);
            bitmap.Clear(BColor.White);
            container.PerformLayout(bitmap, clip);
            container.PerformPaint(bitmap, clip);
        }
        finally
        {
            ImagePrefetch.MaxDegreeOfParallelism = previous;
        }

        return errors;
    }

    private static string Document(string body) =>
        "<!DOCTYPE html><html><head><style>"
        + "body{margin:0;font:13px/1.4 sans-serif}"
        + "img{display:inline-block;margin:2px}"
        + ".bg{width:110px;height:80px;display:inline-block;margin:2px;"
        + "background-repeat:no-repeat;background-size:cover}"
        + "</style></head><body>" + body + "</body></html>";

    private static string Images(int count)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < count; i++)
            sb.Append("<img src=\"").Append(Name(i)).Append("\" style=\"width:110px;height:80px\">");
        return sb.ToString();
    }

    private static string Backgrounds(int count)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < count; i++)
            sb.Append("<div class=\"bg\" style=\"background-image:url(").Append(Name(i)).Append(")\"></div>");
        return sb.ToString();
    }

    private static string Name(int i) => ImageFiles.NameOf(i);

    private static string SvgName(int i) => ImageFiles.SvgNameOf(i);

    /// <summary>
    /// A directory of encoded images shared by every case in this class, written once and deleted
    /// when the class finishes.
    /// </summary>
    /// <remarks>
    /// <b>Shared rather than per-case, and it has to be.</b> These files are inputs, not state: no
    /// case writes to them, and encoding eight bitmaps per theory case would dominate the run. The
    /// static <see cref="Shared"/> exists only for <see cref="Cases"/>, which xunit evaluates before
    /// any fixture is constructed and which needs the file names to build the documents.
    /// </remarks>
    public sealed class ImageFiles : IDisposable
    {
        private const int FileCount = 8;
        private const int Size = 64;

        private static readonly Lazy<ImageFiles> Lazy = new(() => new ImageFiles());

        private readonly string _directory;

        public ImageFiles()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "broiler-image-prefetch-tests-"
                    + Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
                    + "-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_directory);

            for (var i = 0; i < FileCount; i++)
            {
                using var source = BuildImage(i);
                File.WriteAllBytes(
                    Path.Combine(_directory, NameOf(i)),
                    i % 2 == 0
                        ? source.Encode(Media.Image.ImageEncodeFormat.Png, 100)
                        : source.Encode(Media.Image.ImageEncodeFormat.Jpeg, 90));
            }

            for (var i = 0; i < 2; i++)
            {
                File.WriteAllText(Path.Combine(_directory, SvgNameOf(i)),
                    "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"80\">"
                    + $"<rect x=\"4\" y=\"4\" width=\"112\" height=\"72\" fill=\"#{i}9{i}\" stroke=\"#333\"/>"
                    + $"<circle cx=\"{30 + i * 20}\" cy=\"40\" r=\"18\" fill=\"#fc0\"/>"
                    + $"<text x=\"10\" y=\"70\" font-size=\"12\">svg {i}</text></svg>");
            }

            BaseUrl = new Uri(Path.Combine(_directory, "page.html")).AbsoluteUri;
        }

        internal static ImageFiles Shared => Lazy.Value;

        /// <summary>Base for the documents' relative <c>src</c>s — this directory.</summary>
        public string BaseUrl { get; }

        internal static string NameOf(int i) =>
            i % 2 == 0
                ? "f" + (i % FileCount).ToString(CultureInfo.InvariantCulture) + ".png"
                : "f" + (i % FileCount).ToString(CultureInfo.InvariantCulture) + ".jpg";

        internal static string SvgNameOf(int i) =>
            "s" + i.ToString(CultureInfo.InvariantCulture) + ".svg";

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory is not a failed test.
            }
        }

        /// <summary>Deterministic and distinct per index, so no two files decode to the same pixels.</summary>
        private static BBitmap BuildImage(int seed)
        {
            var bitmap = new BBitmap(Size, Size);
            var offset = seed * 29;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    bitmap.SetPixel(x, y, new BColor(
                        (byte)((x * 4 + offset) & 0xFF),
                        (byte)((y * 4 + offset) & 0xFF),
                        (byte)(((x + y) * 2 + offset) & 0xFF),
                        255));
                }
            }

            return bitmap;
        }
    }
}
