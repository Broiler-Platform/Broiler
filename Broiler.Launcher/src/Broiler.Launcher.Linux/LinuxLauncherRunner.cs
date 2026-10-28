using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Linux.OpenGL;
using Broiler.Graphics.Rendering;
using Broiler.Hosting.Linux;
using Broiler.Launcher.UI;
using Broiler.UI;
using Broiler.UI.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

namespace Broiler.Launcher.Linux;

/// <summary>
/// Execution runner for Broiler Launcher on Linux with X11/OpenGL support and offscreen headless capability.
/// </summary>
internal static class LinuxLauncherRunner
{
    private static readonly TimeSpan InteractivePollInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan OffscreenPollInterval = TimeSpan.FromMilliseconds(1);

    public static async Task<int> RunAsync(LinuxLauncherOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        bool closeRequested = false;
        long renderTicks = 0;
        int renderedFrames = 0;

        using LinuxOpenGlRenderer renderer = new();
        BSurfaceDescriptor descriptor = BSurfaceDescriptor.Default(new BSize(options.Width, options.Height));
        using IBroilerSurface surface = options.OpenWindow
            ? renderer.CreateX11WindowSurface(descriptor, "Broiler Launcher")
            : renderer.CreateSurface(descriptor);

        LinuxOpenGlX11WindowSurface? x11Window = surface as LinuxOpenGlX11WindowSurface;
        bool canUseEvdev = options.EnableEvdevInput && x11Window is not null;

        using LinuxX11Clipboard? clipboard = x11Window is not null ? LinuxX11Clipboard.TryOpen() : null;
        LinuxUiHost host = new(renderer, surface);

        StandardQueuedUiDispatcher dispatcher = new();
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(dispatcher).Build(host);

        StandardWindow rootWindow = new()
        {
            Chrome = UiWindowChrome.None,
            BorderThickness = 0,
            Background = LauncherView.CanvasColor,
        };

        LauncherShellView shell = new(ownerWindow: rootWindow);
        rootWindow.AddChild(shell);
        shell.LauncherView.ExitRequested += (_, _) => closeRequested = true;

        session.AddRoot(rootWindow);
        StandardThemeController.Apply(session, LauncherView.Theme);

        await using LinuxInputCoordinator input = new(canUseEvdev, Console.WriteLine, externalPointer: x11Window is not null, applicationName: "Broiler Launcher");
        await input.InitializeAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset start = DateTimeOffset.UtcNow;
        using PeriodicTimer timer = new(options.OpenWindow ? InteractivePollInterval : OffscreenPollInterval);

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool processedWindowEvents = false;

            clipboard?.ProcessPendingEvents();
            if (x11Window is not null)
            {
                processedWindowEvents = x11Window.ProcessPendingEvents();
                await input.SetActiveAsync(x11Window.IsFocused, cancellationToken).ConfigureAwait(false);
            }

            input.SetViewport(host.ViewportSize);
            if (x11Window is not null && x11Window.TryGetPointerPosition(out int pointerX, out int pointerY, out _))
            {
                double scale = host.Scale <= 0 ? 1.0 : host.Scale;
                input.SetExternalPointer(pointerX / scale, pointerY / scale);
            }

            int drained = input.Drain(e => session.DispatchInput(e));

            if (host.IsInvalidated || processedWindowEvents || renderedFrames == 0 || drained > 0)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                session.RenderFrame();
                stopwatch.Stop();
                renderTicks += stopwatch.ElapsedTicks;
                renderedFrames++;
            }

            if (!options.OpenWindow)
                break;

            if (!options.RunUntilClose &&
                (DateTimeOffset.UtcNow - start).TotalMilliseconds >= options.DurationMilliseconds)
            {
                break;
            }
        }
        while (!closeRequested &&
               (x11Window is null || !x11Window.IsCloseRequested) &&
               await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));

        await input.SetActiveAsync(false, cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
