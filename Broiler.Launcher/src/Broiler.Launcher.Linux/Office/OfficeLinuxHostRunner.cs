#if BROILER_OFFICE_ALL_IN_ONE
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Cells.UI;
using Broiler.Documents.Pdf.Writing;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Linux.OpenGL;
using Broiler.Graphics.Rendering;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Resources;
using Broiler.Hosting.Linux;
using Broiler.Launcher.Office;
using Broiler.Mail.Application;
using Broiler.Mail.Application.ViewModels;
using Broiler.Mail.Application.Views;
using Broiler.Plate;
using Broiler.UI;
using Broiler.UI.Standard;
using Broiler.Writer;

namespace Broiler.Launcher.Linux.Office;

/// <summary>
/// Shared Linux X11/OpenGL/offscreen host runner for Broiler Office built-in applications.
/// </summary>
internal static class OfficeLinuxHostRunner
{
    private static readonly TimeSpan InteractivePollInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan OffscreenPollInterval = TimeSpan.FromMilliseconds(1);
    private static readonly BRenderOptions RenderOptions = new(Antialias: true, VSync: true, SubpixelText: true);

    public static int RunWriter(OfficeAppOptions options) =>
        RunInLoop(options, "Broiler Writer", WriterPalette.Canvas, CreateWriterApp);

    public static void OpenWriter(OfficeAppOptions options) =>
        OpenNonModal(options, "Broiler Writer", CreateWriterApp);

    private static AppCallbacks CreateWriterApp(
        IBroilerSurface surface,
        LinuxOpenGlRenderer renderer,
        LinuxX11Clipboard? clipboard,
        Action requestClose,
        OfficeAppOptions options)
    {
        long frameIndex = 0;
        var host = new WriterUiHost(
            () => surface.Size,
            () => surface.DpiScale,
            static () => { },
            renderList => renderer.Render(surface, renderList, new BFrameContext(WriterPalette.Canvas, frameIndex++, RenderOptions)),
            () => clipboard is not null && clipboard.TryGetText(out string text) ? text : null,
            text => clipboard?.SetText(text),
            getRenderer: () => renderer);

        var app = new WriterApp(
            host,
            requestClose,
            documentFormats: OfficeComposition.CreateWriterDocumentFormats(),
            setWindowTitle: surface is LinuxOpenGlX11WindowSurface x11 ? x11.SetTitle : null,
            printHost: LinuxCupsPrintHost.IsAvailable ? new LinuxCupsPrintHost(new PdfRasterPageWriter()) : null);

        if (!string.IsNullOrWhiteSpace(options.InitialPath) && File.Exists(options.InitialPath))
        {
            using FileStream stream = File.OpenRead(options.InitialPath);
            app.LoadDocument(stream, options.InitialPath);
        }

        return new AppCallbacks(
            IsInvalidated: () => host.IsInvalidated,
            Dispatch: app.Dispatch,
            Tick: () => { if (app.WantsTick) app.AnimationTick(); },
            RenderFrame: () => app.RenderFrame(),
            Dispose: () => { app.Dispose(); host.Dispose(); });
    }

    public static int RunPlate(OfficeAppOptions options) =>
        RunInLoop(options, "Broiler Plate", PlatePalette.Canvas, CreatePlateApp);

    public static void OpenPlate(OfficeAppOptions options) =>
        OpenNonModal(options, "Broiler Plate", CreatePlateApp);

    private static AppCallbacks CreatePlateApp(
        IBroilerSurface surface,
        LinuxOpenGlRenderer renderer,
        LinuxX11Clipboard? clipboard,
        Action requestClose,
        OfficeAppOptions options)
    {
        long frameIndex = 0;
        var host = new PlateUiHost(
            () => surface.Size,
            () => surface.DpiScale,
            static () => { },
            renderList => renderer.Render(surface, renderList, new BFrameContext(PlatePalette.Canvas, frameIndex++, RenderOptions)),
            () => clipboard is not null && clipboard.TryGetText(out string text) ? text : null,
            text => clipboard?.SetText(text),
            getRenderer: () => renderer);

        var app = new PlateApp(
            host,
            requestClose,
            formats: OfficeComposition.CreatePlateFileFormats(),
            printHost: LinuxCupsPrintHost.IsAvailable ? new LinuxCupsPrintHost(new PdfRasterPageWriter()) : null,
            printPermission: OfficeComposition.GetPlatePdfPrintPermission,
            scanHost: LinuxScanHost.IsAvailable ? new LinuxScanHost() : null);

        if (!string.IsNullOrWhiteSpace(options.InitialPath))
        {
            if (Directory.Exists(options.InitialPath))
                app.OpenFolder(options.InitialPath);
            else if (File.Exists(options.InitialPath))
                app.OpenFile(options.InitialPath);
        }

        return new AppCallbacks(
            IsInvalidated: () => host.IsInvalidated,
            Dispatch: app.Dispatch,
            Tick: () => { if (app.WantsTick) app.AnimationTick(); },
            RenderFrame: () => app.RenderFrame(),
            Dispose: () => { app.Dispose(); host.Dispose(); });
    }

    public static int RunCells(OfficeAppOptions options) =>
        RunInLoop(options, "Broiler Cells", CellsPalette.Canvas, CreateCellsApp);

    public static void OpenCells(OfficeAppOptions options) =>
        OpenNonModal(options, "Broiler Cells", CreateCellsApp);

    private static AppCallbacks CreateCellsApp(
        IBroilerSurface surface,
        LinuxOpenGlRenderer renderer,
        LinuxX11Clipboard? clipboard,
        Action requestClose,
        OfficeAppOptions _)
    {
        long frameIndex = 0;
        var host = new CellsUiHost(
            () => surface.Size,
            () => surface.DpiScale,
            static () => { },
            renderList => renderer.Render(surface, renderList, new BFrameContext(CellsPalette.Canvas, frameIndex++, RenderOptions)),
            () => clipboard is not null && clipboard.TryGetText(out string text) ? text : null,
            text => clipboard?.SetText(text),
            getRenderer: () => renderer);

        var app = new CellsApp(
            host,
            requestClose,
            setWindowTitle: surface is LinuxOpenGlX11WindowSurface x11 ? x11.SetTitle : null,
            printHost: LinuxCupsPrintHost.IsAvailable ? new LinuxCupsPrintHost(new PdfRasterPageWriter()) : null);

        return new AppCallbacks(
            IsInvalidated: () => host.IsInvalidated,
            Dispatch: evt => app.Dispatch(evt),
            Tick: () => { if (app.WantsTick) app.AnimationTick(); },
            RenderFrame: () => app.RenderFrame(),
            Dispose: () => { app.Dispose(); host.Dispose(); });
    }

    public static int RunMail(OfficeAppOptions options)
    {
        var application = CreateAndInitializeMailApplication(options);
        try
        {
            return RunInLoop(options, "Broiler Mail", StandardControlPaint.Theme.SurfaceAlt,
                (_, _, _, _, _) => throw new NotSupportedException("Use direct surface binding."),
                customFactory: (surface, renderer, clipboard, _, _) => CreateMailAppCallbacks(application, surface, renderer, clipboard));
        }
        finally
        {
            application.Dispose();
        }
    }

    public static void OpenMail(OfficeAppOptions options)
    {
        var application = CreateAndInitializeMailApplication(options);
        try
        {
            OpenNonModal(
                options,
                "Broiler Mail",
                (surface, renderer, clipboard, _, _) => CreateMailAppCallbacks(application, surface, renderer, clipboard),
                companion: application);
        }
        catch
        {
            application.Dispose();
            throw;
        }
    }

    private static MailApplication CreateAndInitializeMailApplication(OfficeAppOptions options)
    {
        StandardControlPaint.ApplyTheme(StandardThemeTokens.Light);
        string dataDirectory = options.DataDirectory ?? OfficeComposition.DefaultMailDataDirectory;
        var application = OfficeComposition.CreateMailApplication(
            new OfficeLinuxCredentialStore(Path.Combine(dataDirectory, "credentials")),
            dataDirectory,
            directory => OpenMail(options with { DataDirectory = directory }));
        try
        {
            application.InitializeAsync().GetAwaiter().GetResult();
            return application;
        }
        catch
        {
            application.Dispose();
            throw;
        }
    }

    private static AppCallbacks CreateMailAppCallbacks(
        MailApplication application,
        IBroilerSurface surface,
        LinuxOpenGlRenderer renderer,
        LinuxX11Clipboard? clipboard)
    {
        var host = new LinuxMailUiHost(renderer, surface, clipboard);
        var dispatcher = new ImmediateUiDispatcher();
        var session = new StandardUiSessionBuilder().WithDispatcher(dispatcher).Build(host);
        MailShellViewModel model = application.CreateViewModel(dispatcher);
        StandardControlPaint.ApplyTheme(AppearancePolicy.Resolve(model.Settings.Settings.Theme, UiSystemSettings.Default));
        var shell = new MailShellView(model);
        shell.Attach(session);
        var keyboard = shell.CreateKeyboardNavigation(session);
        session.SetFocus(shell.NavigationFocus);
        var appearance = new AppearanceController(session, model.Settings, null);

        return new AppCallbacks(
            IsInvalidated: () => host.IsInvalidated,
            Dispatch: evt =>
            {
                if (!keyboard.Handle(evt))
                    session.DispatchInput(evt);
            },
            Tick: static () => { },
            RenderFrame: () => session.RenderFrame(),
            Dispose: () =>
            {
                shell.Dispose();
                appearance.Dispose();
                session.Dispose();
            });
    }

    private sealed record AppCallbacks(
        Func<bool> IsInvalidated,
        Action<UiInputEvent> Dispatch,
        Action Tick,
        Action RenderFrame,
        Action Dispose);

    private sealed class ActiveWindowSession : IAsyncDisposable
    {
        public required LinuxOpenGlRenderer Renderer { get; init; }
        public required IBroilerSurface Surface { get; init; }
        public required LinuxOpenGlX11WindowSurface? X11Window { get; init; }
        public required LinuxX11Clipboard? Clipboard { get; init; }
        public required LinuxInputCoordinator Input { get; init; }
        public required AppCallbacks App { get; init; }
        public required IDisposable? Companion { get; init; }
        public required OfficeAppOptions Options { get; init; }
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public bool FirstFrame { get; set; } = true;
        public bool InputInitialized { get; set; }
        public bool CloseRequested { get; set; }
        public bool Disposed { get; private set; }

        public bool IsClosed => CloseRequested || (X11Window is not null && X11Window.IsCloseRequested);

        public async ValueTask StepAsync(CancellationToken cancellationToken)
        {
            if (Disposed)
                return;

            if (!InputInitialized)
            {
                InputInitialized = true;
                await Input.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }

            bool processedWindowEvents = false;
            Clipboard?.ProcessPendingEvents();
            if (X11Window is not null)
            {
                processedWindowEvents = X11Window.ProcessPendingEvents();
                bool inputActive = Options.IgnoreInputFocus || X11Window.IsFocused;
                await Input.SetActiveAsync(inputActive, cancellationToken).ConfigureAwait(false);
            }

            Input.SetViewport(Surface.Size);
            if (X11Window is not null && X11Window.TryGetPointerPosition(out int pointerX, out int pointerY, out _))
            {
                double scale = Surface.DpiScale <= 0 ? 1.0 : Surface.DpiScale;
                Input.SetExternalPointer(pointerX / scale, pointerY / scale);
            }

            int drained = Input.Drain(App.Dispatch);
            App.Tick();

            if (FirstFrame || App.IsInvalidated() || processedWindowEvents || drained > 0)
            {
                FirstFrame = false;
                App.RenderFrame();
            }

            if (!Options.OpenWindow ||
                (!Options.RunUntilClose && (DateTimeOffset.UtcNow - StartedAt).TotalMilliseconds >= Options.DurationMilliseconds))
            {
                CloseRequested = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Disposed)
                return;

            Disposed = true;
            try { await Input.SetActiveAsync(false, CancellationToken.None).ConfigureAwait(false); } catch { }
            try { await Input.DisposeAsync().ConfigureAwait(false); } catch { }
            try { App.Dispose(); } catch { }
            try { Companion?.Dispose(); } catch { }
            try { Clipboard?.Dispose(); } catch { }
            try { Surface.Dispose(); } catch { }
            try { Renderer.Dispose(); } catch { }
        }
    }

    private static readonly List<ActiveWindowSession> ActiveWindows = [];
    private static bool _hostLoopActive;
    private static bool _hostOpenWindow;

    public static bool HasActiveWindows => ActiveWindows.Count > 0;

    public static void BeginHostLoop(bool openWindow)
    {
        _hostLoopActive = true;
        _hostOpenWindow = openWindow;
    }

    public static async ValueTask StepActiveWindowsAsync(CancellationToken cancellationToken)
    {
        foreach (ActiveWindowSession session in ActiveWindows.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await session.StepAsync(cancellationToken).ConfigureAwait(false);
            if (session.IsClosed)
            {
                ActiveWindows.Remove(session);
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public static async ValueTask EndHostLoopAsync()
    {
        _hostLoopActive = false;
        foreach (ActiveWindowSession session in ActiveWindows.ToArray())
        {
            ActiveWindows.Remove(session);
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static ActiveWindowSession CreateWindowSession(
        OfficeAppOptions options,
        string appName,
        Func<IBroilerSurface, LinuxOpenGlRenderer, LinuxX11Clipboard?, Action, OfficeAppOptions, AppCallbacks> factory,
        IDisposable? companion = null)
    {
        if (options.OpenWindow && !OperatingSystem.IsLinux())
        {
            Console.WriteLine($"Windowed {appName} uses the Linux X11/OpenGL backend; running offscreen on this OS.");
            options = options with { OpenWindow = false, EnableEvdevInput = false, RunUntilClose = false };
        }

        LinuxOpenGlRenderer renderer = new();
        BSurfaceDescriptor descriptor = BSurfaceDescriptor.Default(new BSize(options.Width, options.Height));
        IBroilerSurface surface = options.OpenWindow
            ? renderer.CreateX11WindowSurface(descriptor, appName)
            : renderer.CreateSurface(descriptor);

        LinuxOpenGlX11WindowSurface? x11Window = surface as LinuxOpenGlX11WindowSurface;
        bool canUseEvdev = options.EnableEvdevInput && x11Window is not null;
        LinuxX11Clipboard? clipboard = x11Window is not null ? LinuxX11Clipboard.TryOpen() : null;
        ActiveWindowSession? session = null;
        try
        {
            AppCallbacks app = factory(surface, renderer, clipboard, () =>
            {
                if (session is not null)
                    session.CloseRequested = true;
            }, options);
            var input = new LinuxInputCoordinator(canUseEvdev, Console.WriteLine, externalPointer: x11Window is not null, applicationName: appName);
            session = new ActiveWindowSession
            {
                Renderer = renderer,
                Surface = surface,
                X11Window = x11Window,
                Clipboard = clipboard,
                Input = input,
                App = app,
                Companion = companion,
                Options = options,
            };
            return session;
        }
        catch
        {
            clipboard?.Dispose();
            surface.Dispose();
            renderer.Dispose();
            throw;
        }
    }

    private static void OpenNonModal(
        OfficeAppOptions options,
        string appName,
        Func<IBroilerSurface, LinuxOpenGlRenderer, LinuxX11Clipboard?, Action, OfficeAppOptions, AppCallbacks> factory,
        IDisposable? companion = null)
    {
        if (!_hostLoopActive)
        {
            RunInLoop(options, appName, default, factory);
            companion?.Dispose();
            return;
        }

        OfficeAppOptions resolved = _hostOpenWindow
            ? options
            : options with { OpenWindow = false, EnableEvdevInput = false, RunUntilClose = false };
        ActiveWindowSession session = CreateWindowSession(resolved, appName, factory, companion);
        ActiveWindows.Add(session);
    }

    private static int RunInLoop(
        OfficeAppOptions options,
        string appName,
        BColor clearColor,
        Func<IBroilerSurface, LinuxOpenGlRenderer, LinuxX11Clipboard?, Action, OfficeAppOptions, AppCallbacks> factory,
        Func<IBroilerSurface, LinuxOpenGlRenderer, LinuxX11Clipboard?, Action, OfficeAppOptions, AppCallbacks>? customFactory = null)
    {
        if (options.OpenWindow && !OperatingSystem.IsLinux())
        {
            Console.WriteLine($"Windowed {appName} uses the Linux X11/OpenGL backend; running offscreen on this OS.");
            options = options with { OpenWindow = false, EnableEvdevInput = false, RunUntilClose = false };
        }

        using CancellationTokenSource shutdown = new();
        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        }
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            int exitCode = 0;
            UiThreadSynchronizationContext.Run(async () =>
            {
                BeginHostLoop(options.OpenWindow);
                ActiveWindowSession primary = CreateWindowSession(options, appName, customFactory ?? factory);
                try
                {
                    using PeriodicTimer timer = new(options.OpenWindow ? InteractivePollInterval : OffscreenPollInterval);
                    do
                    {
                        shutdown.Token.ThrowIfCancellationRequested();
                        if (!primary.Disposed)
                        {
                            await primary.StepAsync(shutdown.Token).ConfigureAwait(false);
                            if (primary.IsClosed)
                                await primary.DisposeAsync().ConfigureAwait(false);
                        }

                        await StepActiveWindowsAsync(shutdown.Token).ConfigureAwait(false);
                    }
                    while ((!primary.Disposed || HasActiveWindows) &&
                           await timer.WaitForNextTickAsync(shutdown.Token).ConfigureAwait(false));
                }
                finally
                {
                    await primary.DisposeAsync().ConfigureAwait(false);
                    await EndHostLoopAsync().ConfigureAwait(false);
                }
            });
            return exitCode;
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }

    private sealed class LinuxMailUiHost(
        IBroilerRenderer renderer,
        IBroilerSurface surface,
        LinuxX11Clipboard? clipboard) : IUiHost, IUiClipboardHost, IUiTextInputHost, IUiImageHost
    {
        private long _frameIndex;
        public bool IsInvalidated { get; private set; } = true;
        public BSize ViewportSize => surface.Size;
        public double Scale => surface.DpiScale;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) => IsInvalidated = true;

        public void Present(BRenderList renderList)
        {
            IsInvalidated = false;
            try
            {
                renderer.Render(surface, renderList, BFrameContext.Default.WithFrameIndex(_frameIndex++));
            }
            catch
            {
                IsInvalidated = true;
                throw;
            }
        }

        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return clipboard is not null && clipboard.TryGetText(out text);
        }

        public void SetText(string text) => clipboard?.SetText(text);
        public void PublishCaret(UiTextCaretInfo caret) { }
        public void ClearCaret(UiElement owner) { }

        public BImageHandle CreateImage(ReadOnlySpan<byte> encodedImage)
        {
            if (encodedImage.IsEmpty) return BImageHandle.Invalid;
            try { return renderer.CreateImage(encodedImage); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { return BImageHandle.Invalid; }
        }

        public BImageHandle CreateImage(BPixelBuffer pixels)
        {
            try { return renderer.CreateImage(pixels); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { return BImageHandle.Invalid; }
        }

        public void ReleaseImage(BImageHandle image)
        {
            if (image.IsValid)
                renderer.ReleaseImage(image);
        }
    }
}
#endif
