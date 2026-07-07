using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Broiler.App.Rendering;
using Broiler.Graphics;
using Broiler.Graphics.Linux;
using Broiler.Graphics.Linux.OpenGL;
using Broiler.HTML.Graphics;
using Broiler.HtmlBridge;
using HtmlContainer = Broiler.HTML.Image.HtmlContainer;

namespace Broiler.App.Graphics;

internal static class LinuxBrowserRunner
{
    private static readonly TimeSpan InteractivePollInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan OffscreenPollInterval = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan AnimationInterval = TimeSpan.FromMilliseconds(16);
    private static readonly BRenderOptions RenderOptions = new(Antialias: true, VSync: true, SubpixelText: true);

    public static async Task<int> RunAsync(LinuxBrowserOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        Console.WriteLine("Broiler Browser");
        Console.WriteLine();
        Console.WriteLine("Preview safety notice: use controlled content only. JavaScript is not a sandbox.");
        Console.WriteLine();
        PrintRuntime(LinuxGraphicsRuntimeDiagnostics.Capture());
        Print("Windowing baseline", LinuxGraphicsDependencies.CheckWindowingBaseline());
        Print("OpenGL", LinuxOpenGlRenderer.CheckDependencies());

        long frameIndex = 0;
        long renderTicks = 0;
        int renderedFrames = 0;
        BFrameContext lastFrameContext = BFrameContext.Default;
        BRenderList? lastRenderList = null;

        using LinuxOpenGlRenderer renderer = new();
        BSurfaceDescriptor descriptor = BSurfaceDescriptor.Default(new BSize(options.Width, options.Height));
        using IBroilerSurface surface = options.OpenWindow
            ? renderer.CreateX11WindowSurface(descriptor, "Broiler Browser")
            : renderer.CreateSurface(descriptor);

        LinuxOpenGlX11WindowSurface? x11Window = surface as LinuxOpenGlX11WindowSurface;
        using BrowserPage page = new();
        await page.LoadAsync(options.InitialUrl, cancellationToken).ConfigureAwait(false);

        DateTimeOffset start = DateTimeOffset.UtcNow;
        DateTimeOffset nextAnimationUpdate = DateTimeOffset.MinValue;
        BSize lastSurfaceSize = surface.Size;
        using PeriodicTimer timer = new(options.OpenWindow ? InteractivePollInterval : OffscreenPollInterval);
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool processedWindowEvents = false;
            if (x11Window is not null)
            {
                processedWindowEvents = x11Window.ProcessPendingEvents();
                if (!surface.Size.Equals(lastSurfaceSize))
                {
                    page.MarkLayoutDirty();
                    lastSurfaceSize = surface.Size;
                }
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (page.HasPendingWork && now >= nextAnimationUpdate)
            {
                page.StepAnimation();
                nextAnimationUpdate = now + AnimationInterval;
            }

            if (page.IsInvalidated || processedWindowEvents)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                BRenderList? renderList = page.BuildRenderList(renderer, surface.Size);
                if (renderList is not null)
                {
                    lastFrameContext = new BFrameContext(page.ResolveClearColor(), frameIndex++, RenderOptions);
                    renderer.Render(surface, renderList, lastFrameContext);
                    lastRenderList = renderList;
                    renderedFrames++;
                }

                stopwatch.Stop();
                renderTicks += stopwatch.ElapsedTicks;
                page.ClearInvalidated();
            }

            if (!options.OpenWindow)
                break;

            if (!options.RunUntilClose &&
                (DateTimeOffset.UtcNow - start).TotalMilliseconds >= options.DurationMilliseconds)
            {
                break;
            }
        }
        while ((x11Window is null || !x11Window.IsCloseRequested) &&
               await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));

        using BBitmap bitmap = ReadSurface(surface);
        Console.WriteLine("Browser presentation:");
        Console.WriteLine("  presentation: " + PresentationState(surface));
        Console.WriteLine("  diagnostic: " + SurfaceDiagnostic(surface));
        Console.WriteLine("  page: " + page.Status);
        Console.WriteLine("  bitmap: " + bitmap.Width.ToString(CultureInfo.InvariantCulture) + "x" + bitmap.Height.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("  render-time: " + FormatMilliseconds(renderTicks) + " ms total across " + renderedFrames.ToString(CultureInfo.InvariantCulture) + " frame(s)");
        SaveArtifacts(options, surface, bitmap, lastRenderList, lastFrameContext);
        Console.WriteLine();
        return 0;
    }

    private static BBitmap ReadSurface(IBroilerSurface surface) =>
        surface switch
        {
            LinuxOpenGlSurface openGlSurface => openGlSurface.ReadToBitmap(),
            LinuxOpenGlX11WindowSurface x11Surface => x11Surface.ReadToBitmap(),
            _ => throw new InvalidOperationException("Unexpected Linux browser surface."),
        };

    private static string PresentationState(IBroilerSurface surface) =>
        surface switch
        {
            LinuxOpenGlSurface openGlSurface => openGlSurface.IsGpuBacked ? "EGL/OpenGL pbuffer" : "CPU fallback",
            LinuxOpenGlX11WindowSurface x11Surface => x11Surface.IsGpuBacked ? "X11/EGL/OpenGL window" : "CPU fallback",
            _ => "unknown",
        };

    private static string SurfaceDiagnostic(IBroilerSurface surface) =>
        surface switch
        {
            LinuxOpenGlSurface openGlSurface => openGlSurface.Diagnostic,
            LinuxOpenGlX11WindowSurface x11Surface => x11Surface.Diagnostic,
            _ => string.Empty,
        };

    private static void SaveArtifacts(
        LinuxBrowserOptions options,
        IBroilerSurface surface,
        BBitmap backendBitmap,
        BRenderList? renderList,
        BFrameContext frameContext)
    {
        if (options.ArtifactDirectory is null)
            return;

        Directory.CreateDirectory(options.ArtifactDirectory);
        string backendPath = Path.Combine(options.ArtifactDirectory, "broiler-browser-linux-opengl.png");
        backendBitmap.Save(backendPath);

        if (renderList is not null)
        {
            using BImageRenderer cpu = new();
            using BBitmap cpuBitmap = cpu.RenderToImage(renderList, new BSurfaceDescriptor(surface.Size, surface.DpiScale), frameContext);
            string cpuPath = Path.Combine(options.ArtifactDirectory, "broiler-browser-linux-cpu.png");
            cpuBitmap.Save(cpuPath);
            Console.WriteLine("  artifacts: " + cpuPath + "; " + backendPath);
            return;
        }

        Console.WriteLine("  artifact: " + backendPath);
    }

    private static string FormatMilliseconds(long ticks) =>
        (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.###", CultureInfo.InvariantCulture);

    private static void PrintRuntime(LinuxGraphicsRuntimeReport report)
    {
        Console.WriteLine("Runtime:");
        Console.WriteLine("  os: " + report.OperatingSystemDescription);
        Console.WriteLine("  framework: " + report.FrameworkDescription);
        Console.WriteLine("  arch: " + report.ProcessArchitecture);
        Console.WriteLine("  rid: " + report.RuntimeIdentifier);
        Console.WriteLine("  display: " + report.DisplayServer.Diagnostic);
        Console.WriteLine();
    }

    private static void Print(string title, System.Collections.Generic.IReadOnlyList<LinuxNativeLibraryStatus> statuses)
    {
        Console.WriteLine(title + ":");
        foreach (LinuxNativeLibraryStatus status in statuses)
        {
            string state = status.IsAvailable ? "available" : "missing";
            Console.WriteLine("  " + status.Id + ": " + state + " - " + status.Diagnostic);
        }

        Console.WriteLine();
    }

    private sealed class BrowserPage : IDisposable
    {
        private const string WelcomePage = """
<html>
<body style="font-family: Segoe UI, Arial, sans-serif; margin: 40px; color: #263238;">
  <h1>Broiler Browser Preview</h1>
  <p>This Linux preview host renders through Broiler.Graphics OpenGL.</p>
  <p>Pass --url &lt;url&gt; to load an HTTP(S) or local HTML page.</p>
</body>
</html>
""";

        private HtmlContainer _container = CreateContentContainer(WelcomePage, string.Empty);
        private HtmlGraphicsRenderList? _renderList;
        private InteractiveSession? _session;
        private bool _layoutDirty = true;
        private bool _renderDirty = true;
        private float _contentHeight;
        private float _scrollY;
        private BSize _lastLayoutSize;

        public bool IsInvalidated { get; private set; } = true;

        public bool HasPendingWork => _session?.HasPendingWork == true;

        public string Status { get; private set; } = "about:blank";

        public async Task LoadAsync(string? url, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url) || string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
            {
                Status = "about:blank";
                ReplaceContainer(CreateContentContainer(WelcomePage, string.Empty));
                return;
            }

            string normalisedInput = NormalizeInput(url);
            Console.WriteLine("Loading " + normalisedInput + "...");
            try
            {
                using var pipeline = new RenderingPipeline(
                    new PageLoader(new HttpClient()),
                    new ScriptEngine());

                var (normalisedUrl, content) = await pipeline.LoadPageAsync(normalisedInput, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                string html = HtmlPostProcessor.Process(content.Html);
                InteractiveSession? session = null;
                try
                {
                    session = pipeline.ExecuteScriptsInteractive(content);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (session is not null)
                    {
                        string initial = session.CurrentHtml();
                        if (!string.IsNullOrWhiteSpace(initial))
                            html = HtmlPostProcessor.Process(initial);

                        if (!session.HasPendingWork)
                        {
                            session.Dispose();
                            session = null;
                        }
                    }

                    ReplaceContainer(CreateContentContainer(html, normalisedUrl));
                    _session = session;
                    Status = normalisedUrl;
                }
                catch
                {
                    session?.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Status = "error: " + ex.Message;
                ReplaceContainer(CreateContentContainer(
                    "<html><body><h1>Error</h1><p>" + System.Net.WebUtility.HtmlEncode(ex.Message) + "</p></body></html>",
                    string.Empty));
            }
        }

        public void StepAnimation()
        {
            if (_session is null || !_session.HasPendingWork)
                return;

            string? html = _session.Step();
            if (!string.IsNullOrWhiteSpace(html))
                ReplaceContainer(CreateContentContainer(HtmlPostProcessor.Process(html), _container.BaseUrl));

            if (!_session.HasPendingWork)
            {
                _session.Dispose();
                _session = null;
            }
        }

        public void MarkLayoutDirty()
        {
            _layoutDirty = true;
            _renderDirty = true;
            IsInvalidated = true;
        }

        public void ClearInvalidated() => IsInvalidated = false;

        public BColor ResolveClearColor()
        {
            BColor background = _container.GetRootBackgroundColor();
            return !background.IsEmpty && background.A > 0
                ? new BColor(background.R, background.G, background.B, background.A)
                : BColor.White;
        }

        public BRenderList? BuildRenderList(IBroilerRenderer renderer, BSize size)
        {
            if (size.IsEmpty)
                return null;

            float viewportWidth = (float)size.Width;
            float viewportHeight = (float)size.Height;
            if (_layoutDirty || !size.Equals(_lastLayoutSize))
            {
                _container.Location = PointF.Empty;
                _container.MaxSize = new SizeF(viewportWidth, viewportHeight);
                _container.PerformLayout(new RectangleF(0, 0, viewportWidth, viewportHeight));
                _contentHeight = _container.ActualSize.Height;
                _layoutDirty = false;
                _renderDirty = true;
                _lastLayoutSize = size;
            }

            ClampScroll(viewportHeight);

            if (_renderDirty || _renderList is null)
            {
                _container.ScrollOffset = new PointF(0, -_scrollY);
                DisposeRenderList();
                _renderList = HtmlGraphicsRenderListBuilder.Build(
                    renderer,
                    _container.CreateDisplayList(),
                    new RectangleF(0, 0, viewportWidth, viewportHeight));
                _renderDirty = false;
            }

            return _renderList.RenderList;
        }

        public void Dispose()
        {
            _session?.Dispose();
            DisposeRenderList();
            _container.Dispose();
        }

        private void ClampScroll(float viewportHeight)
        {
            float maxScroll = Math.Max(0, _contentHeight - viewportHeight);
            float clamped = Math.Clamp(_scrollY, 0, maxScroll);
            if (clamped != _scrollY)
            {
                _scrollY = clamped;
                _renderDirty = true;
            }
        }

        private void ReplaceContainer(HtmlContainer container)
        {
            DisposeRenderList();
            _container.Dispose();
            _container = container;
            _scrollY = 0;
            MarkLayoutDirty();
        }

        private void DisposeRenderList()
        {
            HtmlGraphicsRenderList? renderList = _renderList;
            _renderList = null;
            renderList?.Dispose();
        }

        private static HtmlContainer CreateContentContainer(string html, string baseUrl)
        {
            HtmlContainer container = new()
            {
                AvoidAsyncImagesLoading = true,
                AvoidImagesLateLoading = true,
                BaseUrl = baseUrl,
            };
            container.SetHtmlWithStyleSet(html, baseUrl: baseUrl);
            return container;
        }

        private static string NormalizeInput(string input)
        {
            input = input.Trim();
            if (File.Exists(input))
                return new Uri(Path.GetFullPath(input)).AbsoluteUri;

            return input;
        }
    }
}
