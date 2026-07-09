using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.Graphics.Linux;
using Broiler.Graphics.Linux.OpenGL;
using Broiler.UI.Standard;

namespace Broiler.UI.Linux.Demo;

internal static class Program
{
    private static readonly TimeSpan InteractivePollInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan OffscreenPollInterval = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan AnimationInterval = TimeSpan.FromMilliseconds(100);

    private static async Task<int> Main(string[] args)
    {
        LinuxUiDemoOptions options = LinuxUiDemoOptions.Parse(args);
        if (options.ShowHelp)
        {
            LinuxUiDemoOptions.PrintHelp();
            return 0;
        }

        Console.WriteLine("Broiler.UI Linux Demo");
        Console.WriteLine();
        PrintRuntime(LinuxGraphicsRuntimeDiagnostics.Capture());
        Print("Windowing baseline", LinuxGraphicsDependencies.CheckWindowingBaseline());
        Print("OpenGL", LinuxOpenGlRenderer.CheckDependencies());
        PrintFonts();

        if (options.OpenWindow && !OperatingSystem.IsLinux())
        {
            Console.WriteLine("--window requested, but X11/OpenGL windows are only available on Linux in this demo; running offscreen.");
            options = options with { OpenWindow = false, EnableEvdevInput = false, RunUntilClose = false };
        }

        using CancellationTokenSource shutdown = new();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        await RunOpenGlUiDemoAsync(options, shutdown.Token).ConfigureAwait(false);
        return 0;
    }

    private static async Task RunOpenGlUiDemoAsync(LinuxUiDemoOptions options, CancellationToken cancellationToken)
    {
        using LinuxOpenGlRenderer renderer = new();
        BSurfaceDescriptor descriptor = BSurfaceDescriptor.Default(new BSize(options.Width, options.Height));
        using IBroilerSurface surface = options.OpenWindow
            ? renderer.CreateX11WindowSurface(descriptor, "Broiler.UI Linux Demo")
            : renderer.CreateSurface(descriptor);

        LinuxOpenGlX11WindowSurface? x11Window = surface as LinuxOpenGlX11WindowSurface;
        bool canUseEvdev = options.EnableEvdevInput && x11Window is not null;
        if (options.EnableEvdevInput && x11Window is null)
            Console.WriteLine("evdev input requested, but no focus-capable X11 window exists; input is disabled.");

        using LinuxUiDemoHost host = new(renderer, surface);
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        LinuxUiDemoRoot root = new();
        session.AddRoot(root);

        await using LinuxUiDemoInputCoordinator input = new(canUseEvdev, Console.WriteLine, externalPointer: x11Window is not null);
        await input.InitializeAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset start = DateTimeOffset.UtcNow;
        DateTimeOffset nextAnimationUpdate = DateTimeOffset.MinValue;
        int frameIndex = 0;
        long renderTicks = 0;
        using PeriodicTimer timer = new(options.OpenWindow ? InteractivePollInterval : OffscreenPollInterval);
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool processedWindowEvents = false;
            if (x11Window is not null)
            {
                processedWindowEvents = x11Window.ProcessPendingEvents();
                bool inputActive = options.IgnoreInputFocus || x11Window.IsFocused;
                await input.SetActiveAsync(inputActive, cancellationToken).ConfigureAwait(false);
            }

            input.SetViewport(host.ViewportSize);

            // Keep the demo pointer in sync with the real OS cursor by sourcing
            // its position from the X11 window (pixel space -> logical pixels).
            if (x11Window is not null && x11Window.TryGetPointerPosition(out int pointerX, out int pointerY, out _))
            {
                double scale = host.Scale <= 0 ? 1.0 : host.Scale;
                input.SetExternalPointer(pointerX / scale, pointerY / scale);
            }

            input.Drain(session);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now >= nextAnimationUpdate)
            {
                root.UpdateAnimation(now);
                nextAnimationUpdate = now + AnimationInterval;
            }

            root.UpdateInputSnapshot(input.Snapshot, x11Window?.IsFocused ?? false);

            if (host.IsInvalidated || processedWindowEvents)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                session.RenderFrame();
                stopwatch.Stop();
                renderTicks += stopwatch.ElapsedTicks;
                frameIndex++;
            }

            if (!options.OpenWindow)
                break;

            if (!options.RunUntilClose &&
                (DateTimeOffset.UtcNow - start).TotalMilliseconds >= options.DurationMilliseconds)
            {
                break;
            }
        }
        while (!input.QuitRequested &&
               (x11Window is null || !x11Window.IsCloseRequested) &&
               await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));

        await input.SetActiveAsync(false, cancellationToken).ConfigureAwait(false);
        using BBitmap bitmap = ReadSurface(surface);
        Console.WriteLine("OpenGL UI demo:");
        Console.WriteLine("  presentation: " + PresentationState(surface));
        Console.WriteLine("  diagnostic: " + SurfaceDiagnostic(surface));
        Console.WriteLine("  bitmap: " + bitmap.Width.ToString(CultureInfo.InvariantCulture) + "x" + bitmap.Height.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("  render-time: " + FormatMilliseconds(renderTicks) + " ms total across " + frameIndex.ToString(CultureInfo.InvariantCulture) + " frame(s)");
        Console.WriteLine("  input: " + InputSummary(input.Snapshot));
        SaveArtifacts(options, surface, bitmap, host.LastRenderList, host.LastFrameContext);
        Console.WriteLine();
    }

    private static BBitmap ReadSurface(IBroilerSurface surface) =>
        surface switch
        {
            LinuxOpenGlSurface openGlSurface => openGlSurface.ReadToBitmap(),
            LinuxOpenGlX11WindowSurface x11Surface => x11Surface.ReadToBitmap(),
            _ => throw new InvalidOperationException("Unexpected Linux UI demo surface."),
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
        LinuxUiDemoOptions options,
        IBroilerSurface surface,
        BBitmap backendBitmap,
        BRenderList? renderList,
        BFrameContext frameContext)
    {
        if (options.ArtifactDirectory is null || renderList is null)
            return;

        Directory.CreateDirectory(options.ArtifactDirectory);
        string backendPath = Path.Combine(options.ArtifactDirectory, "broiler-ui-linux-opengl.png");
        backendBitmap.Save(backendPath);

        using BImageRenderer cpu = new();
        using BBitmap cpuBitmap = cpu.RenderToImage(renderList, new BSurfaceDescriptor(surface.Size, surface.DpiScale), frameContext);
        string cpuPath = Path.Combine(options.ArtifactDirectory, "broiler-ui-linux-cpu.png");
        cpuBitmap.Save(cpuPath);
        Console.WriteLine("  artifacts: " + cpuPath + "; " + backendPath);
    }

    private static string InputSummary(LinuxUiDemoInputSnapshot input)
    {
        if (!input.Enabled)
            return "disabled";
        if (!input.Initialized)
            return "requested but not opened";

        string keyboard = input.KeyboardDevice ?? "none";
        string mouse = input.MouseDevice ?? "none";
        return "active=" + input.Active.ToString(CultureInfo.InvariantCulture) +
            ", keyboard=" + keyboard +
            ", mouse=" + mouse +
            ", keys=" + input.KeyEvents.ToString(CultureInfo.InvariantCulture) +
            ", text=" + input.TextEvents.ToString(CultureInfo.InvariantCulture) +
            ", moves=" + input.MouseMoveEvents.ToString(CultureInfo.InvariantCulture) +
            ", buttons=" + input.MouseButtonEvents.ToString(CultureInfo.InvariantCulture) +
            ", wheels=" + input.MouseWheelEvents.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatMilliseconds(long ticks) =>
        (ticks * 1000.0 / Stopwatch.Frequency).ToString("0.###", CultureInfo.InvariantCulture);

    private static void PrintFonts()
    {
        Console.WriteLine("Fonts:");
        Console.WriteLine("  host text font: " + BImageRenderer.DescribeSystemTextFont());
        Console.WriteLine("  fallback: built-in 5x7 block glyphs (used only for codepoints the host font lacks, or when no host font is found).");
        Console.WriteLine("  note: OpenGL native replay has no text support, so every text run is CPU-rasterized.");
        foreach ((string role, BFontStyle font) in LinuxUiDemoRoot.DiagnosticFonts)
        {
            BTextMetrics metrics = BTextMeasurer.Measure("Broiler", font);
            Console.WriteLine(
                "  " + role +
                ": family=" + font.FamilyName +
                ", size=" + font.SizeInPixels.ToString(CultureInfo.InvariantCulture) + "px" +
                ", weight=" + font.Weight +
                ", line-height=" + metrics.LineHeight.ToString(CultureInfo.InvariantCulture) + "px" +
                ", advance(\"Broiler\")=" + metrics.Advance.ToString(CultureInfo.InvariantCulture) + "px");
        }

        Console.WriteLine();
    }

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
}
