using System.Drawing;
using Broiler.App;
using Broiler.App.Rendering;
using Broiler.Graphics;
using Broiler.HTML.Core.Entities;
using Broiler.HTML.Graphics;
using Broiler.HtmlBridge;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window.Standard;
using HtmlContainer = Broiler.HTML.Image.HtmlContainer;

namespace Broiler.Browser;

internal sealed class BrowserApp : IDisposable
{
    private const double AnimationIntervalMs = 16;

    private readonly BrowserUiHost _host;
    private readonly Func<IBroilerRenderer?> _getRenderer;
    private readonly Action<bool> _setAnimationActive;
    private readonly UiSession _session;
    private readonly FavoritesManager _favorites = new();
    private readonly List<string> _history = [];
    private readonly StandardButton _backButton;
    private readonly StandardButton _forwardButton;
    private readonly StandardButton _refreshButton;
    private readonly StandardButton _stopButton;
    private readonly StandardButton _goButton;
    private readonly StandardButton _starButton;
    private readonly StandardEdit _address;
    private readonly StandardLabel _status;
    private readonly BrowserViewport _viewport;
    private readonly BrowserContent _content;
    private int _historyIndex = -1;
    private bool _isPageBusy;
    private bool _isShuttingDown;
    private long _navigationGeneration;
    private CancellationTokenSource? _navigationCancellation;

    public BrowserApp(
        BrowserUiHost host,
        Func<IBroilerRenderer?> getRenderer,
        string? initialUrl,
        Action<bool> setAnimationActive)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _getRenderer = getRenderer ?? throw new ArgumentNullException(nameof(getRenderer));
        _setAnimationActive = setAnimationActive ?? throw new ArgumentNullException(nameof(setAnimationActive));
        _session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(_host);

        _backButton = CreateChromeButton("<", "Back");
        _forwardButton = CreateChromeButton(">", "Forward");
        _refreshButton = CreateChromeButton("Reload", "Reload");
        _stopButton = CreateChromeButton("Stop", "Stop");
        _goButton = CreateChromeButton("Go", "Go");
        _starButton = CreateChromeButton("*", "Favorite");
        _address = new StandardEdit
        {
            PreferredSize = new BSize(420, 28),
            PlaceholderText = "about:blank or https://example.com",
            Font = new BFontStyle("Segoe UI", 14),
            Background = BrowserPalette.Surface,
            BorderColor = BrowserPalette.Border,
            FocusRing = BrowserPalette.Accent,
            PaddingX = 10,
            PaddingY = 5,
        };
        _status = new StandardLabel
        {
            Text = "Ready",
            Font = new BFontStyle("Segoe UI", 13),
            Foreground = BrowserPalette.Muted,
            Trimming = UiTextTrimming.CharacterEllipsis,
        };
        _viewport = new BrowserViewport(_getRenderer);
        _content = new BrowserContent(
            _backButton,
            _forwardButton,
            _refreshButton,
            _stopButton,
            _address,
            _starButton,
            _goButton,
            _viewport,
            _status);

        var root = new StandardWindow
        {
            Title = "Broiler Browser",
            Background = BrowserPalette.Canvas,
            BorderColor = BrowserPalette.Border,
            ActiveBorderColor = BrowserPalette.Accent,
            BorderThickness = 1,
        };
        root.AddChild(_content);
        _session.AddRoot(root);

        _backButton.Clicked += (_, _) => GoHistory(-1);
        _forwardButton.Clicked += (_, _) => GoHistory(1);
        _refreshButton.Clicked += (_, _) => Reload();
        _stopButton.Clicked += (_, _) => StopLoading();
        _goButton.Clicked += (_, _) => NavigateTo(_address.Text);
        _starButton.Clicked += (_, _) => ToggleFavorite();
        _address.Submitted += (_, _) => NavigateTo(_address.Text);
        _viewport.LinkActivated += OnViewportLinkActivated;

        _favorites.Load();
        RefreshFavoritesBar();
        UpdateNavigationButtons();
        SetBusy(false);
        _session.SetFocus(_address);
        NavigateTo(initialUrl ?? "about:blank");
    }

    public UiSession Session => _session;

    public bool HasPendingWork => _viewport.HasPendingWork;

    public bool IsBusy => _isPageBusy;

    public string Status => _status.Text;

    public BRenderList RenderFrame() => _session.RenderFrame();

    public void Dispatch(UiInputEvent input)
    {
        if (HandleGlobalShortcut(input))
        {
            _host.RequestInvalidate();
            return;
        }

        if (_session.DispatchInput(input))
            _host.RequestInvalidate();
    }

    public void Invalidate() => _host.RequestInvalidate();

    public void ReleaseGraphicsResources()
    {
        _viewport.ReleaseGraphicsResources();
        _host.RequestInvalidate();
    }

    public BColor ResolveClearColor() => BrowserPalette.Canvas;

    public void StepAnimation()
    {
        if (_isShuttingDown)
            return;

        if (!_viewport.HasPendingWork)
        {
            SetBusy(false);
            _setAnimationActive(false);
            return;
        }

        if (_viewport.StepAnimation())
            _host.RequestInvalidate();

        if (!_viewport.HasPendingWork)
        {
            SetBusy(false);
            SetStatus("Done");
            _setAnimationActive(false);
        }
    }

    public void Dispose()
    {
        BeginShutdown();
        _viewport.LinkActivated -= OnViewportLinkActivated;
        _session.Dispose();
    }

    private static StandardButton CreateChromeButton(string text, string semanticName) =>
        new()
        {
            Text = text,
            PreferredSize = new BSize(semanticName.Length <= 7 ? 38 : 64, 28),
            Font = new BFontStyle("Segoe UI", 13, BFontWeight.SemiBold),
            Background = BrowserPalette.Surface,
            BorderColor = BrowserPalette.Border,
            Foreground = BrowserPalette.Text,
            HoverBackground = BrowserPalette.AccentSoft,
            PressedBackground = BrowserPalette.Accent,
            CornerRadius = 5,
            PaddingX = 10,
            PaddingY = 5,
        };

    private bool HandleGlobalShortcut(UiInputEvent input)
    {
        if (input.Kind != UiInputEventKind.KeyboardKey ||
            input.KeyTransition != KeyboardKeyTransition.Down)
        {
            return false;
        }

        if (IsKey(input, BVirtualKey.F5, "F5"))
        {
            Reload();
            return true;
        }

        if (input.KeyModifiers.HasFlag(KeyboardModifierState.Alt))
        {
            if (IsKey(input, BVirtualKey.Left, "Left"))
            {
                GoHistory(-1);
                return true;
            }

            if (IsKey(input, BVirtualKey.Right, "Right"))
            {
                GoHistory(1);
                return true;
            }
        }

        return false;
    }

    private void NavigateTo(string url)
    {
        if (_isShuttingDown || string.IsNullOrWhiteSpace(url))
            return;

        url = NormalizeInput(url);
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
        _viewport.StopSession();
        _navigationGeneration++;
        _setAnimationActive(false);
        SetBusy(false);
        SetStatus("Stopped");
        _host.RequestInvalidate();
    }

    private void LoadUrl(string url)
    {
        if (_isShuttingDown)
            return;

        long navigationGeneration = BeginNavigation();
        SetUrlText(url);
        UpdateNavigationButtons();
        UpdateStarButton();

        if (string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            _viewport.ReplacePage(BrowserViewport.CreateContentContainer(WelcomePage, string.Empty), null, string.Empty);
            SetBusy(false);
            SetStatus("Ready");
            _host.RequestInvalidate();
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
        _viewport.StopSession();
        _setAnimationActive(false);
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

        if (!_host.Post(() => CompleteBackgroundLoad(navigationGeneration, cancellation, result)))
        {
            result?.Dispose();
            cancellation.Dispose();
        }
    }

    private static async Task<NavigationLoadResult> LoadUrlOnWorkerAsync(string url, CancellationToken cancellationToken)
    {
        using var pipeline = new RenderingPipeline(
            new PageLoader(new HttpClient()),
            new ScriptEngine());

        var (normalisedUrl, content) = await pipeline.LoadPageAsync(url, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        string html = HtmlPostProcessor.ProcessForBrowsing(content.Html);
        InteractiveSession? session = null;
        try
        {
            session = pipeline.ExecuteScriptsInteractive(content);
            cancellationToken.ThrowIfCancellationRequested();

            if (session is not null)
            {
                string initial = session.CurrentHtml();
                if (!string.IsNullOrWhiteSpace(initial))
                    html = HtmlPostProcessor.ProcessForBrowsing(initial);

                if (!session.HasPendingWork)
                {
                    session.Dispose();
                    session = null;
                }
            }

            HtmlContainer container = BrowserViewport.CreateContentContainer(html, normalisedUrl);
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

            if (_isShuttingDown ||
                cancellation.IsCancellationRequested ||
                navigationGeneration != _navigationGeneration)
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
        SetUrlText(result.NormalisedUrl);
        _viewport.ReplacePage(result.TakeContainer(), result.TakeSession(), result.NormalisedUrl);

        if (_viewport.HasPendingWork)
        {
            SetBusy(true);
            SetStatus("Rendering...");
            _setAnimationActive(true);
        }
        else
        {
            SetBusy(false);
            SetStatus("Done");
        }

        _host.RequestInvalidate();
    }

    private void ShowLoadingPage(string url)
    {
        SetBusy(true);
        SetStatus("Loading " + url + "...");
        _viewport.ReplacePage(BrowserViewport.CreateContentContainer($"""
<html>
<body style='font-family: Segoe UI, Arial, sans-serif; margin: 40px; color: #333;'>
    <p>Loading {System.Net.WebUtility.HtmlEncode(url)}...</p>
</body>
</html>
""", string.Empty), null, string.Empty);
        _host.RequestInvalidate();
    }

    private void ShowErrorPage(Exception ex)
    {
        SetBusy(false);
        SetStatus("Error loading page");
        _viewport.ReplacePage(BrowserViewport.CreateContentContainer(
            "<html><body><h1>Error</h1><p>" + System.Net.WebUtility.HtmlEncode(ex.Message) + "</p></body></html>",
            string.Empty),
            null,
            string.Empty);
        _host.RequestInvalidate();
    }

    private void OnViewportLinkActivated(object? sender, BrowserLinkEventArgs e)
    {
        if (_isShuttingDown)
            return;

        NavigateTo(ResolveLinkUrl(e.Link));
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

        string baseUrl = !string.IsNullOrWhiteSpace(_viewport.BaseUrl)
            ? _viewport.BaseUrl
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

    private void ToggleFavorite()
    {
        string url = _address.Text;
        if (string.IsNullOrWhiteSpace(url) || string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
            return;

        if (_favorites.Contains(url))
            _favorites.Remove(url);
        else
            _favorites.Add(url);

        _favorites.Save();
        UpdateStarButton();
        RefreshFavoritesBar();
        _host.RequestInvalidate();
    }

    private void RefreshFavoritesBar()
    {
        var buttons = new List<StandardButton>();
        foreach (string url in _favorites.Favorites)
        {
            string favUrl = url;
            StandardButton button = CreateChromeButton(FavoriteLabel(url), "Favorite");
            button.PreferredSize = new BSize(EstimateFavoriteWidth(button.Text), 28);
            button.Clicked += (_, _) => NavigateTo(favUrl);
            buttons.Add(button);
        }

        _content.ReplaceFavorites(buttons);
    }

    private void UpdateNavigationButtons()
    {
        _backButton.IsEnabled = _historyIndex > 0;
        _forwardButton.IsEnabled = _historyIndex < _history.Count - 1;
    }

    private void UpdateStarButton() =>
        _starButton.Text = _favorites.Contains(_address.Text) ? "Saved" : "*";

    private void SetUrlText(string url)
    {
        if (!string.Equals(_address.Text, url, StringComparison.Ordinal))
            _address.Text = url;
        UpdateStarButton();
    }

    private void SetStatus(string status)
    {
        if (!string.Equals(_status.Text, status, StringComparison.Ordinal))
            _status.Text = status;
    }

    private void SetBusy(bool busy)
    {
        _isPageBusy = busy;
        _stopButton.IsEnabled = busy;
    }

    private void CancelPendingNavigation()
    {
        CancellationTokenSource? cancellation = _navigationCancellation;
        _navigationCancellation = null;
        cancellation?.Cancel();
    }

    private void BeginShutdown()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        SetBusy(false);
        CancelPendingNavigation();
        _viewport.StopSession();
        _setAnimationActive(false);
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static string NormalizeInput(string input)
    {
        input = input.Trim();
        if (File.Exists(input))
            return new Uri(Path.GetFullPath(input)).AbsoluteUri;
        return input;
    }

    private static double EstimateFavoriteWidth(string label) =>
        Math.Clamp(label.Length * 8 + 18, 52, 170);

    private static string FavoriteLabel(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Host))
        {
            string host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                host = host[4..];
            return host;
        }

        return url.Length > 24 ? url[..21] + "..." : url;
    }

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

    private sealed class BrowserContent : UiElement
    {
        private const double ToolbarHeight = 42;
        private const double FavoritesBarHeight = 30;
        private const double StatusBarHeight = 24;
        private const double Margin = 8;
        private const double ControlHeight = 28;
        private const double NavButtonWidth = 38;
        private const double StopButtonWidth = 58;
        private const double GoButtonWidth = 54;
        private const double StarWidth = 62;
        private const double MinWidth = 720;
        private const double MinHeight = 480;

        private readonly StandardButton _backButton;
        private readonly StandardButton _forwardButton;
        private readonly StandardButton _refreshButton;
        private readonly StandardButton _stopButton;
        private readonly StandardEdit _address;
        private readonly StandardButton _starButton;
        private readonly StandardButton _goButton;
        private readonly BrowserViewport _viewport;
        private readonly StandardLabel _status;
        private readonly List<StandardButton> _favorites = [];

        public BrowserContent(
            StandardButton backButton,
            StandardButton forwardButton,
            StandardButton refreshButton,
            StandardButton stopButton,
            StandardEdit address,
            StandardButton starButton,
            StandardButton goButton,
            BrowserViewport viewport,
            StandardLabel status)
        {
            _backButton = backButton;
            _forwardButton = forwardButton;
            _refreshButton = refreshButton;
            _stopButton = stopButton;
            _address = address;
            _starButton = starButton;
            _goButton = goButton;
            _viewport = viewport;
            _status = status;

            AddChild(_backButton);
            AddChild(_forwardButton);
            AddChild(_refreshButton);
            AddChild(_stopButton);
            AddChild(_address);
            AddChild(_starButton);
            AddChild(_goButton);
            AddChild(_viewport);
            AddChild(_status);
        }

        public void ReplaceFavorites(IEnumerable<StandardButton> buttons)
        {
            foreach (StandardButton button in _favorites.ToArray())
            {
                RemoveChild(button);
                button.Dispose();
            }

            _favorites.Clear();
            foreach (StandardButton button in buttons)
            {
                _favorites.Add(button);
                AddChild(button);
            }

            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? MinWidth : Math.Max(MinWidth, availableSize.Width);
            double height = double.IsInfinity(availableSize.Height) ? MinHeight : Math.Max(MinHeight, availableSize.Height);
            double addressWidth = Math.Max(90, width - 4 * NavButtonWidth - StopButtonWidth - StarWidth - GoButtonWidth - 9 * Margin);
            BSize controlSize = new(double.PositiveInfinity, ControlHeight);

            _backButton.Measure(new BSize(NavButtonWidth, ControlHeight));
            _forwardButton.Measure(new BSize(NavButtonWidth, ControlHeight));
            _refreshButton.Measure(new BSize(NavButtonWidth + 24, ControlHeight));
            _stopButton.Measure(new BSize(StopButtonWidth, ControlHeight));
            _address.Measure(new BSize(addressWidth, ControlHeight));
            _starButton.Measure(new BSize(StarWidth, ControlHeight));
            _goButton.Measure(new BSize(GoButtonWidth, ControlHeight));
            foreach (StandardButton button in _favorites)
                button.Measure(controlSize);

            double viewportHeight = Math.Max(120, height - ToolbarHeight - FavoritesBarHeight - StatusBarHeight);
            _viewport.Measure(new BSize(width, viewportHeight));
            _status.Measure(new BSize(Math.Max(0, width - 2 * Margin), StatusBarHeight));
            return new BSize(width, height);
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            double x = finalRect.Left + Margin;
            double y = finalRect.Top + (ToolbarHeight - ControlHeight) / 2;

            _backButton.Arrange(new BRect(x, y, NavButtonWidth, ControlHeight));
            x += NavButtonWidth + Margin;
            _forwardButton.Arrange(new BRect(x, y, NavButtonWidth, ControlHeight));
            x += NavButtonWidth + Margin;
            _refreshButton.Arrange(new BRect(x, y, NavButtonWidth + 24, ControlHeight));
            x += NavButtonWidth + 24 + Margin;
            _stopButton.Arrange(new BRect(x, y, StopButtonWidth, ControlHeight));
            x += StopButtonWidth + Margin;

            double rightControls = StarWidth + GoButtonWidth + 2 * Margin;
            double addressWidth = Math.Max(90, finalRect.Right - Margin - x - rightControls);
            _address.Arrange(new BRect(x, y, addressWidth, ControlHeight));
            x += addressWidth + Margin;
            _starButton.Arrange(new BRect(x, y, StarWidth, ControlHeight));
            x += StarWidth + Margin;
            _goButton.Arrange(new BRect(x, y, GoButtonWidth, ControlHeight));

            double favoriteX = finalRect.Left + Margin;
            double favoriteY = finalRect.Top + ToolbarHeight + (FavoritesBarHeight - ControlHeight) / 2;
            foreach (StandardButton button in _favorites)
            {
                double width = Math.Min(button.DesiredSize.Width, Math.Max(0, finalRect.Right - Margin - favoriteX));
                if (width < 24)
                {
                    button.Visibility = UiVisibility.Collapsed;
                    continue;
                }

                button.Visibility = UiVisibility.Visible;
                button.Arrange(new BRect(favoriteX, favoriteY, width, ControlHeight));
                favoriteX += width + Margin;
            }

            double contentTop = finalRect.Top + ToolbarHeight + FavoritesBarHeight;
            double statusTop = Math.Max(contentTop, finalRect.Bottom - StatusBarHeight);
            _viewport.Arrange(new BRect(finalRect.Left, contentTop, finalRect.Width, Math.Max(0, statusTop - contentTop)));
            _status.Arrange(new BRect(finalRect.Left + Margin, statusTop, Math.Max(0, finalRect.Width - 2 * Margin), StatusBarHeight));
        }

        protected override void RenderCore(UiRenderContext context)
        {
            context.RenderList.FillRect(Bounds, BrowserPalette.Canvas);
            context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top, Bounds.Width, ToolbarHeight), BrowserPalette.Toolbar);
            context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top + ToolbarHeight, Bounds.Width, FavoritesBarHeight), BrowserPalette.Canvas);
            context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top + ToolbarHeight - 1, Bounds.Width, 1), BrowserPalette.ToolbarRule);
            context.RenderList.FillRect(new BRect(Bounds.Left, Bounds.Top + ToolbarHeight + FavoritesBarHeight - 1, Bounds.Width, 1), BrowserPalette.ToolbarRule);
            context.RenderList.FillRect(new BRect(Bounds.Left, Math.Max(Bounds.Top, Bounds.Bottom - StatusBarHeight), Bounds.Width, StatusBarHeight), BrowserPalette.Status);
            context.RenderList.FillRect(new BRect(Bounds.Left, Math.Max(Bounds.Top, Bounds.Bottom - StatusBarHeight), Bounds.Width, 1), BrowserPalette.ToolbarRule);
            base.RenderCore(context);
        }
    }

    private sealed class BrowserViewport : UiElement
    {
        private const double WheelScrollStep = 60;
        private const double KeyScrollStep = 48;

        private readonly Func<IBroilerRenderer?> _getRenderer;
        private HtmlContainer _container = CreateContentContainer(WelcomePage, string.Empty);
        private HtmlGraphicsRenderList? _renderList;
        private InteractiveSession? _interactiveSession;
        private bool _layoutDirty = true;
        private bool _renderDirty = true;
        private bool _suppressNavigation;
        private float _contentHeight;
        private float _scrollY;
        private BSize _lastLayoutSize;

        public BrowserViewport(Func<IBroilerRenderer?> getRenderer)
        {
            _getRenderer = getRenderer ?? throw new ArgumentNullException(nameof(getRenderer));
            _container.LinkClicked += OnLinkClicked;
        }

        public event EventHandler<BrowserLinkEventArgs>? LinkActivated;

        public string BaseUrl { get; private set; } = string.Empty;

        public bool HasPendingWork => _interactiveSession?.HasPendingWork == true;

        public void ReplacePage(HtmlContainer container, InteractiveSession? interactiveSession, string baseUrl)
        {
            ArgumentNullException.ThrowIfNull(container);
            StopSession();
            DisposeRenderList();
            _container.LinkClicked -= OnLinkClicked;
            _container.Dispose();

            _container = container;
            _container.LinkClicked += OnLinkClicked;
            _interactiveSession = interactiveSession;
            BaseUrl = baseUrl ?? string.Empty;
            _scrollY = 0;
            MarkLayoutDirty();
        }

        public bool StepAnimation()
        {
            if (_interactiveSession is null || !_interactiveSession.HasPendingWork)
                return false;

            string? html = _interactiveSession.Step();
            if (!string.IsNullOrWhiteSpace(html))
            {
                _suppressNavigation = true;
                try
                {
                    _container.SetHtmlWithStyleSet(HtmlPostProcessor.ProcessForBrowsing(html), baseUrl: BaseUrl);
                }
                finally
                {
                    _suppressNavigation = false;
                }

                MarkLayoutDirty();
            }

            if (!_interactiveSession.HasPendingWork)
                StopSession();

            return html is not null;
        }

        public void StopSession()
        {
            _interactiveSession?.Dispose();
            _interactiveSession = null;
        }

        public void ReleaseGraphicsResources()
        {
            DisposeRenderList();
            _layoutDirty = true;
            _renderDirty = true;
        }

        public static HtmlContainer CreateContentContainer(string html, string baseUrl)
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

        protected override BSize MeasureCore(BSize availableSize) =>
            new(
                double.IsInfinity(availableSize.Width) ? 640 : Math.Max(0, availableSize.Width),
                double.IsInfinity(availableSize.Height) ? 360 : Math.Max(0, availableSize.Height));

        protected override void RenderCore(UiRenderContext context)
        {
            context.RenderList.FillRect(Bounds, ResolveClearColor());
            if (Bounds.IsEmpty)
                return;

            IBroilerRenderer? renderer = _getRenderer();
            if (renderer is null)
            {
                context.RenderList.DrawText(
                    new BTextRun("Renderer unavailable", new BFontStyle("Segoe UI", 14), BrowserPalette.Muted),
                    new BPoint(Bounds.Left + 24, Bounds.Top + 24));
                return;
            }

            BRenderList? htmlList = BuildHtmlRenderList(renderer);
            if (htmlList is null)
                return;

            context.RenderList.PushClip(Bounds);
            context.RenderList.PushTransform(BMatrix3x2.Translation(Bounds.Left, Bounds.Top));
            ReplayCommands(context.RenderList, htmlList.Commands);
            context.RenderList.PopTransform();
            context.RenderList.PopClip();
        }

        protected override bool OnInput(UiInputEvent input)
        {
            switch (input.Kind)
            {
                case UiInputEventKind.PointerButton:
                    return HandlePointerButton(input);
                case UiInputEventKind.PointerMove:
                    return HandlePointerMove(input);
                case UiInputEventKind.PointerWheel:
                    return HandleWheel(input);
                case UiInputEventKind.KeyboardKey:
                    return HandleKeyboard(input);
                default:
                    return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopSession();
                DisposeRenderList();
                _container.LinkClicked -= OnLinkClicked;
                _container.Dispose();
            }

            base.Dispose(disposing);
        }

        private BRenderList? BuildHtmlRenderList(IBroilerRenderer renderer)
        {
            if (Bounds.IsEmpty)
                return null;

            float viewportWidth = (float)Math.Max(0, Bounds.Width);
            float viewportHeight = (float)Math.Max(0, Bounds.Height);
            BSize viewportSize = new(viewportWidth, viewportHeight);
            if (_layoutDirty || viewportSize != _lastLayoutSize)
            {
                _container.Location = PointF.Empty;
                _container.MaxSize = new SizeF(viewportWidth, viewportHeight);
                _container.PerformLayout(new RectangleF(0, 0, viewportWidth, viewportHeight));
                _contentHeight = _container.ActualSize.Height;
                _layoutDirty = false;
                _renderDirty = true;
                _lastLayoutSize = viewportSize;
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

        private bool HandlePointerButton(UiInputEvent input)
        {
            PointF point = ToLocalPoint(input.Position);
            bool left = input.MouseButton == MouseButton.Left;
            bool right = input.MouseButton == MouseButton.Right;

            if (input.MouseButtonTransition == MouseButtonTransition.Down)
            {
                Session?.SetFocus(this);
                _container.HandleMouseDown(point, left, right);
                InvalidateRenderedContent();
                return true;
            }

            if (input.MouseButtonTransition == MouseButtonTransition.Up)
            {
                _container.HandleMouseUp(point, left, right);
                InvalidateRenderedContent();
                return true;
            }

            return false;
        }

        private bool HandlePointerMove(UiInputEvent input)
        {
            _container.HandleMouseMove(ToLocalPoint(input.Position), false, false);
            InvalidateRenderedContent();
            return true;
        }

        private bool HandleWheel(UiInputEvent input)
        {
            if (input.WheelAxis != MouseWheelAxis.Vertical)
                return false;

            ScrollBy(-(float)(input.WheelDeltaNotches * WheelScrollStep));
            return true;
        }

        private bool HandleKeyboard(UiInputEvent input)
        {
            if (input.KeyTransition != KeyboardKeyTransition.Down)
                return false;

            bool control = input.KeyModifiers.HasFlag(KeyboardModifierState.Control);
            _container.HandleKeyDown(
                control,
                IsKey(input, BVirtualKey.A, "A"),
                IsKey(input, BVirtualKey.C, "C"));

            if (IsKey(input, BVirtualKey.Down, "Down"))
                ScrollBy((float)KeyScrollStep);
            else if (IsKey(input, BVirtualKey.Up, "Up"))
                ScrollBy(-(float)KeyScrollStep);
            else if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
                ScrollBy(Math.Max(1, (float)Bounds.Height - 40));
            else if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
                ScrollBy(-Math.Max(1, (float)Bounds.Height - 40));
            else if (IsKey(input, BVirtualKey.Home, "Home"))
                SetScroll(0);
            else if (IsKey(input, BVirtualKey.End, "End"))
                SetScroll(float.MaxValue);
            else
                InvalidateRenderedContent();

            return true;
        }

        private void ScrollBy(float delta) => SetScroll(_scrollY + delta);

        private void SetScroll(float value)
        {
            _scrollY = value;
            _renderDirty = true;
            Invalidate(UiInvalidationKind.Render);
        }

        private void ClampScroll(float viewportHeight)
        {
            float maxScroll = Math.Max(0, _contentHeight - viewportHeight);
            float clamped = Math.Clamp(_scrollY, 0, maxScroll);
            if (Math.Abs(clamped - _scrollY) > 0.01f)
            {
                _scrollY = clamped;
                _renderDirty = true;
            }
        }

        private void InvalidateRenderedContent()
        {
            _renderDirty = true;
            Invalidate(UiInvalidationKind.Render);
        }

        private void MarkLayoutDirty()
        {
            _layoutDirty = true;
            _renderDirty = true;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }

        private BColor ResolveClearColor()
        {
            BColor background = _container.GetRootBackgroundColor();
            return !background.IsEmpty && background.A > 0
                ? new BColor(background.R, background.G, background.B, background.A)
                : BColor.White;
        }

        private void OnLinkClicked(object? sender, HtmlLinkClickedEventArgs e)
        {
            e.Handled = true;
            if (_suppressNavigation)
                return;

            LinkActivated?.Invoke(this, new BrowserLinkEventArgs(e.Link, e.Attributes));
        }

        private void DisposeRenderList()
        {
            HtmlGraphicsRenderList? renderList = _renderList;
            _renderList = null;
            _renderDirty = true;
            renderList?.Dispose();
        }

        private PointF ToLocalPoint(BPoint point) =>
            new((float)(point.X - Bounds.Left), (float)(point.Y - Bounds.Top));

        private static void ReplayCommands(BRenderList target, IReadOnlyList<BRenderCommand> commands)
        {
            foreach (BRenderCommand command in commands)
            {
                switch (command)
                {
                    case BRenderCommand.FillRect fill:
                        target.FillRect(fill.Rect, fill.Color);
                        break;
                    case BRenderCommand.StrokeRect stroke:
                        target.StrokeRect(stroke.Rect, stroke.Color, stroke.Thickness);
                        break;
                    case BRenderCommand.FillRoundedRect fillRounded:
                        target.FillRoundedRect(fillRounded.Rect, fillRounded.Color, fillRounded.RadiusX, fillRounded.RadiusY);
                        break;
                    case BRenderCommand.StrokeRoundedRect strokeRounded:
                        target.StrokeRoundedRect(strokeRounded.Rect, strokeRounded.Color, strokeRounded.RadiusX, strokeRounded.RadiusY, strokeRounded.Thickness);
                        break;
                    case BRenderCommand.DrawText text:
                        target.DrawText(text.Text, text.Origin);
                        break;
                    case BRenderCommand.DrawImage image:
                        target.DrawImage(image.Image, image.Source, image.Destination, image.Opacity);
                        break;
                    case BRenderCommand.PushClip clip:
                        target.PushClip(clip.Rect);
                        break;
                    case BRenderCommand.PopClip:
                        target.PopClip();
                        break;
                    case BRenderCommand.PushTransform transform:
                        target.PushTransform(transform.Transform);
                        break;
                    case BRenderCommand.PopTransform:
                        target.PopTransform();
                        break;
                }
            }
        }
    }

    private sealed class BrowserLinkEventArgs : EventArgs
    {
        public BrowserLinkEventArgs(string link, IReadOnlyDictionary<string, string> attributes)
        {
            Link = link;
            Attributes = attributes;
        }

        public string Link { get; }

        public IReadOnlyDictionary<string, string> Attributes { get; }
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
    <p>This is the Broiler browser running with shared Broiler.UI controls.</p>
    <div class='info'>
        <p><strong>Getting Started:</strong> type a URL in the address bar and press Enter or click Go.</p>
        <p><strong>Features:</strong></p>
        <ul>
            <li>Shared Win32/Linux browser toolbar and status bar</li>
            <li>HTML &amp; CSS rendering via Broiler.HTML.Graphics</li>
            <li>JavaScript execution with interactive animation stepping</li>
            <li>Navigation history, favorites, links, mouse-wheel and keyboard scrolling</li>
        </ul>
    </div>
</body>
</html>
""";
}
