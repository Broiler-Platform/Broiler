using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Broiler.Layout.Engine;
using Xunit;

namespace Broiler.Layout.Tests;

/// <summary>
/// The prefetch walk issues a document's image loads on pooled worker threads, and
/// <see cref="DocumentRoot"/> is thread-scoped — so a worker sees the pinning thread's root only
/// because <see cref="ImagePrefetch.RunAll"/> carries it across, the same way it carries the
/// viewport and the document mode.
/// </summary>
/// <remarks>
/// This is worth a test of its own because losing it is silent and expensive rather than visibly
/// wrong: the load still happens, it just happens against the real network instead of being
/// recognised as unreachable, one blocking round trip per image on the layout thread.
/// </remarks>
public sealed class ImagePrefetchDocumentRootTests
{
    private static readonly string[] ServedHosts = ["web-platform.test"];

    [Fact]
    public void RunAll_CarriesTheDocumentRootOntoEveryWorker()
    {
        // Enough loads that the walk takes its parallel path rather than the serial fallback.
        const int LoadCount = 64;

        var seenRoots = new ConcurrentBag<string?>();
        var seenUnreachable = new ConcurrentBag<bool>();

        var loads = new Action[LoadCount];
        for (var i = 0; i < LoadCount; i++)
        {
            loads[i] = () =>
            {
                seenRoots.Add(DocumentRoot.Current);
                // The decision the load itself makes, taken on the worker that would make it.
                seenUnreachable.Add(DocumentRoot.IsUnreachableAbsoluteUrl("http://example.com/a.png"));
            };
        }

        var previousDegree = ImagePrefetch.MaxDegreeOfParallelism;
        ImagePrefetch.MaxDegreeOfParallelism = 8;
        try
        {
            using var scope = DocumentRoot.Pin("/corpus", ServedHosts);
            ImagePrefetch.RunAll(loads, viewportWidth: 800, viewportHeight: 600, rootWritingMode: null);
        }
        finally
        {
            ImagePrefetch.MaxDegreeOfParallelism = previousDegree;
        }

        Assert.Equal(LoadCount, seenRoots.Count);
        Assert.All(seenRoots, root => Assert.Equal("/corpus", root));
        Assert.All(seenUnreachable, unreachable => Assert.True(unreachable));
    }

    [Fact]
    public void RunAll_LeavesTheWorkerThreadsUnpinnedAfterwards()
    {
        // The pin is scoped per load, so a pooled thread does not carry one document's root into
        // whatever runs on it next — the reason the ambient state around it is cleared too.
        const int LoadCount = 64;

        var loads = new Action[LoadCount];
        for (var i = 0; i < LoadCount; i++)
            loads[i] = () => { };

        var previousDegree = ImagePrefetch.MaxDegreeOfParallelism;
        ImagePrefetch.MaxDegreeOfParallelism = 8;
        try
        {
            using (var scope = DocumentRoot.Pin("/corpus", ServedHosts))
                ImagePrefetch.RunAll(loads, viewportWidth: 800, viewportHeight: 600, rootWritingMode: null);

            var leaked = new ConcurrentBag<string?>();
            var probes = new Action[LoadCount];
            for (var i = 0; i < LoadCount; i++)
                probes[i] = () => leaked.Add(DocumentRoot.Current);

            // No pin this time: a worker that kept the previous root would report it here.
            ImagePrefetch.RunAll(probes, viewportWidth: 800, viewportHeight: 600, rootWritingMode: null);

            Assert.All(leaked, root => Assert.Null(root));
        }
        finally
        {
            ImagePrefetch.MaxDegreeOfParallelism = previousDegree;
        }
    }
}
