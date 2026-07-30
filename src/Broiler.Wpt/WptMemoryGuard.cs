using System.Diagnostics;
using System.Globalization;

namespace Broiler.Wpt;

/// <summary>
/// Thrown when a single WPT test grew the memory of the process rendering it past the
/// configured cap. Carries the samples that tripped the guard so the failure result can
/// report the footprint the test started from as well as the one it reached.
/// </summary>
internal sealed class WptMemoryLimitExceededException : Exception
{
    internal WptMemoryLimitExceededException(long limitBytes, long baselineBytes, long observedBytes)
        : base($"Render memory grew by {WptMemoryGuard.FormatMebibytes(observedBytes - baselineBytes)} " +
               $"during this test, over the {WptMemoryGuard.FormatMebibytes(limitBytes)} limit.")
    {
        LimitBytes = limitBytes;
        BaselineBytes = baselineBytes;
        ObservedBytes = observedBytes;
    }

    /// <summary>Configured per-test cap, in bytes.</summary>
    internal long LimitBytes { get; }

    /// <summary>Resident set of the rendering process when this test started, in bytes.</summary>
    internal long BaselineBytes { get; }

    /// <summary>Resident set of the sample that tripped the guard, in bytes.</summary>
    internal long ObservedBytes { get; }

    /// <summary>Bytes this test added on top of <see cref="BaselineBytes"/>.</summary>
    internal long GrowthBytes => ObservedBytes - BaselineBytes;

    /// <summary>Human-readable "grew from 240 MiB to 1500 MiB, +1260 MiB" summary.</summary>
    internal string Describe() =>
        $"grew from {WptMemoryGuard.FormatMebibytes(BaselineBytes)} to " +
        $"{WptMemoryGuard.FormatMebibytes(ObservedBytes)}, +{WptMemoryGuard.FormatMebibytes(GrowthBytes)}";
}

/// <summary>
/// Per-test RAM cap for the WPT runner. A single WPT document can allocate without
/// bound — a runaway layout, an enormous decoded image — and with no cap the render
/// process (or the CI machine) is OOM-killed, taking the whole shard's results with it.
/// The guard polls the resident set of the process rendering the current test while the
/// harness waits for that test's result and aborts the test once it has grown past the
/// cap: the same "cap each test, let the suite continue" role that Broiler.JS's test262
/// runner fills with its POSIX <c>--memory-limit-mb</c> rlimit.
/// </summary>
/// <remarks>
/// The rlimit approach does not transfer directly. test262 spawns a fresh process per
/// test, so a kernel-enforced address-space cap is exactly a per-test cap; the WPT runner
/// reuses one worker across tests, where the same rlimit would instead cap the worker's
/// accumulated lifetime. Hence polling, and hence <em>growth since this test started</em>
/// rather than the absolute footprint: a measured css run leaves the worker resident at
/// ~860 MiB after 147 well-behaved tests (mostly garbage the GC has no pressure to
/// reclaim), so an absolute check near 1 GiB would fail whichever innocent test happened
/// to run next. Growth charges each test only for what it itself allocated.
/// <para>
/// Growth alone does not bound the harness, so it is paired with worker recycling: the
/// runner restarts a worker whose resident set has reached the cap <em>between</em> tests,
/// where no test has to be blamed for it. See <c>Program.WptWorkerClient</c>.
/// </para>
/// </remarks>
internal sealed class WptMemoryGuard
{
    internal const long BytesPerMebibyte = 1024 * 1024;

    /// <summary>
    /// How often the resident set is sampled while a test runs. Short enough to catch a
    /// ballooning test well before the OS notices, cheap enough (one <c>/proc</c> read)
    /// to run for every test in the suite.
    /// </summary>
    internal static readonly TimeSpan DefaultSampleInterval = TimeSpan.FromMilliseconds(100);

    private readonly long _limitBytes;
    private readonly Func<long> _sampleBytes;
    private readonly TimeSpan _sampleInterval;

    /// <param name="limitBytes">Per-test growth cap in bytes; zero or negative disables the guard.</param>
    /// <param name="sampleBytes">
    /// Returns the current resident set of the rendering process in bytes, or a negative
    /// value when it cannot be read (the process exited, or the platform does not report it).
    /// </param>
    /// <param name="sampleInterval">Sampling interval; defaults to <see cref="DefaultSampleInterval"/>.</param>
    internal WptMemoryGuard(long limitBytes, Func<long> sampleBytes, TimeSpan? sampleInterval = null)
    {
        _limitBytes = limitBytes;
        _sampleBytes = sampleBytes;
        _sampleInterval = sampleInterval ?? DefaultSampleInterval;
    }

    /// <summary>Resident set sampled when the wait started, in bytes (-1 when unavailable).</summary>
    internal long BaselineBytes { get; private set; } = -1;

    /// <summary>Highest resident set observed during the wait, in bytes (-1 when unavailable).</summary>
    internal long PeakBytes { get; private set; } = -1;

    /// <summary>True when the guard is armed; false when the cap is disabled.</summary>
    internal bool IsEnabled => _limitBytes > 0;

    /// <summary>
    /// Waits for <paramref name="task"/> — the in-flight render of a single test — while
    /// sampling memory, and returns its result.
    /// </summary>
    /// <exception cref="TimeoutException"><paramref name="timeout"/> elapsed first.</exception>
    /// <exception cref="WptMemoryLimitExceededException">The cap was exceeded first.</exception>
    internal T Wait<T>(Task<T> task, TimeSpan timeout)
    {
        if (!IsEnabled)
            return task.WaitAsync(timeout).GetAwaiter().GetResult();

        BaselineBytes = Sample();
        PeakBytes = BaselineBytes;

        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            var remaining = timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException("The operation has timed out.");

            var slice = remaining < _sampleInterval ? remaining : _sampleInterval;

            bool completed;
            try
            {
                completed = task.Wait(slice);
            }
            catch (AggregateException)
            {
                // The render faulted. Fall through and rethrow the original exception
                // (not the aggregate) through the awaiter, matching the plain
                // WaitAsync().GetAwaiter().GetResult() path.
                completed = true;
            }

            if (completed)
                return task.GetAwaiter().GetResult();

            ObserveMemory();
        }
    }

    /// <summary>
    /// Samples memory once and throws if this test has grown past the cap. Samples that
    /// cannot be read are ignored: an unreadable resident set must not fail an otherwise
    /// healthy test.
    /// </summary>
    private void ObserveMemory()
    {
        var sample = Sample();
        if (sample < 0)
            return;

        // The first readable sample doubles as the baseline when the wait started before
        // the rendering process was observable (a worker still starting up, say).
        if (BaselineBytes < 0)
            BaselineBytes = sample;
        if (sample > PeakBytes)
            PeakBytes = sample;

        if (sample - BaselineBytes > _limitBytes)
            throw new WptMemoryLimitExceededException(_limitBytes, BaselineBytes, sample);
    }

    private long Sample()
    {
        try
        {
            return _sampleBytes();
        }
        catch (Exception ex) when (
            ex is InvalidOperationException ||
            ex is NotSupportedException ||
            ex is PlatformNotSupportedException ||
            ex is System.ComponentModel.Win32Exception)
        {
            // The process exited between the liveness check and the read, or the platform
            // does not expose the counter. Treat as "unknown" rather than as a violation.
            return -1;
        }
    }

    /// <summary>
    /// Samples the resident set of <paramref name="process"/>, returning -1 once it exits.
    /// </summary>
    internal static Func<long> CreateProcessSampler(Process process) => () =>
    {
        if (process.HasExited)
            return -1;

        process.Refresh();
        return process.WorkingSet64;
    };

    // Held for the life of the run rather than per test: Process.GetCurrentProcess()
    // opens a handle, and a suite is thousands of tests long.
    private static readonly Process CurrentProcess = Process.GetCurrentProcess();

    /// <summary>
    /// Samples the resident set of the harness process itself (in-process mode).
    /// </summary>
    internal static Func<long> CreateCurrentProcessSampler() => static () =>
    {
        CurrentProcess.Refresh();
        return CurrentProcess.WorkingSet64;
    };

    /// <summary>Formats a byte count as "1024 MiB"; negative values render as "unknown".</summary>
    internal static string FormatMebibytes(long bytes)
    {
        if (bytes < 0)
            return "unknown";

        var mebibytes = (double)bytes / BytesPerMebibyte;
        return mebibytes.ToString("0.#", CultureInfo.InvariantCulture) + " MiB";
    }
}
