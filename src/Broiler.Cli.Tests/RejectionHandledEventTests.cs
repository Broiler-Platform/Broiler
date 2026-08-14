namespace Broiler.Cli.Tests;

/// <summary>
/// The half of the promise-rejection contract that needs the engine to say <em>which</em> promise it
/// reported: <c>event.promise</c>, and the <c>rejectionhandled</c> event fired when a handler
/// arrives for a rejection already reported as unhandled.
/// </summary>
/// <remarks>
/// <para>
/// A rejection can be claimed after it has been reported — a page that stores a promise and attaches
/// its <c>catch</c> from a later task does exactly that — and a page that logged the failure needs
/// to know it was not a failure after all. Matching the two events up is what the promise identity
/// is for: two promises rejected with the same error are two rejections, so the reason cannot do it.
/// </para>
/// <para>
/// Compiled only when the engine carries the notification API; see the
/// <c>BroilerJsRejectionPromiseIdentity</c> probe in the test project.
/// </para>
/// </remarks>
[Collection("DiagnosticSession")]
public sealed class RejectionHandledEventTests
{
    /// <summary>Runs <paramref name="script"/> through a capture and returns the serialized DOM.</summary>
    private static string Capture(string script) =>
        CaptureService.ExecuteScriptsWithDom(
            $"<!DOCTYPE html><html><body><div id='out'></div><script>{script}</script></body></html>",
            "https://example.test/page");

    private const string Report =
        "function report(what) { document.getElementById('out').textContent += '[' + what + ']'; }";

    /// <summary>
    /// The identity itself. The event's <c>promise</c> is the very promise that rejected, not a copy
    /// or a description of it, which is what lets a page match the report against its own work.
    /// </summary>
    [Fact]
    public void The_Event_Carries_The_Promise_That_Rejected()
    {
        var dom = Capture(
            Report +
            "var mine = Promise.reject(new Error('identityMarker'));" +
            "window.addEventListener('unhandledrejection', function (e) {" +
            "  report('same=' + (e.promise === mine));" +
            "});");

        Assert.Contains("[same=true]", dom, StringComparison.Ordinal);
    }

    /// <summary>
    /// A handler attached after the rejection was reported retracts it. The <c>setTimeout</c> is the
    /// point: it puts the <c>catch</c> in a later task, after the checkpoint that reported the
    /// rejection has already drained, which is the only way this event can arise.
    /// </summary>
    [Fact]
    public void A_Late_Handler_Fires_RejectionHandled()
    {
        var dom = Capture(
            Report +
            "window.addEventListener('rejectionhandled', function (e) {" +
            "  report('handled:' + e.reason.message);" +
            "});" +
            "var late = Promise.reject(new Error('lateMarker'));" +
            "setTimeout(function () { late.catch(function (e) {}); }, 0);");

        Assert.Contains("[handled:lateMarker]", dom, StringComparison.Ordinal);
    }

    /// <summary>
    /// It names the same promise the <c>unhandledrejection</c> did, which is the whole point of
    /// pairing them: a page that keyed its log by the promise can find the entry to retract.
    /// </summary>
    [Fact]
    public void It_Names_The_Same_Promise_As_The_Report()
    {
        var dom = Capture(
            Report +
            "var seen = null;" +
            "window.addEventListener('unhandledrejection', function (e) { seen = e.promise; });" +
            "window.addEventListener('rejectionhandled', function (e) { report('paired=' + (e.promise === seen)); });" +
            "var late = Promise.reject(new Error('pairMarker'));" +
            "setTimeout(function () { late.catch(function (e) {}); }, 0);");

        Assert.Contains("[paired=true]", dom, StringComparison.Ordinal);
    }

    /// <summary>
    /// A rejection handled before it was ever reported has nothing to retract, so only the promises
    /// that actually reached the host fire this.
    /// </summary>
    [Fact]
    public void A_Promptly_Handled_Rejection_Fires_Nothing()
    {
        var dom = Capture(
            Report +
            "window.addEventListener('rejectionhandled', function (e) { report('handled'); });" +
            "Promise.reject(new Error('promptMarker')).catch(function (e) {});");

        Assert.DoesNotContain("[handled]", dom, StringComparison.Ordinal);
    }
}
