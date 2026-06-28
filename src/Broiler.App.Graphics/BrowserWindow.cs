using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
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
    private const double Margin = 6;
    private const double ControlHeight = 26;
    private const double NavButtonWidth = 34;
    private const double GoButtonWidth = 56;
    private const double StarWidth = 34;

    private const double WheelScrollStep = 60;
    private const double KeyScrollStep = 48;
    private const double AnimationIntervalMs = 16;

    private readonly string? _initialUrl;
    private readonly HtmlContainer _container = new();
    private readonly RenderingPipeline _pipeline;
    private readonly FavoritesManager _favorites = new();

    private readonly List<string> _history = [];
    private int _historyIndex = -1;

    private BButtonControl? _backButton;
    private BButtonControl? _forwardButton;
    private BButtonControl? _refreshButton;
    private BButtonControl? _goButton;
    private BButtonControl? _starButton;
    private BEditControl? _urlEdit;
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
        _container.AvoidAsyncImagesLoading = true;
        _container.AvoidImagesLateLoading = true;
        _container.LinkClicked += OnLinkClicked;

        _pipeline = new RenderingPipeline(
            new PageLoader(new HttpClient()),
            new ScriptExtractor(),
            new ScriptEngine());
    }

    // The render content area sits below the toolbar and favorites bar.
    protected override BRect GetRenderBounds(BSize clientSize)
    {
        double top = ToolbarHeight + FavoritesBarHeight;
        return new BRect(0, top, clientSize.Width, Math.Max(0, clientSize.Height - top));
    }

    protected override BFrameContext CreateFrameContext(long frameIndex) =>
        new(ResolveClearColor(), frameIndex, Options.RenderOptions);

    protected override void OnCreated() => Diagnostics.Guard(nameof(OnCreated), OnCreatedCore);

    private void OnCreatedCore()
    {
        _backButton = MakeButton("←", () => GoHistory(-1));
        _forwardButton = MakeButton("→", () => GoHistory(1));
        _refreshButton = MakeButton("↻", Reload);
        _urlEdit = CreateEditControl(new BControlOptions { Text = string.Empty });
        _urlEdit.Submitted += (_, _) => NavigateTo(_urlEdit!.Text);
        _starButton = MakeButton("☆", ToggleFavorite);
        _goButton = MakeButton("Go", () => NavigateTo(_urlEdit!.Text));

        _favorites.Load();
        RefreshFavoritesBar();
        LayoutControls(ClientSize);

        NavigateTo(_initialUrl ?? "about:blank");
        _urlEdit.Focus();
    }

    protected override void OnResized(BSize clientSize, double dpiScale)
    {
        LayoutControls(clientSize);
        MarkLayoutDirty();
    }

    protected override void OnGraphicsResourcesReleasing() => MarkLayoutDirty();

    // ---- Navigation -------------------------------------------------------

    private void NavigateTo(string url)
    {
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
        int target = _historyIndex + delta;
        if (target < 0 || target >= _history.Count)
            return;

        _historyIndex = target;
        LoadUrl(_history[target]);
    }

    private void Reload()
    {
        if (_historyIndex >= 0 && _historyIndex < _history.Count)
            LoadUrl(_history[_historyIndex]);
    }

    private void LoadUrl(string url)
    {
        SetUrlText(url);
        UpdateNavigationButtons();
        UpdateStarButton();
        StopSession();
        _scrollY = 0;
        _pendingAnchor = null;

        if (string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            _baseUrl = string.Empty;
            _container.SetHtmlWithStyleSet(WelcomePage);
            _hasContent = true;
            MarkLayoutDirty();
            Invalidate();
            return;
        }

        try
        {
            var (normalisedUrl, content) = _pipeline.LoadPageAsync(url).GetAwaiter().GetResult();
            _baseUrl = normalisedUrl;
            SetUrlText(normalisedUrl);

            _container.BaseUrl = normalisedUrl;
            _container.SetHtmlWithStyleSet(HtmlPostProcessor.Process(content.Html), baseUrl: normalisedUrl);
            _hasContent = true;

            // Interactive session lets timer / rAF callbacks be stepped one batch at a
            // time so animations play out, mirroring the WPF shell. The Graphics host
            // renders from serialized HTML (the typed-document path lays out empty here).
            _session = _pipeline.ExecuteScriptsInteractive(content);
            if (_session != null)
            {
                string initial = _session.CurrentHtml();
                if (!string.IsNullOrWhiteSpace(initial))
                    _container.SetHtmlWithStyleSet(HtmlPostProcessor.Process(initial), baseUrl: normalisedUrl);

                if (_session.HasPendingWork)
                    StartAnimationTimer(AnimationIntervalMs);
                else
                    StopSession();
            }

            if (Uri.TryCreate(normalisedUrl, UriKind.Absolute, out Uri? uri)
                && !string.IsNullOrEmpty(uri.Fragment) && uri.Fragment.Length > 1)
            {
                _pendingAnchor = uri.Fragment[1..];
            }

            MarkLayoutDirty();
            Invalidate();
        }
        catch (Exception ex)
        {
            _baseUrl = string.Empty;
            _container.SetHtmlWithStyleSet($"<html><body><h1>Error</h1><p>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p></body></html>");
            _hasContent = true;
            MarkLayoutDirty();
            Invalidate();
        }
    }

    private void OnLinkClicked(object? sender, HtmlLinkClickedEventArgs e)
    {
        e.Handled = true;
        if (_suppressNavigation)
            return;

        string link = e.Link;

        // Resolve fragment-only hrefs (e.g. "#top") against the current page URL.
        if (link.StartsWith('#') && _historyIndex >= 0 && _historyIndex < _history.Count)
        {
            string current = _history[_historyIndex];
            int hash = current.IndexOf('#');
            if (hash >= 0)
                current = current[..hash];
            link = current + link;
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
        if (!_hasContent || clientSize.IsEmpty || Renderer is null)
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

        if (_renderDirty || _renderList is null)
        {
            _container.ScrollOffset = new PointF(0, -_scrollY);
            _renderList?.Dispose();
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
        if (_hasContent)
            _container.HandleMouseDown(ToPoint(e.Position), e.LeftButton, e.RightButton);
    });

    protected override void OnPointerMove(BPointerEventArgs e) => Diagnostics.Guard(nameof(OnPointerMove), () =>
    {
        if (_hasContent)
            _container.HandleMouseMove(ToPoint(e.Position), e.LeftButton, e.RightButton);
    });

    protected override void OnPointerUp(BPointerEventArgs e) => Diagnostics.Guard(nameof(OnPointerUp), () =>
    {
        // HandleMouseUp raises LinkClicked for links, which drives navigation.
        if (_hasContent)
            _container.HandleMouseUp(ToPoint(e.Position), e.LeftButton, e.RightButton);
    });

    protected override void OnPointerLeave() => Diagnostics.Guard(nameof(OnPointerLeave), () =>
    {
        if (_hasContent)
            _container.HandleMouseLeave();
    });

    protected override void OnMouseWheel(BMouseWheelEventArgs e)
    {
        ScrollBy(-(float)(e.Delta * WheelScrollStep));
    }

    protected override void OnKeyDown(BKeyEventArgs e) => Diagnostics.Guard(nameof(OnKeyDown), () =>
    {
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
        _scrollY = value;
        _renderDirty = true;
        Invalidate();
    }

    // ---- Animation --------------------------------------------------------

    protected override void OnAnimationTick() => Diagnostics.Guard(nameof(OnAnimationTick), OnAnimationTickCore);

    private void OnAnimationTickCore()
    {
        if (_session is null || !_session.HasPendingWork)
        {
            StopSession();
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
                _container.SetHtmlWithStyleSet(HtmlPostProcessor.Process(html), baseUrl: _baseUrl);
            }
            finally
            {
                _suppressNavigation = false;
            }
            MarkLayoutDirty();
            Invalidate();
        }

        if (!_session.HasPendingWork)
            StopSession();
    }

    private void StopSession()
    {
        StopAnimationTimer();
        _session?.Dispose();
        _session = null;
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
            || _urlEdit is null || _starButton is null || _goButton is null)
            return;

        double x = Margin;
        double y = (ToolbarHeight - ControlHeight) / 2;

        _backButton.Bounds = new BRect(x, y, NavButtonWidth, ControlHeight); x += NavButtonWidth + Margin;
        _forwardButton.Bounds = new BRect(x, y, NavButtonWidth, ControlHeight); x += NavButtonWidth + Margin;
        _refreshButton.Bounds = new BRect(x, y, NavButtonWidth, ControlHeight); x += NavButtonWidth + Margin;

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
    }

    private void SetUrlText(string url)
    {
        if (_urlEdit != null && !string.Equals(_urlEdit.Text, url, StringComparison.Ordinal))
            _urlEdit.Text = url;
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

    private void MarkLayoutDirty()
    {
        _layoutDirty = true;
        _renderDirty = true;
    }

    private static PointF ToPoint(BPoint p) => new((float)p.X, (float)p.Y);

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
            StopSession();
            foreach (var (button, _) in _favoriteButtons)
                button.Dispose();
            _favoriteButtons.Clear();
            _backButton?.Dispose();
            _forwardButton?.Dispose();
            _refreshButton?.Dispose();
            _goButton?.Dispose();
            _starButton?.Dispose();
            _urlEdit?.Dispose();
            _renderList?.Dispose();
            _pipeline.Dispose();
            _container.Dispose();
        }

        base.Dispose(disposing);
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
