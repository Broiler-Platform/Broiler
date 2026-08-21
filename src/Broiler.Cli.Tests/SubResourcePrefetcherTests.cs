using System.Collections.Concurrent;
using System.Diagnostics;
using Broiler.HtmlBridge;

namespace Broiler.Cli.Tests;

/// <summary>
/// Covers the prefetch/consume split behind concurrent sub-resource fetching (multithreading
/// roadmap item #2): that requests overlap, that a resource is still requested exactly once,
/// that per-host concurrency is bounded, and that a consumer sees the same value it would have
/// seen fetching inline.
/// </summary>
public sealed class SubResourcePrefetcherTests
{
    /// <summary>
    /// Runs a batch of prefetches whose fetches all block until released, and reports the highest
    /// number that were in flight at once.
    /// </summary>
    /// <remarks>
    /// Overlap is asserted by observing concurrency, not by timing the batch: a wall-clock bound
    /// would be measuring this machine's thread-pool injection rate as much as the prefetcher, and
    /// would go flaky the moment the test host schedules another class alongside this one.
    /// </remarks>
    private static int MeasurePeakConcurrency(
        IReadOnlyList<string> urls,
        int maxConcurrentRequestsPerHost,
        int expectedConcurrency)
    {
        int inFlight = 0, peak = 0;
        var gate = new object();
        using var release = new ManualResetEventSlim(false);

        var prefetcher = new SubResourcePrefetcher(
            _ =>
            {
                lock (gate)
                {
                    inFlight++;
                    peak = Math.Max(peak, inFlight);
                }

                release.Wait(TimeSpan.FromSeconds(10));

                lock (gate)
                    inFlight--;

                return "content";
            },
            maxConcurrentRequestsPerHost);

        prefetcher.Prefetch(urls);

        // Wait for the expected overlap rather than for a fixed duration: the thread pool injects
        // threads over time, so a fixed sleep is a race against an unrelated schedule.
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            lock (gate)
            {
                if (peak >= expectedConcurrency)
                    break;
            }

            Thread.Sleep(10);
        }

        // Let a cap violation show itself: the batch has reached the concurrency it was expected to,
        // so anything above it would appear in this window rather than after the release.
        Thread.Sleep(250);

        release.Set();
        foreach (var url in urls)
            prefetcher.Consume(url);

        lock (gate)
            return peak;
    }

    [Fact(Timeout = 600000)]
    public void Prefetched_Requests_Overlap_Instead_Of_Running_One_At_A_Time()
    {
        // The whole point of the item: a page's sub-resources are on the wire together, so their
        // round trips overlap instead of being paid one after another.
        var urls = Enumerable.Range(0, 4).Select(i => $"https://example.com/{i}.js").ToList();

        var peak = MeasurePeakConcurrency(urls, maxConcurrentRequestsPerHost: 4, expectedConcurrency: 2);

        Assert.True(peak >= 2, $"peak concurrency was {peak} — the prefetches are still serial");
    }

    [Fact(Timeout = 600000)]
    public void Prefetched_Content_Reaches_The_Consumer_Unchanged()
    {
        var urls = Enumerable.Range(0, 6).Select(i => $"https://example.com/{i}.js").ToList();
        var prefetcher = new SubResourcePrefetcher(url => $"body of {url}");

        prefetcher.Prefetch(urls);

        Assert.Equal(urls.Select(url => $"body of {url}").ToList(), urls.Select(prefetcher.Consume).ToList());
    }

    [Fact(Timeout = 600000)]
    public void A_Resource_Is_Fetched_Once_However_Often_It_Is_Named()
    {
        var fetches = new ConcurrentBag<string>();
        var prefetcher = new SubResourcePrefetcher(url =>
        {
            fetches.Add(url);
            return "content";
        });

        prefetcher.Prefetch(["https://example.com/a.js", "https://example.com/a.js"]);
        prefetcher.Prefetch(["https://example.com/a.js"]);
        Assert.Equal("content", prefetcher.Consume("https://example.com/a.js"));
        Assert.Equal("content", prefetcher.Consume("https://example.com/a.js"));

        Assert.Single(fetches);
        Assert.Equal(1, prefetcher.RequestedCount);
    }

    [Fact(Timeout = 600000)]
    public void A_Url_That_Was_Never_Prefetched_Is_Still_Fetched_On_Consume()
    {
        // What makes this safe to adopt one call site at a time: consuming works with or without
        // a prior prefetch, so a missed prefetch is a lost speedup and never a lost resource.
        var prefetcher = new SubResourcePrefetcher(url => $"body of {url}");

        Assert.Equal("body of https://example.com/late.js", prefetcher.Consume("https://example.com/late.js"));
    }

    [Fact(Timeout = 600000)]
    public void Concurrency_Is_Bounded_Per_Host()
    {
        // Twelve resources from one origin, allowance of three: an origin must never see more than
        // its share, however many the document names.
        var urls = Enumerable.Range(0, 12).Select(i => $"https://example.com/{i}.css").ToList();

        var peak = MeasurePeakConcurrency(urls, maxConcurrentRequestsPerHost: 3, expectedConcurrency: 3);

        Assert.True(peak <= 3, $"peak in-flight per host was {peak}, above the cap of 3");
    }

    [Fact(Timeout = 600000)]
    public void Different_Hosts_Do_Not_Share_One_Hosts_Allowance()
    {
        // The cap is per host, so a page pulling from four origins is not serialized behind one.
        var urls = Enumerable.Range(0, 4).Select(i => $"https://host{i}.example/a.css").ToList();

        var peak = MeasurePeakConcurrency(urls, maxConcurrentRequestsPerHost: 1, expectedConcurrency: 2);

        Assert.True(peak > 1, "four distinct hosts were serialized against each other");
    }

    [Fact(Timeout = 600000)]
    public void A_Failed_Fetch_Is_Reported_As_No_Content_Rather_Than_Thrown()
    {
        // A dead sub-resource is not a dead page; the consumer applies whatever it already did
        // for a fetch that came back empty.
        var prefetcher = new SubResourcePrefetcher(_ => throw new HttpRequestException("connection refused"));

        prefetcher.Prefetch(["https://example.com/gone.js"]);

        Assert.Null(prefetcher.Consume("https://example.com/gone.js"));
    }

    [Fact(Timeout = 600000)]
    public void A_Failure_Is_Not_Retried_On_Every_Consume()
    {
        int attempts = 0;
        var prefetcher = new SubResourcePrefetcher(_ =>
        {
            Interlocked.Increment(ref attempts);
            throw new HttpRequestException("connection refused");
        });

        prefetcher.Prefetch(["https://example.com/gone.js"]);
        prefetcher.Consume("https://example.com/gone.js");
        prefetcher.Consume("https://example.com/gone.js");

        Assert.Equal(1, attempts);
    }

    [Fact(Timeout = 600000)]
    public void Pending_Reports_Whether_A_Request_Was_Issued()
    {
        var prefetcher = new SubResourcePrefetcher(url => url);

        prefetcher.Prefetch(["https://example.com/a.js"]);

        Assert.True(prefetcher.IsPending("https://example.com/a.js"));
        Assert.False(prefetcher.IsPending("https://example.com/b.js"));
    }

    [Fact(Timeout = 600000)]
    public void Blank_Urls_Are_Not_Requested()
    {
        var prefetcher = new SubResourcePrefetcher(_ => throw new InvalidOperationException("should not fetch"));

        prefetcher.Prefetch(["", "   "]);

        Assert.Equal(0, prefetcher.RequestedCount);
        Assert.Null(prefetcher.Consume(""));
    }
}
