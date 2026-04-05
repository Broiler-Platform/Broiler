using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Broiler.App.Rendering;
using Broiler.HtmlBridge;
using Broiler.HTML.Core.Core.Entities;
using Broiler.HTML.WPF;

namespace Broiler.App;

/// <summary>
/// Main browser window with navigation bar and HTML rendering panel.
/// Delegates page loading, script extraction, and JavaScript execution
/// to the <see cref="RenderingPipeline"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private readonly RenderingPipeline _pipeline;
    private bool _consoleVisible;

    /// <summary>
    /// Active interactive rendering session.  When non-null, a
    /// <see cref="DispatcherTimer"/> steps through pending timer and rAF
    /// callbacks one batch at a time, re-rendering the panel after each
    /// step so that animations are displayed interactively.
    /// </summary>
    private InteractiveSession? _activeSession;
    private DispatcherTimer? _renderTimer;

    public MainWindow()
    {
        InitializeComponent();

        _pipeline = new RenderingPipeline(
            new PageLoader(new HttpClient()),
            new ScriptExtractor(),
            new ScriptEngine());

        DevConsole.CloseRequested += ToggleConsole;

        Closed += (_, _) =>
        {
            StopInteractiveRendering();
            _pipeline.Dispose();
            DevConsole.Dispose();
        };

        NavigateTo("about:blank");
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_historyIndex > 0)
        {
            _historyIndex--;
            LoadUrl(_history[_historyIndex]);
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_historyIndex < _history.Count - 1)
        {
            _historyIndex++;
            LoadUrl(_history[_historyIndex]);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_historyIndex >= 0 && _historyIndex < _history.Count)
        {
            LoadUrl(_history[_historyIndex]);
        }
    }

    private void GoButton_Click(object sender, RoutedEventArgs e) => NavigateTo(UrlTextBox.Text);

    private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateTo(UrlTextBox.Text);
        }
    }

    private void HtmlPanel_LinkClicked(object sender, RoutedEventArgs<HtmlLinkClickedEventArgs> e)
    {
        e.Handled = true;
        var link = e.Data.Link;

        // Resolve fragment-only hrefs (e.g. "#top") against the current page
        // URL so the address bar shows "acid2.html#top" instead of just "#top".
        if (link.StartsWith("#") && _historyIndex >= 0 && _historyIndex < _history.Count)
        {
            var currentUrl = _history[_historyIndex];
            var hashIndex = currentUrl.IndexOf('#');
            if (hashIndex >= 0)
                currentUrl = currentUrl.Substring(0, hashIndex);
            link = currentUrl + link;
        }

        NavigateTo(link);
        e.Handled = true;
    }

    /// <summary>
    /// Navigate to a URL, adding it to the history stack.
    /// </summary>
    public void NavigateTo(string url)
    {
        // Remove forward history when navigating to a new URL
        if (_historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }

        _history.Add(url);
        _historyIndex = _history.Count - 1;
        LoadUrl(url);
    }

    private async void LoadUrl(string url)
    {
        UrlTextBox.Text = url;
        UpdateNavigationButtons();

        // Stop any in-progress interactive rendering from a previous page.
        StopInteractiveRendering();

        if (url == "about:blank")
        {
            HtmlPanel.Text = GetWelcomePage();
            StatusText.Text = "Ready";
            return;
        }

        try
        {
            StatusText.Text = $"Loading {url}...";

            var (normalisedUrl, content) = await _pipeline.LoadPageAsync(url);
            UrlTextBox.Text = normalisedUrl;

            // Render the original HTML (post-processed to strip elements
            // that HtmlRenderer cannot handle).
            HtmlPanel.BaseUrl = normalisedUrl;
            HtmlPanel.Text = HtmlPostProcessor.Process(content.Html);

            // Start an interactive session so that timer / rAF callbacks
            // are stepped through one batch at a time, allowing animations
            // to be displayed in real-time instead of jumping to the final
            // frame.
            _activeSession = _pipeline.ExecuteScriptsInteractive(content);
            if (_activeSession != null)
            {
                // Render the post-script initial state (before any timers).
                HtmlPanel.Text = HtmlPostProcessor.Process(_activeSession.CurrentHtml());

                if (_activeSession.HasPendingWork)
                {
                    // Step through remaining callbacks at ~60 fps.
                    _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
                    {
                        Interval = TimeSpan.FromMilliseconds(16)
                    };
                    _renderTimer.Tick += OnRenderTimerTick;
                    _renderTimer.Start();
                    StatusText.Text = "Rendering…";
                }
                else
                {
                    // No pending work – clean up immediately.
                    _activeSession.Dispose();
                    _activeSession = null;
                }
            }

            // Scroll to fragment anchor if the URL contains one (e.g. "#top"
            // for Acid2).  This mirrors the CLI's RenderAtAnchor behaviour.
            if (Uri.TryCreate(normalisedUrl, UriKind.Absolute, out var uri)
                && !string.IsNullOrEmpty(uri.Fragment) && uri.Fragment.Length > 1)
            {
                var elementId = uri.Fragment[1..];
                // Force a synchronous layout pass so that element positions
                // are computed before we try to scroll.  Setting HtmlPanel.Text
                // only parses the HTML and calls InvalidateMeasure(); the
                // actual layout (MeasureOverride → PerformHtmlLayout) has not
                // yet run because we are still on the UI thread.  Without this
                // call GetElementRectangle returns zero coordinates, causing
                // ScrollToElement to silently do nothing.
                HtmlPanel.UpdateLayout();
                HtmlPanel.ScrollToElement(elementId);
            }

            if (_activeSession == null)
                StatusText.Text = "Done";
        }
        catch (Exception ex)
        {
            HtmlPanel.Text = $"<html><body><h1>Error</h1><p>{ex.Message}</p></body></html>";
            StatusText.Text = "Error loading page";
        }
    }

    /// <summary>
    /// DispatcherTimer callback that steps through one batch of pending
    /// timer / rAF callbacks and re-renders the panel.
    /// </summary>
    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        if (_activeSession == null || !_activeSession.HasPendingWork)
        {
            StopInteractiveRendering();
            StatusText.Text = "Done";
            return;
        }

        var html = _activeSession.Step();
        if (html != null)
            HtmlPanel.Text = HtmlPostProcessor.Process(html);

        // If there's no more work after this step, stop the timer.
        if (!_activeSession.HasPendingWork)
        {
            StopInteractiveRendering();
            StatusText.Text = "Done";
        }
    }

    /// <summary>
    /// Stops the interactive render loop and disposes the session.
    /// </summary>
    private void StopInteractiveRendering()
    {
        if (_renderTimer != null)
        {
            _renderTimer.Stop();
            _renderTimer.Tick -= OnRenderTimerTick;
            _renderTimer = null;
        }

        if (_activeSession != null)
        {
            _activeSession.Dispose();
            _activeSession = null;
        }
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = _historyIndex > 0;
        ForwardButton.IsEnabled = _historyIndex < _history.Count - 1;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            ToggleConsole();
            e.Handled = true;
        }
    }

    private void ToggleConsole()
    {
        _consoleVisible = !_consoleVisible;

        if (_consoleVisible)
        {
            ConsoleRow.Height = new GridLength(250);
            DevConsole.Visibility = Visibility.Visible;
            ConsoleSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            ConsoleRow.Height = new GridLength(0);
            DevConsole.Visibility = Visibility.Collapsed;
            ConsoleSplitter.Visibility = Visibility.Collapsed;
        }
    }

    private static string GetWelcomePage() => @"
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
    <p>Broiler is a lightweight WPF web browser powered by HTML-Renderer and YantraJS.</p>
    <div class='info'>
        <p><strong>Getting Started:</strong> Enter a URL in the address bar above and press Enter or click Go.</p>
        <p><strong>Features:</strong></p>
        <ul>
            <li>HTML 4.01 and CSS Level 2 rendering</li>
            <li>JavaScript execution via YantraJS</li>
            <li>Navigation history (Back / Forward / Refresh)</li>
            <li>Link navigation</li>
        </ul>
    </div>
</body>
</html>";
}
