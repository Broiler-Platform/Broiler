using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Broiler.App.Rendering;
using Broiler.Graphics;
using Broiler.Graphics.Windows;
using Broiler.HtmlBridge;
using Broiler.HTML.Core.Entities;
using Broiler.HTML.Graphics;

namespace Broiler.App.Graphics;

/// <summary>
/// Browser shell hosted on a Direct2D window. Provides the navigation chrome (address bar,
/// back/forward/refresh, favorites), translates Win32 input into HTML hit-testing, drives
/// scrolling, and steps script-driven animations through the window's animation timer.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed class BrowserWindow : Direct2DWindow
{
    private const int DesiredClientWidth = 1100;
    private const int DesiredClientHeight = 800;

    private const double ToolbarHeight = 40;
    private const double FavoritesBarHeight = 30;
    private const double StatusBarHeight = 24;
    private const double Margin = 8;
    private const double ControlHeight = 28;
    private const double NavButtonWidth = 36;
    private const double StopButtonWidth = 58;
    private const double GoButtonWidth = 54;
    private const double StarWidth = 36;

    private const double WheelScrollStep = 60;
    private const double KeyScrollStep = 48;
    private const double AnimationIntervalMs = 16;
    private const int MaxSavePath = 32768;

    private const int OfnOverwritePrompt = 0x00000002;
    private const int OfnHideReadOnly = 0x00000004;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnExplorer = 0x00080000;

    private const uint MfString = 0x00000000;
    private const uint MfGrayed = 0x00000001;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;

    private const int ContextOpenLink = 1001;
    private const int ContextSaveLink = 1002;
    private const int ContextBack = 1003;
    private const int ContextForward = 1004;
    private const int ContextReload = 1005;
    private const int ContextStop = 1006;

    private static readonly HashSet<string> DownloadFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".7z", ".avi", ".bin", ".bmp", ".csv", ".dll", ".dmg", ".doc", ".docx", ".exe",
        ".gif", ".gz", ".iso", ".jar", ".jpeg", ".jpg", ".json", ".mov", ".mp3", ".mp4",
        ".msi", ".pdf", ".png", ".ppt", ".pptx", ".rar", ".tar", ".tgz", ".txt", ".wasm",
        ".webm", ".webp", ".xls", ".xlsx", ".xml", ".zip",
    };

    private readonly string? _initialUrl;
    private HtmlContainer _container = CreateHtmlContainer();
    private readonly FavoritesManager _favorites = new();

    private readonly List<string> _history = [];
    private int _historyIndex = -1;

    private BButtonControl? _backButton;
    private BButtonControl? _forwardButton;
    private BButtonControl? _refreshButton;
    private BButtonControl? _stopButton;
    private BButtonControl? _goButton;
    private BButtonControl? _starButton;
    private BEditControl? _urlEdit;
    private BEditControl? _formEdit;
    private BLabelControl? _statusBar;
    private readonly List<(BButtonControl Button, string Url)> _favoriteButtons = [];

    private InteractiveSession? _session;
    private HtmlGraphicsRenderList? _renderList;
    private bool _hasContent;
    private bool _layoutDirty = true;
    private bool _renderDirty = true;
    private float _scrollY;
    private float _contentHeight;
    private float _viewportHeight;
    private string _baseUrl = string.Empty;
    private string? _pendingAnchor;
    private bool _suppressNavigation;
    private bool _isShuttingDown;
    private bool _isPageBusy;
    private long _navigationGeneration;
    private CancellationTokenSource? _navigationCancellation;
    private CancellationTokenSource? _downloadCancellation;
    private bool _updatingFormEdit;
    private bool _skipNextFormPointerUp;
    private FormInputElementData<RectangleF>? _activeFormInput;
    private PointF _activeFormInputDocumentPoint;

    public BrowserWindow(string? initialUrl)
        : base(new BWindowOptions
        {
            Title = "Broiler",
            ClientWidth = DesiredClientWidth,
            ClientHeight = DesiredClientHeight,
            ClearColor = BColor.White,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
        })
    {
        _initialUrl = initialUrl;
        _container.LinkClicked += OnLinkClicked;
    }

    // The render content area sits below the toolbar and favorites bar.
    protected override BRect GetRenderBounds(BSize clientSize)
    {
        double top = ToolbarHeight + FavoritesBarHeight;
        return new BRect(0, top, clientSize.Width, Math.Max(0, clientSize.Height - top - StatusBarHeight));
    }

    protected override BFrameContext CreateFrameContext(long frameIndex) =>
        new(ResolveClearColor(), frameIndex, Options.RenderOptions);

    protected override void OnCreated() => Diagnostics.Guard(nameof(OnCreated), OnCreatedCore);

    private void OnCreatedCore()
    {
        _backButton = MakeButton("←", () => GoHistory(-1));
        _forwardButton = MakeButton("→", () => GoHistory(1));
        _refreshButton = MakeButton("↻", Reload);
        _stopButton = MakeButton("Stop", StopLoading);
        _stopButton.Enabled = false;
        _urlEdit = CreateEditControl(new BControlOptions { Text = string.Empty });
        _urlEdit.Submitted += (_, _) => NavigateTo(_urlEdit!.Text);
        _starButton = MakeButton("☆", ToggleFavorite);
        _goButton = MakeButton("Go", () => NavigateTo(_urlEdit!.Text));
        _statusBar = CreateLabelControl(new BControlOptions { Text = "Ready" });

        _favorites.Load();
        RefreshFavoritesBar();
        LayoutControls(ClientSize);

        NavigateTo(_initialUrl ?? "about:blank");
        _urlEdit.Focus();
    }

    protected override void OnResized(BSize clientSize, double dpiScale)
    {
        LayoutControls(clientSize);
        LayoutFormEdit();
        MarkLayoutDirty();
    }

    protected override void OnGraphicsResourcesReleasing()
    {
        DisposeRenderList();
        MarkLayoutDirty();
    }

    protected override void OnClosing() => Diagnostics.Guard(nameof(OnClosing), BeginShutdown);

    // ---- Navigation -------------------------------------------------------

    private void NavigateTo(string url)
    {
        if (_isShuttingDown)
            return;

        if (string.IsNullOrWhiteSpace(url))
            return;

        url = NormalizeInput(url);

        // Drop forward history when navigating somewhere new.
        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

        _history.Add(url);
        _historyIndex = _history.Count - 1;
        LoadUrl(url);
    }

    private void GoHistory(int delta)
    {
        if (_isShuttingDown)
            return;

        int target = _historyIndex + delta;
        if (target < 0 || target >= _history.Count)
            return;

        _historyIndex = target;
        LoadUrl(_history[target]);
    }

    private void Reload()
    {
        if (_isShuttingDown)
            return;

        if (_historyIndex >= 0 && _historyIndex < _history.Count)
            LoadUrl(_history[_historyIndex]);
    }

    private void StopLoading()
    {
        if (_isShuttingDown || !_isPageBusy)
            return;

        CancelPendingNavigation();
        CancelPendingDownload();
        StopSession();
        _navigationGeneration++;
        SetBusy(false);
        SetStatus("Stopped");
        RequestInvalidate();
    }

    private void LoadUrl(string url)
    {
        if (_isShuttingDown)
            return;

        long navigationGeneration = BeginNavigation();
        SetUrlText(url);
        UpdateNavigationButtons();
        UpdateStarButton();
        HideFormEdit();
        StopSession();
        _scrollY = 0;
        _pendingAnchor = null;

        if (string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            _baseUrl = string.Empty;
            ReplaceContainer(CreateContentContainer(WelcomePage, string.Empty));
            _hasContent = true;
            SetBusy(false);
            SetStatus("Ready");
            MarkLayoutDirty();
            RequestInvalidate();
            return;
        }

        ShowLoadingPage(url);

        var cancellation = new CancellationTokenSource();
        _navigationCancellation = cancellation;
        _ = LoadUrlInBackgroundAsync(navigationGeneration, url, cancellation);
    }

    private long BeginNavigation()
    {
        CancelPendingNavigation();
        CancelPendingDownload();
        return ++_navigationGeneration;
    }

    private async Task LoadUrlInBackgroundAsync(
        long navigationGeneration,
        string url,
        CancellationTokenSource cancellation)
    {
        NavigationLoadResult? result = null;

        try
        {
            result = await LoadUrlOnWorkerAsync(url, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            result = NavigationLoadResult.FromError(ex);
        }

        if (!PostToUiThread(() => CompleteBackgroundLoad(navigationGeneration, cancellation, result)))
        {
            result?.Dispose();
            cancellation.Dispose();
        }
    }

    private static async Task<NavigationLoadResult> LoadUrlOnWorkerAsync(string url, CancellationToken cancellationToken)
    {
        using var pipeline = new RenderingPipeline(
            new PageLoader(new HttpClient()),
            new ScriptExtractor(),
            new ScriptEngine());

        var (normalisedUrl, content) = await pipeline.LoadPageAsync(url, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        string html = HtmlPostProcessor.Process(content.Html);
        InteractiveSession? session = null;
        try
        {
            session = pipeline.ExecuteScriptsInteractive(content);
            cancellationToken.ThrowIfCancellationRequested();

            if (session != null)
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

            HtmlContainer container = CreateContentContainer(html, normalisedUrl);
            return NavigationLoadResult.FromSuccess(normalisedUrl, container, session);
        }
        catch
        {
            session?.Dispose();
            throw;
        }
    }

    private void CompleteBackgroundLoad(
        long navigationGeneration,
        CancellationTokenSource cancellation,
        NavigationLoadResult? result)
    {
        try
        {
            if (ReferenceEquals(_navigationCancellation, cancellation))
                _navigationCancellation = null;

            if (_isShuttingDown
                || cancellation.IsCancellationRequested
                || navigationGeneration != _navigationGeneration)
            {
                result?.Dispose();
                return;
            }

            if (result is null)
                return;

            if (result.Error is { } error)
            {
                ShowErrorPage(error);
                return;
            }

            ApplyLoadedPage(result);
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private void ApplyLoadedPage(NavigationLoadResult result)
    {
        _baseUrl = result.NormalisedUrl;
        SetUrlText(result.NormalisedUrl);
        ReplaceContainer(result.TakeContainer());
        _hasContent = true;

        _session = result.TakeSession();
        if (_session != null && _session.HasPendingWork)
        {
            SetBusy(true);
            SetStatus("Rendering...");
            StartAnimationTimer(AnimationIntervalMs);
        }
        else
        {
            SetBusy(false);
            SetStatus("Done");
        }

        if (Uri.TryCreate(result.NormalisedUrl, UriKind.Absolute, out Uri? uri)
            && !string.IsNullOrEmpty(uri.Fragment) && uri.Fragment.Length > 1)
        {
            _pendingAnchor = uri.Fragment[1..];
        }

        MarkLayoutDirty();
        RequestInvalidate();
    }

    private void ShowLoadingPage(string url)
    {
        _baseUrl = string.Empty;
        SetBusy(true);
        SetStatus($"Loading {url}...");
        ReplaceContainer(CreateContentContainer($"""
<html>
<body style='font-family: Segoe UI, Arial, sans-serif; margin: 40px; color: #333;'>
    <p>Loading {System.Net.WebUtility.HtmlEncode(url)}...</p>
</body>
</html>
""", string.Empty));
        _hasContent = true;
        MarkLayoutDirty();
        RequestInvalidate();
    }

    private void ShowErrorPage(Exception ex)
    {
        _baseUrl = string.Empty;
        SetBusy(false);
        SetStatus("Error loading page");
        ReplaceContainer(CreateContentContainer($"<html><body><h1>Error</h1><p>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p></body></html>", string.Empty));
        _hasContent = true;
        MarkLayoutDirty();
        RequestInvalidate();
    }

    private void OnLinkClicked(object? sender, HtmlLinkClickedEventArgs e)
    {
        e.Handled = true;
        if (_isShuttingDown || _suppressNavigation)
            return;

        string link = ResolveLinkUrl(e.Link);
        if (ShouldDownloadLink(link, e.Attributes, out string suggestedFileName))
        {
            SaveLinkAs(link, suggestedFileName);
            return;
        }

        NavigateTo(link);
    }

    // ---- Rendering --------------------------------------------------------

    protected override BRenderList? BuildRenderList(BSize clientSize)
    {
        try
        {
            return BuildRenderListCore(clientSize);
        }
        catch (Exception ex)
        {
            Diagnostics.Log(nameof(BuildRenderList), ex);
            return null;
        }
    }

    private BRenderList? BuildRenderListCore(BSize clientSize)
    {
        if (_isShuttingDown || !_hasContent || clientSize.IsEmpty || Renderer is null)
            return null;

        float vpW = (float)clientSize.Width;
        float vpH = (float)clientSize.Height;
        _viewportHeight = vpH;

        if (_layoutDirty)
        {
            _container.Location = PointF.Empty;
            _container.MaxSize = new SizeF(vpW, vpH);
            _container.PerformLayout(new RectangleF(0, 0, vpW, vpH));
            _contentHeight = _container.ActualSize.Height;
            _layoutDirty = false;
            _renderDirty = true;
            ResolvePendingAnchor();
        }

        ClampScroll(vpH);
        RefreshActiveFormEditLayout();

        if (_renderDirty || _renderList is null)
        {
            _container.ScrollOffset = new PointF(0, -_scrollY);
            DisposeRenderList();
            _renderList = _container.CreateRenderList(Renderer, new RectangleF(0, 0, vpW, vpH));
            _renderDirty = false;
        }

        return _renderList.RenderList;
    }

    private void ResolvePendingAnchor()
    {
        if (_pendingAnchor is null)
            return;

        RectangleF? rect = _container.GetElementRectangle(_pendingAnchor);
        _pendingAnchor = null;
        if (rect is { } r)
        {
            _scrollY = Math.Max(0, r.Y);
            _renderDirty = true;
        }
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

    // ---- Input ------------------------------------------------------------

    protected override void OnPointerDown(BPointerEventArgs e) => Diagnostics.Guard(nameof(OnPointerDown), () =>
    {
        if (_hasContent && !_isShuttingDown)
        {
            PointF location = ToPoint(e.Position);
            if (IsChangedOrCurrentButton(e, BMouseButtons.Right))
                return;

            if (IsChangedOrCurrentButton(e, BMouseButtons.Left) && TryActivateFormEdit(location))
            {
                _skipNextFormPointerUp = true;
                return;
            }

            _container.HandleMouseDown(
                location,
                IsChangedOrCurrentButton(e, BMouseButtons.Left),
                IsChangedOrCurrentButton(e, BMouseButtons.Right));
            InvalidateRenderedContent();
        }
    });

    protected override void OnPointerMove(BPointerEventArgs e) => Diagnostics.Guard(nameof(OnPointerMove), () =>
    {
        if (_hasContent && !_isShuttingDown)
        {
            _container.HandleMouseMove(ToPoint(e.Position), e.LeftButton, e.RightButton);
            InvalidateRenderedContent();
        }
    });

    protected override void OnPointerUp(BPointerEventArgs e) => Diagnostics.Guard(nameof(OnPointerUp), () =>
    {
        // HandleMouseUp raises LinkClicked for links, which drives navigation.
        if (_hasContent && !_isShuttingDown)
        {
            if (IsChangedOrCurrentButton(e, BMouseButtons.Right))
            {
                ShowHtmlContextMenu(ToPoint(e.Position));
                return;
            }

            if (_skipNextFormPointerUp && IsChangedOrCurrentButton(e, BMouseButtons.Left))
            {
                _skipNextFormPointerUp = false;
                return;
            }

            _container.HandleMouseUp(
                ToPoint(e.Position),
                IsChangedOrCurrentButton(e, BMouseButtons.Left),
                IsChangedOrCurrentButton(e, BMouseButtons.Right));
            InvalidateRenderedContent();
        }
    });

    protected override void OnPointerLeave() => Diagnostics.Guard(nameof(OnPointerLeave), () =>
    {
        if (_hasContent && !_isShuttingDown)
        {
            _container.HandleMouseLeave();
            InvalidateRenderedContent();
        }
    });

    protected override void OnMouseWheel(BMouseWheelEventArgs e)
    {
        if (_isShuttingDown)
            return;

        ScrollBy(-(float)(e.Delta * WheelScrollStep));
    }

    protected override void OnKeyDown(BKeyEventArgs e) => Diagnostics.Guard(nameof(OnKeyDown), () =>
    {
        if (_hasContent && !_isShuttingDown)
        {
            _container.HandleKeyDown(
                e.Control,
                e.VirtualKey == BVirtualKey.A,
                e.VirtualKey == BVirtualKey.C);
            InvalidateRenderedContent();
        }

        switch (e.VirtualKey)
        {
            case BVirtualKey.Down: ScrollBy((float)KeyScrollStep); break;
            case BVirtualKey.Up: ScrollBy(-(float)KeyScrollStep); break;
            case BVirtualKey.PageDown: ScrollBy(Math.Max(1, _viewportHeight - 40)); break;
            case BVirtualKey.PageUp: ScrollBy(-Math.Max(1, _viewportHeight - 40)); break;
            case BVirtualKey.Home: SetScroll(0); break;
            case BVirtualKey.End: SetScroll(float.MaxValue); break;
            case BVirtualKey.F5: Reload(); break;
            case BVirtualKey.Left when e.Alt: GoHistory(-1); break;
            case BVirtualKey.Right when e.Alt: GoHistory(1); break;
        }
    });

    private void ScrollBy(float delta) => SetScroll(_scrollY + delta);

    private void SetScroll(float value)
    {
        if (_isShuttingDown)
            return;

        _scrollY = value;
        _renderDirty = true;
        LayoutFormEdit();
        RequestInvalidate();
    }

    private void InvalidateRenderedContent()
    {
        if (_isShuttingDown)
            return;

        _renderDirty = true;
        RequestInvalidate();
    }

    private bool TryActivateFormEdit(PointF location)
    {
        if (_isShuttingDown)
            return false;

        FormInputElementData<RectangleF>? input = _container.GetEditableInputAt(location);
        if (input is null)
        {
            HideFormEdit();
            return false;
        }

        if (!EnsureFormEdit())
            return false;

        _activeFormInput = input;
        _activeFormInputDocumentPoint = Center(input.Rectangle);

        _updatingFormEdit = true;
        try
        {
            if (!string.Equals(_formEdit!.Text, input.Value, StringComparison.Ordinal))
                _formEdit.Text = input.Value;
        }
        finally
        {
            _updatingFormEdit = false;
        }

        LayoutFormEdit();
        _formEdit!.Focus();
        return true;
    }

    private bool EnsureFormEdit()
    {
        if (_formEdit != null)
            return true;

        if (_isShuttingDown || IsDisposed)
            return false;

        _formEdit = CreateEditControl(new BControlOptions
        {
            Bounds = BRect.Empty,
            Text = string.Empty,
            Visible = false,
        });
        _formEdit.TextChanged += OnFormEditTextChanged;
        return true;
    }

    private void OnFormEditTextChanged(object? sender, EventArgs e)
    {
        if (_updatingFormEdit || _formEdit is null || _activeFormInput is null || _isShuttingDown)
            return;

        if (_container.SetEditableInputValueAtDocumentPoint(_activeFormInputDocumentPoint, _formEdit.Text))
        {
            MarkLayoutDirty();
            RequestInvalidate();
        }
    }

    private void RefreshActiveFormEditLayout()
    {
        if (_formEdit is null || _activeFormInput is null || _isShuttingDown)
            return;

        FormInputElementData<RectangleF>? refreshed =
            _container.GetEditableInputAtDocumentPoint(_activeFormInputDocumentPoint);
        if (refreshed is null)
        {
            HideFormEdit();
            return;
        }

        _activeFormInput = refreshed;
        LayoutFormEdit();
    }

    private void LayoutFormEdit()
    {
        if (_formEdit is null || _activeFormInput is null || _isShuttingDown)
            return;

        if (!TryGetFormEditBounds(_activeFormInput.Rectangle, out BRect bounds))
        {
            SafeSetFormEditVisible(false);
            return;
        }

        try
        {
            _formEdit.Bounds = bounds;
            _formEdit.Visible = true;
        }
        catch (ObjectDisposedException) when (_isShuttingDown)
        {
        }
    }

    private bool TryGetFormEditBounds(RectangleF documentRect, out BRect bounds)
    {
        double contentTop = ToolbarHeight + FavoritesBarHeight;
        double x = documentRect.X;
        double y = contentTop + documentRect.Y - _scrollY;
        double width = Math.Max(24, documentRect.Width);
        double height = Math.Max(18, documentRect.Height);

        BSize clientSize = ClientSize;
        BRect viewport = new(0, contentTop, clientSize.Width, Math.Max(0, clientSize.Height - contentTop - StatusBarHeight));
        BRect editBounds = new(x, y, width, height);
        BRect visibleBounds = editBounds.Intersect(viewport);

        if (visibleBounds.IsEmpty || visibleBounds.Height < height || visibleBounds.Width < 8)
        {
            bounds = BRect.Empty;
            return false;
        }

        bounds = new BRect(visibleBounds.X, visibleBounds.Y, visibleBounds.Width, height);
        return true;
    }

    private void HideFormEdit()
    {
        _activeFormInput = null;
        _skipNextFormPointerUp = false;
        SafeSetFormEditVisible(false);
    }

    private void SafeSetFormEditVisible(bool visible)
    {
        if (_formEdit is null)
            return;

        try
        {
            _formEdit.Visible = visible;
        }
        catch (ObjectDisposedException) when (_isShuttingDown)
        {
        }
    }

    private void DisposeFormEdit()
    {
        BEditControl? edit = _formEdit;
        _formEdit = null;
        _activeFormInput = null;
        _skipNextFormPointerUp = false;

        if (edit is null)
            return;

        edit.TextChanged -= OnFormEditTextChanged;
        try
        {
            edit.Dispose();
        }
        catch (ObjectDisposedException) when (_isShuttingDown)
        {
        }
    }

    private static PointF Center(RectangleF rectangle) =>
        new(rectangle.X + (rectangle.Width / 2), rectangle.Y + (rectangle.Height / 2));

    // ---- Animation --------------------------------------------------------

    protected override void OnAnimationTick() => Diagnostics.Guard(nameof(OnAnimationTick), OnAnimationTickCore);

    private void OnAnimationTickCore()
    {
        if (_isShuttingDown)
        {
            StopSession();
            return;
        }

        if (_session is null || !_session.HasPendingWork)
        {
            StopSession();
            SetBusy(false);
            SetStatus("Done");
            return;
        }

        string? html = _session.Step();
        if (html != null)
        {
            // Re-applying content mutates the container; avoid re-entrant navigation
            // if a script-driven change happens to look like a link activation.
            _suppressNavigation = true;
            try
            {
                HideFormEdit();
                _container.SetHtmlWithStyleSet(HtmlPostProcessor.Process(html), baseUrl: _baseUrl);
            }
            finally
            {
                _suppressNavigation = false;
            }
            MarkLayoutDirty();
            RequestInvalidate();
        }

        if (!_session.HasPendingWork)
        {
            StopSession();
            SetBusy(false);
            SetStatus("Done");
        }
    }

    private void StopSession()
    {
        try
        {
            if (!IsDisposed)
                StopAnimationTimer();
        }
        catch (ObjectDisposedException)
        {
        }

        _session?.Dispose();
        _session = null;
    }

    private void CancelPendingNavigation()
    {
        CancellationTokenSource? cancellation = _navigationCancellation;
        _navigationCancellation = null;
        cancellation?.Cancel();
    }

    private void CancelPendingDownload()
    {
        CancellationTokenSource? cancellation = _downloadCancellation;
        _downloadCancellation = null;
        cancellation?.Cancel();
    }

    private void ReplaceContainer(HtmlContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        DisposeRenderList();
        _container.LinkClicked -= OnLinkClicked;
        _container.Dispose();

        _container = container;
        _container.LinkClicked += OnLinkClicked;
    }

    private void BeginShutdown()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        SetBusy(false);
        CancelPendingNavigation();
        CancelPendingDownload();
        _container.LinkClicked -= OnLinkClicked;
        DisposeFormEdit();
        StopSession();
        DisposeRenderList();
    }

    private void DisposeRenderList()
    {
        HtmlGraphicsRenderList? renderList = _renderList;
        _renderList = null;
        _renderDirty = true;

        if (renderList is null)
            return;

        try
        {
            renderList.Dispose();
        }
        catch (ObjectDisposedException) when (_isShuttingDown)
        {
            // The native renderer may already be gone if disposal follows WM_DESTROY.
        }
    }

    private void RequestInvalidate()
    {
        if (_isShuttingDown || IsDisposed)
            return;

        try
        {
            Invalidate();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    // ---- Downloads & context menu ----------------------------------------

    private void ShowHtmlContextMenu(PointF location)
    {
        if (_isShuttingDown || NativeHandle == IntPtr.Zero)
            return;

        string? link = _container.GetLinkAt(location);
        string resolvedLink = string.IsNullOrWhiteSpace(link)
            ? string.Empty
            : ResolveLinkUrl(link);

        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return;

        try
        {
            if (!string.IsNullOrWhiteSpace(resolvedLink))
            {
                AppendContextMenuItem(menu, ContextOpenLink, "Open Link");
                AppendContextMenuItem(menu, ContextSaveLink, "Save Link As...");
                AppendMenu(menu, MfSeparator, UIntPtr.Zero, null);
            }

            AppendContextMenuItem(menu, ContextBack, "Back", _historyIndex > 0);
            AppendContextMenuItem(menu, ContextForward, "Forward", _historyIndex < _history.Count - 1);
            AppendContextMenuItem(menu, ContextReload, "Reload", _historyIndex >= 0 && _historyIndex < _history.Count);
            AppendContextMenuItem(menu, ContextStop, "Stop", _isPageBusy);

            NativePoint screenPoint = ContentToScreen(location);
            SetForegroundWindow(NativeHandle);
            int command = TrackPopupMenu(
                menu,
                TpmRightButton | TpmReturnCmd,
                screenPoint.X,
                screenPoint.Y,
                0,
                NativeHandle,
                IntPtr.Zero);

            switch (command)
            {
                case ContextOpenLink when !string.IsNullOrWhiteSpace(resolvedLink):
                    NavigateTo(resolvedLink);
                    break;

                case ContextSaveLink when !string.IsNullOrWhiteSpace(resolvedLink):
                    SaveLinkAs(resolvedLink, SuggestedFileNameFromDownload(
                        _container.GetAttributeAt(location, "download"),
                        resolvedLink));
                    break;

                case ContextBack:
                    GoHistory(-1);
                    break;

                case ContextForward:
                    GoHistory(1);
                    break;

                case ContextReload:
                    Reload();
                    break;

                case ContextStop:
                    StopLoading();
                    break;
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private static void AppendContextMenuItem(IntPtr menu, int command, string text, bool enabled = true) =>
        AppendMenu(menu, MfString | (enabled ? 0 : MfGrayed), new UIntPtr((uint)command), text);

    private NativePoint ContentToScreen(PointF location)
    {
        double scale = DpiScale;
        var point = new NativePoint
        {
            X = (int)Math.Round(location.X * scale),
            Y = (int)Math.Round((ToolbarHeight + FavoritesBarHeight + location.Y) * scale),
        };

        ClientToScreen(NativeHandle, ref point);
        return point;
    }

    private void SaveLinkAs(string link, string? suggestedFileName)
    {
        if (_isShuttingDown)
            return;

        try
        {
            string url = ResolveLinkUrl(link);
            string? destination = ShowSaveFileDialog(SuggestedFileNameFromDownload(suggestedFileName, url));
            if (string.IsNullOrWhiteSpace(destination))
                return;

            CancelPendingDownload();

            var cancellation = new CancellationTokenSource();
            _downloadCancellation = cancellation;
            SetBusy(true);
            SetStatus($"Downloading {Path.GetFileName(destination)}...");
            _ = DownloadLinkInBackgroundAsync(url, destination, cancellation);
        }
        catch (Exception ex)
        {
            Diagnostics.Log(nameof(SaveLinkAs), ex);
            SetBusy(false);
            SetStatus($"Download failed: {ex.Message}");
        }
    }

    private async Task DownloadLinkInBackgroundAsync(
        string url,
        string destination,
        CancellationTokenSource cancellation)
    {
        Exception? error = null;

        try
        {
            await DownloadUrlToFileAsync(url, destination, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryDeletePartial(destination);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        if (!PostToUiThread(() => CompleteDownload(cancellation, destination, error)))
            cancellation.Dispose();
    }

    private void CompleteDownload(
        CancellationTokenSource cancellation,
        string destination,
        Exception? error)
    {
        try
        {
            if (ReferenceEquals(_downloadCancellation, cancellation))
                _downloadCancellation = null;

            if (_isShuttingDown || cancellation.IsCancellationRequested)
                return;

            SetBusy(false);
            SetStatus(error is null
                ? $"Saved {Path.GetFileName(destination)}"
                : $"Download failed: {error.Message}");
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private static async Task DownloadUrlToFileAsync(
        string url,
        string destination,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (TryGetLocalFilePath(url, out string? localPath))
        {
            if (IsSameFilePath(localPath, destination))
                return;

            await using var source = new FileStream(
                localPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);
            await using var target = new FileStream(
                destination,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            uri = new Uri("https://" + url);

        if (string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
        {
            byte[] payload = DecodeDataUri(uri.OriginalString);
            await File.WriteAllBytesAsync(destination, payload, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new NotSupportedException($"Cannot download '{uri.Scheme}' URLs.");

        using var httpClient = new HttpClient();
        using HttpResponseMessage response = await httpClient
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream sourceStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var destinationStream = new FileStream(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] DecodeDataUri(string dataUri)
    {
        int comma = dataUri.IndexOf(',');
        if (comma < 0 || !dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("The data URL is not valid.");

        string metadata = dataUri[5..comma];
        string payload = dataUri[(comma + 1)..];
        bool isBase64 = metadata
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Contains("base64", StringComparer.OrdinalIgnoreCase);

        return isBase64
            ? Convert.FromBase64String(Uri.UnescapeDataString(payload))
            : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
    }

    private static bool TryGetLocalFilePath(string url, out string? localPath)
    {
        localPath = null;

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            if (!uri.IsFile)
                return false;

            localPath = uri.LocalPath;
            return File.Exists(localPath);
        }

        if (!File.Exists(url))
            return false;

        localPath = Path.GetFullPath(url);
        return true;
    }

    private static bool IsSameFilePath(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeletePartial(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private string? ShowSaveFileDialog(string suggestedFileName)
    {
        IntPtr fileBuffer = IntPtr.Zero;
        IntPtr filterBuffer = IntPtr.Zero;
        IntPtr titleBuffer = IntPtr.Zero;
        IntPtr defaultExtensionBuffer = IntPtr.Zero;

        try
        {
            string safeFileName = SanitizeFileName(suggestedFileName);
            fileBuffer = AllocateSaveFileBuffer(safeFileName);
            filterBuffer = Marshal.StringToHGlobalUni("All files (*.*)\0*.*\0\0");
            titleBuffer = Marshal.StringToHGlobalUni("Save As");

            string extension = Path.GetExtension(safeFileName);
            if (!string.IsNullOrWhiteSpace(extension))
                defaultExtensionBuffer = Marshal.StringToHGlobalUni(extension.TrimStart('.'));

            var openFileName = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                hwndOwner = NativeHandle,
                lpstrFilter = filterBuffer,
                lpstrFile = fileBuffer,
                nMaxFile = MaxSavePath,
                lpstrTitle = titleBuffer,
                Flags = OfnExplorer
                    | OfnOverwritePrompt
                    | OfnHideReadOnly
                    | OfnNoChangeDir
                    | OfnPathMustExist,
                lpstrDefExt = defaultExtensionBuffer,
            };

            if (!GetSaveFileName(ref openFileName))
                return null;

            string? destination = Marshal.PtrToStringUni(fileBuffer);
            return string.IsNullOrWhiteSpace(destination) ? null : destination;
        }
        finally
        {
            FreeHGlobal(fileBuffer);
            FreeHGlobal(filterBuffer);
            FreeHGlobal(titleBuffer);
            FreeHGlobal(defaultExtensionBuffer);
        }
    }

    private static IntPtr AllocateSaveFileBuffer(string suggestedFileName)
    {
        IntPtr buffer = Marshal.AllocHGlobal(MaxSavePath * sizeof(char));
        byte[] zeroes = new byte[MaxSavePath * sizeof(char)];
        Marshal.Copy(zeroes, 0, buffer, zeroes.Length);

        if (suggestedFileName.Length == 0)
            return buffer;

        string trimmed = suggestedFileName.Length >= MaxSavePath
            ? suggestedFileName[..(MaxSavePath - 1)]
            : suggestedFileName;
        byte[] bytes = Encoding.Unicode.GetBytes(trimmed);
        Marshal.Copy(bytes, 0, buffer, bytes.Length);
        return buffer;
    }

    private static void FreeHGlobal(IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
            Marshal.FreeHGlobal(pointer);
    }

    private string ResolveLinkUrl(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return link;

        link = link.Trim();

        if (link.StartsWith('#'))
        {
            string current = CurrentHistoryUrl();
            if (!string.IsNullOrWhiteSpace(current))
            {
                int hash = current.IndexOf('#');
                if (hash >= 0)
                    current = current[..hash];
                return current + link;
            }
        }

        if (Uri.TryCreate(link, UriKind.Absolute, out _))
            return link;

        string baseUrl = !string.IsNullOrWhiteSpace(_baseUrl)
            ? _baseUrl
            : CurrentHistoryUrl();

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri)
            && Uri.TryCreate(baseUri, link, out Uri? resolved))
        {
            return resolved.AbsoluteUri;
        }

        return link;
    }

    private string CurrentHistoryUrl() =>
        _historyIndex >= 0 && _historyIndex < _history.Count
            ? _history[_historyIndex]
            : string.Empty;

    private static bool ShouldDownloadLink(
        string link,
        IReadOnlyDictionary<string, string> attributes,
        out string suggestedFileName)
    {
        if (TryGetAttribute(attributes, "download", out string? downloadName))
        {
            suggestedFileName = SuggestedFileNameFromDownload(downloadName, link);
            return true;
        }

        if (LooksLikeDownloadTarget(link))
        {
            suggestedFileName = GuessFileNameFromUrl(link);
            return true;
        }

        suggestedFileName = string.Empty;
        return false;
    }

    private static bool LooksLikeDownloadTarget(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return false;

        string path = link;
        if (Uri.TryCreate(link, UriKind.Absolute, out Uri? uri))
            path = uri.AbsolutePath;

        int queryStart = path.IndexOfAny(['?', '#']);
        if (queryStart >= 0)
            path = path[..queryStart];

        string extension = Path.GetExtension(Uri.UnescapeDataString(path));
        return !string.IsNullOrWhiteSpace(extension) && DownloadFileExtensions.Contains(extension);
    }

    private static bool TryGetAttribute(
        IReadOnlyDictionary<string, string> attributes,
        string name,
        out string? value)
    {
        if (attributes.TryGetValue(name, out value))
            return true;

        foreach (var pair in attributes)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string SuggestedFileNameFromDownload(string? downloadValue, string link)
    {
        if (!string.IsNullOrWhiteSpace(downloadValue))
            return SanitizeFileName(downloadValue);

        return GuessFileNameFromUrl(link);
    }

    private static string GuessFileNameFromUrl(string link)
    {
        string candidate = string.Empty;

        if (Uri.TryCreate(link, UriKind.Absolute, out Uri? uri))
            candidate = Path.GetFileName(Uri.UnescapeDataString(uri.LocalPath));
        else
            candidate = Path.GetFileName(link.Split('?', '#')[0]);

        return SanitizeFileName(string.IsNullOrWhiteSpace(candidate) ? "download" : candidate);
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "download";

        foreach (char invalid in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(invalid, '_');

        fileName = fileName.Trim();
        if (fileName is "." or "..")
            return "download";

        return fileName.Length <= 240 ? fileName : fileName[..240];
    }

    // ---- Favorites & chrome ----------------------------------------------

    private void ToggleFavorite()
    {
        string url = _urlEdit?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url) || string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
            return;

        if (_favorites.Contains(url))
            _favorites.Remove(url);
        else
            _favorites.Add(url);

        _favorites.Save();
        UpdateStarButton();
        RefreshFavoritesBar();
        LayoutControls(ClientSize);
    }

    private void RefreshFavoritesBar()
    {
        foreach (var (button, _) in _favoriteButtons)
            button.Dispose();
        _favoriteButtons.Clear();

        foreach (string url in _favorites.Favorites)
        {
            string favUrl = url;
            var button = CreateButtonControl(new BControlOptions { Text = FavoriteLabel(url) });
            button.Clicked += (_, _) => NavigateTo(favUrl);
            _favoriteButtons.Add((button, url));
        }
    }

    private void UpdateNavigationButtons()
    {
        if (_backButton != null)
            _backButton.Enabled = _historyIndex > 0;
        if (_forwardButton != null)
            _forwardButton.Enabled = _historyIndex < _history.Count - 1;
    }

    private void UpdateStarButton()
    {
        if (_starButton != null)
            _starButton.Text = _favorites.Contains(_urlEdit?.Text ?? string.Empty) ? "★" : "☆";
    }

    private void LayoutControls(BSize clientSize)
    {
        if (_backButton is null || _forwardButton is null || _refreshButton is null
            || _stopButton is null || _urlEdit is null || _starButton is null || _goButton is null)
            return;

        double x = Margin;
        double y = (ToolbarHeight - ControlHeight) / 2;

        _backButton.Bounds = new BRect(x, y, NavButtonWidth, ControlHeight); x += NavButtonWidth + Margin;
        _forwardButton.Bounds = new BRect(x, y, NavButtonWidth, ControlHeight); x += NavButtonWidth + Margin;
        _refreshButton.Bounds = new BRect(x, y, NavButtonWidth, ControlHeight); x += NavButtonWidth + Margin;
        _stopButton.Bounds = new BRect(x, y, StopButtonWidth, ControlHeight); x += StopButtonWidth + Margin;

        double rightControls = GoButtonWidth + StarWidth + (Margin * 3);
        double editWidth = Math.Max(40, clientSize.Width - x - rightControls);
        _urlEdit.Bounds = new BRect(x, y, editWidth, ControlHeight); x += editWidth + Margin;
        _starButton.Bounds = new BRect(x, y, StarWidth, ControlHeight); x += StarWidth + Margin;
        _goButton.Bounds = new BRect(x, y, GoButtonWidth, ControlHeight);

        // Favorites bar row.
        double fx = Margin;
        double fy = ToolbarHeight + ((FavoritesBarHeight - ControlHeight) / 2);
        foreach (var (button, _) in _favoriteButtons)
        {
            double width = EstimateFavoriteWidth(button.Text);
            if (fx + width > clientSize.Width - Margin)
            {
                button.Visible = false;
                continue;
            }
            button.Visible = true;
            button.Bounds = new BRect(fx, fy, width, ControlHeight);
            fx += width + Margin;
        }

        if (_statusBar != null)
        {
            _statusBar.Visible = true;
            _statusBar.Bounds = new BRect(
                Margin,
                Math.Max(ToolbarHeight + FavoritesBarHeight, clientSize.Height - StatusBarHeight),
                Math.Max(0, clientSize.Width - (Margin * 2)),
                StatusBarHeight);
        }
    }

    private void SetUrlText(string url)
    {
        if (_urlEdit != null && !string.Equals(_urlEdit.Text, url, StringComparison.Ordinal))
            _urlEdit.Text = url;
    }

    private void SetStatus(string status)
    {
        if (_statusBar != null && !string.Equals(_statusBar.Text, status, StringComparison.Ordinal))
            _statusBar.Text = status;
    }

    private void SetBusy(bool busy)
    {
        _isPageBusy = busy;
        if (_stopButton != null)
            _stopButton.Enabled = busy;
    }

    private BButtonControl MakeButton(string text, Action onClick)
    {
        var button = CreateButtonControl(new BControlOptions { Text = text });
        button.Clicked += (_, _) => onClick();
        return button;
    }

    private BColor ResolveClearColor()
    {
        Color background = _container.GetRootBackgroundColor();
        return !background.IsEmpty && background.A > 0
            ? new BColor(background.R, background.G, background.B, background.A)
            : BColor.White;
    }

    private static HtmlContainer CreateContentContainer(string html, string baseUrl)
    {
        HtmlContainer container = CreateHtmlContainer();
        container.BaseUrl = baseUrl;
        container.SetHtmlWithStyleSet(html, baseUrl: baseUrl);
        return container;
    }

    private static HtmlContainer CreateHtmlContainer()
    {
        return new HtmlContainer
        {
            AvoidAsyncImagesLoading = true,
            AvoidImagesLateLoading = true,
        };
    }

    private void MarkLayoutDirty()
    {
        _layoutDirty = true;
        _renderDirty = true;
    }

    private static PointF ToPoint(BPoint p) => new((float)p.X, (float)p.Y);

    private static bool IsChangedOrCurrentButton(BPointerEventArgs e, BMouseButtons button) =>
        e.ChangedButton == BMouseButtons.None
            ? (e.Buttons & button) != 0
            : (e.ChangedButton & button) != 0;

    // Accept a local filesystem path (e.g. "C:\page.html") as a convenience and turn it into a
    // file:// URI the page loader understands; otherwise pass the input through unchanged.
    private static string NormalizeInput(string input)
    {
        input = input.Trim();
        bool looksLikePath = (input.Length >= 2 && input[1] == ':') || input.StartsWith(@"\\", StringComparison.Ordinal);
        if (looksLikePath && File.Exists(input))
            return new Uri(Path.GetFullPath(input)).AbsoluteUri;
        return input;
    }

    private static double EstimateFavoriteWidth(string label) =>
        Math.Clamp(label.Length * 8 + 16, 48, 160);

    private static string FavoriteLabel(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Host))
        {
            string host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                host = host[4..];
            return host;
        }

        return url.Length > 24 ? url[..21] + "…" : url;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            BeginShutdown();
            foreach (var (button, _) in _favoriteButtons)
                button.Dispose();
            _favoriteButtons.Clear();
            _backButton?.Dispose();
            _forwardButton?.Dispose();
            _refreshButton?.Dispose();
            _stopButton?.Dispose();
            _goButton?.Dispose();
            _starButton?.Dispose();
            _urlEdit?.Dispose();
            _statusBar?.Dispose();
            DisposeRenderList();
            _container.Dispose();
        }

        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileName(ref OpenFileName openFileName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr newItemId, string? itemText);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int TrackPopupMenu(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        int reserved,
        IntPtr owner,
        IntPtr rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    private sealed class NavigationLoadResult : IDisposable
    {
        private HtmlContainer? _container;
        private InteractiveSession? _session;

        private NavigationLoadResult(
            string normalisedUrl,
            HtmlContainer? container,
            InteractiveSession? session,
            Exception? error)
        {
            NormalisedUrl = normalisedUrl;
            _container = container;
            _session = session;
            Error = error;
        }

        public string NormalisedUrl { get; }

        public Exception? Error { get; }

        public static NavigationLoadResult FromSuccess(
            string normalisedUrl,
            HtmlContainer container,
            InteractiveSession? session) =>
            new(normalisedUrl, container, session, null);

        public static NavigationLoadResult FromError(Exception error) =>
            new(string.Empty, null, null, error);

        public HtmlContainer TakeContainer()
        {
            HtmlContainer container = _container ?? throw new InvalidOperationException("Navigation result has no container.");
            _container = null;
            return container;
        }

        public InteractiveSession? TakeSession()
        {
            InteractiveSession? session = _session;
            _session = null;
            return session;
        }

        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
            _container?.Dispose();
            _container = null;
        }
    }

    private const string WelcomePage = """
<html>
<head>
    <style>
        body { font-family: Segoe UI, Arial, sans-serif; margin: 40px; background: #fafafa; color: #333; }
        h1 { color: #2c3e50; }
        p { line-height: 1.6; }
        .info { background: #ecf0f1; padding: 16px; border-radius: 4px; margin-top: 20px; }
    </style>
</head>
<body>
    <h1>Welcome to Broiler</h1>
    <p>This is the Broiler browser running on the Broiler.Graphics (Direct2D) UI &mdash; no WPF.</p>
    <div class='info'>
        <p><strong>Getting Started:</strong> type a URL in the address bar and press Enter or click Go.</p>
        <p><strong>Features:</strong></p>
        <ul>
            <li>HTML &amp; CSS rendering via Broiler.HTML.Graphics</li>
            <li>JavaScript execution with interactive animation stepping</li>
            <li>Navigation history, favorites, links, mouse-wheel and keyboard scrolling</li>
        </ul>
    </div>
</body>
</html>
""";
}
