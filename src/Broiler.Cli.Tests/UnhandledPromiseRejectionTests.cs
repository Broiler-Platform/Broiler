using Broiler.HtmlBridge.Logging;

namespace Broiler.Cli.Tests;

/// <summary>
/// Covers the failures a capture used to lose entirely: a promise rejected with nothing waiting on
/// it. They reach no <c>catch</c> in the capture path — they propagate through the promise
/// machinery instead — so before the engine tracked them a page could fail throughout and the
/// diagnostics bundle would report zero failures.
/// </summary>
/// <remarks>
/// <para>
/// Compiled only when the <c>Broiler.JS</c> patch adding <c>JSPromiseRejectionTracker</c> is
/// applied; see the <c>BroilerJsRejectionTracking</c> probe in the test project.
/// </para>
/// <para>
/// The half that matters most here is <see cref="Handled_Rejections_Are_Not_Reported"/>. Reporting
/// an unhandled rejection is easy; not reporting a handled one is the whole difficulty, because a
/// handler is routinely attached after the promise has already settled.
/// </para>
/// </remarks>
[Collection("DiagnosticSession")]
public sealed class UnhandledPromiseRejectionTests
{
    /// <summary>Runs <paramref name="script"/> through a capture and returns what was logged.</summary>
    private static string[] CaptureFailures(string script)
    {
        var messages = new List<string>();
        void Collect(RenderLogEntry entry)
        {
            if (entry.Category == LogCategory.JavaScript && entry.Level >= LogLevel.Warning)
                lock (messages) messages.Add(entry.Message);
        }

        var directory = Path.Combine(Path.GetTempPath(), "broiler-rej-" + Guid.NewGuid().ToString("N"));
        RenderLogger.EntryLogged += Collect;
        try
        {
            // Through a session, because the engine only tracks while one is running.
            using (DiagnosticSession.Start(Program.ResolveDiagnosticOptions(directory, null)))
            {
                CaptureService.ExecuteScriptsWithDom(
                    $"<!DOCTYPE html><html><body><script>{script}</script></body></html>",
                    "https://example.test/page");
            }
        }
        finally
        {
            RenderLogger.EntryLogged -= Collect;
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        lock (messages) return [.. messages];
    }

    [Theory]
    // The three ways a page produces one, which are three different code paths in the engine: a
    // throw inside a reaction rejects the derived promise, while the other two mint a promise that
    // is already rejected without ever passing through Reject.
    [InlineData("Promise.resolve().then(function () { throw new Error('rejectionMarker'); });")]
    [InlineData("Promise.reject(new Error('rejectionMarker'));")]
    [InlineData("(async function () { throw new Error('rejectionMarker'); })();")]
    public void An_Unclaimed_Rejection_Is_Reported(string script)
    {
        var failures = CaptureFailures(script);

        Assert.Contains(failures, m => m.Contains("Unhandled promise rejection", StringComparison.Ordinal));
        Assert.Contains(failures, m => m.Contains("rejectionMarker", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Promise.reject(new Error('handledMarker')).catch(function (e) {});")]
    [InlineData("Promise.resolve().then(function () { throw new Error('handledMarker'); }).catch(function (e) {});")]
    [InlineData("Promise.reject(new Error('handledMarker')).then(null, function (e) {});")]
    [InlineData("(async function () { try { await Promise.reject(new Error('handledMarker')); } catch (e) {} })();")]
    [InlineData("Promise.all([Promise.reject(new Error('handledMarker'))]).catch(function (e) {});")]
    // The case that forces the report to be deferred rather than raised at the rejection: the
    // promise is already rejected when the handler is attached, a microtask later.
    [InlineData("var p = Promise.reject(new Error('handledMarker')); Promise.resolve().then(function () { p.catch(function (e) {}); });")]
    public void Handled_Rejections_Are_Not_Reported(string script)
    {
        Assert.DoesNotContain(
            CaptureFailures(script),
            m => m.Contains("Unhandled promise rejection", StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void A_Rejection_Is_Reported_Once_Per_Promise()
    {
        var failures = CaptureFailures(
            "Promise.reject(new Error('firstMarker')); Promise.reject(new Error('secondMarker'));");

        Assert.Equal(
            2,
            failures.Count(m => m.Contains("Unhandled promise rejection", StringComparison.Ordinal)));
    }

    [Fact(Timeout = 600000)]
    public void The_Report_Names_The_Error()
    {
        var failures = CaptureFailures("Promise.reject(new TypeError('describedMarker'));");

        // The reason is described as a console would describe it — name and message — rather than
        // as "[object Object]", which would say nothing about what failed.
        Assert.Contains(failures, m => m.Contains("TypeError: describedMarker", StringComparison.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void A_Non_Error_Reason_Is_Still_Described()
    {
        var failures = CaptureFailures("Promise.reject('plainStringMarker');");

        Assert.Contains(failures, m => m.Contains("plainStringMarker", StringComparison.Ordinal));
    }
}
