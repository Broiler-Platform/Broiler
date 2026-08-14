using Broiler.HtmlBridge.Logging;

namespace Broiler.Cli.Tests;

/// <summary>
/// The <c>unhandledrejection</c> event: a page's own view of a promise rejected with nobody waiting
/// on it, and its chance to claim the failure before the engine reports it.
/// </summary>
/// <remarks>
/// <para>
/// Collecting these rejections came first and fed one consumer — the diagnostic log — so a page
/// could not see its own failures and every rejection was reported whether or not the page had
/// already handled it through its own error channel. These cover the event that closes both gaps.
/// </para>
/// <para>
/// In the <see cref="Collection"/> with the diagnostics tests because the engine's tracker is
/// process-wide static state: two capture tests running in parallel would drain each other's
/// rejections, and the failure would look like flakiness rather than like shared state.
/// </para>
/// </remarks>
[Collection("DiagnosticSession")]
public sealed class UnhandledRejectionEventTests
{
    /// <summary>The marker the scripts below reject with, so an assertion cannot match by accident.</summary>
    private const string Marker = "rejectionEventMarker";

    /// <summary>
    /// Runs <paramref name="script"/> through a capture and returns the serialized DOM it left
    /// behind together with everything logged as a JavaScript failure.
    /// </summary>
    private static (string Dom, string[] Failures) Capture(string script)
    {
        var messages = new List<string>();
        void Collect(RenderLogEntry entry)
        {
            if (entry.Category == LogCategory.JavaScript && entry.Level >= LogLevel.Warning)
                lock (messages) messages.Add(entry.Message);
        }

        RenderLogger.EntryLogged += Collect;
        try
        {
            var dom = CaptureService.ExecuteScriptsWithDom(
                $"<!DOCTYPE html><html><body><div id='out'></div><script>{script}</script></body></html>",
                "https://example.test/page");
            lock (messages) return (dom, [.. messages]);
        }
        finally
        {
            RenderLogger.EntryLogged -= Collect;
        }
    }

    /// <summary>Records <c>what</c> into the document, where the serialized DOM will show it.</summary>
    private const string Report =
        "function report(what) { document.getElementById('out').textContent += '[' + what + ']'; }";

    [Fact]
    public void A_Listener_Sees_The_Rejection_And_Its_Reason()
    {
        var (dom, _) = Capture(
            Report +
            "window.addEventListener('unhandledrejection', function (e) {" +
            "  report(e.type + ':' + e.reason.message);" +
            "});" +
            $"Promise.reject(new Error('{Marker}'));");

        Assert.Contains($"[unhandledrejection:{Marker}]", dom, StringComparison.Ordinal);
    }

    /// <summary>
    /// The handler-property form. It is the one a page written against
    /// <c>window.onerror</c>-era conventions reaches for, and — because <c>window</c> is the global
    /// object — the bare <c>onunhandledrejection = f</c> spelling is the same property.
    /// </summary>
    [Theory]
    [InlineData("window.onunhandledrejection = function (e) { report('on:' + e.reason.message); };")]
    [InlineData("onunhandledrejection = function (e) { report('on:' + e.reason.message); };")]
    public void The_On_Handler_Property_Is_Called(string registration)
    {
        var (dom, _) = Capture(Report + registration + $"Promise.reject(new Error('{Marker}'));");

        Assert.Contains($"[on:{Marker}]", dom, StringComparison.Ordinal);
    }

    /// <summary>
    /// The point of the event. A page with its own error channel says so with
    /// <c>preventDefault()</c>, and is then not also told it failed to handle the rejection.
    /// </summary>
    [Theory]
    [InlineData("window.addEventListener('unhandledrejection', function (e) { e.preventDefault(); });")]
    [InlineData("window.onunhandledrejection = function (e) { e.preventDefault(); };")]
    public void Cancelling_Suppresses_The_Default_Report(string registration)
    {
        var (_, failures) = Capture(registration + $"Promise.reject(new Error('{Marker}'));");

        Assert.DoesNotContain(
            failures,
            m => m.Contains("Unhandled promise rejection", StringComparison.Ordinal));
    }

    /// <summary>
    /// The other half of the same contract, and the one a regression would silently break: a page
    /// that merely observes the event has not claimed the failure, so the report still happens.
    /// </summary>
    [Fact]
    public void Observing_Without_Cancelling_Leaves_The_Report_Alone()
    {
        var (_, failures) = Capture(
            "window.addEventListener('unhandledrejection', function (e) {});" +
            $"Promise.reject(new Error('{Marker}'));");

        Assert.Contains(failures, m => m.Contains("Unhandled promise rejection", StringComparison.Ordinal));
        Assert.Contains(failures, m => m.Contains(Marker, StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>preventDefault()</c> is a no-op on a non-cancelable event, so the event has to be built
    /// cancelable for the suppression above to be possible at all. Asserting the flag directly keeps
    /// a regression there from being reported as "cancelling stopped working".
    /// </summary>
    [Fact]
    public void The_Event_Is_Cancelable_And_Does_Not_Bubble()
    {
        var (dom, _) = Capture(
            Report +
            "window.addEventListener('unhandledrejection', function (e) {" +
            "  report('cancelable=' + e.cancelable + ',bubbles=' + e.bubbles);" +
            "});" +
            $"Promise.reject(new Error('{Marker}'));");

        Assert.Contains("[cancelable=true,bubbles=false]", dom, StringComparison.Ordinal);
    }

    /// <summary>A rejection the page handles is not a failure, and fires nothing.</summary>
    [Theory]
    [InlineData("Promise.reject(new Error('m')).catch(function (e) {});")]
    [InlineData("(async function () { try { await Promise.reject(new Error('m')); } catch (e) {} })();")]
    public void A_Handled_Rejection_Fires_Nothing(string script)
    {
        var (dom, _) = Capture(
            Report + "window.addEventListener('unhandledrejection', function (e) { report('fired'); });" + script);

        Assert.DoesNotContain("[fired]", dom, StringComparison.Ordinal);
    }

    /// <summary>
    /// A reason that is not an <c>Error</c> reaches the listener as the value the page rejected
    /// with, not as a stringified description of it.
    /// </summary>
    [Fact]
    public void The_Reason_Reaches_The_Listener_As_The_Rejected_Value()
    {
        var (dom, _) = Capture(
            Report +
            "window.addEventListener('unhandledrejection', function (e) {" +
            "  report(typeof e.reason + ':' + e.reason.code);" +
            "});" +
            "Promise.reject({ code: 42 });");

        Assert.Contains("[object:42]", dom, StringComparison.Ordinal);
    }

    /// <summary>
    /// A listener that throws must not take the report down with it: the rejection it was told about
    /// is still a failure, and the listener's own failure is separate.
    /// </summary>
    [Fact]
    public void A_Throwing_Listener_Does_Not_Lose_The_Report()
    {
        var (_, failures) = Capture(
            "window.addEventListener('unhandledrejection', function (e) { throw new Error('listenerBlewUp'); });" +
            $"Promise.reject(new Error('{Marker}'));");

        Assert.Contains(failures, m => m.Contains("Unhandled promise rejection", StringComparison.Ordinal));
    }
}
