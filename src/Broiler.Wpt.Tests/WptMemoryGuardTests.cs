using System.Diagnostics;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Covers the per-test RAM cap: the guard itself (with a scripted sampler, so the
/// assertions do not depend on the real resident set), how the cap is resolved from the
/// command line/environment, and the end-to-end abort through
/// <see cref="Program.RunTestWithTimeout"/>.
/// </summary>
public sealed class WptMemoryGuardTests
{
    private static readonly TimeSpan FastSampling = TimeSpan.FromMilliseconds(5);
    private const long OneMebibyte = WptMemoryGuard.BytesPerMebibyte;

    /// <summary>A sampler that walks a scripted series of readings, repeating the last one.</summary>
    private static Func<long> ScriptedSampler(params long[] samples)
    {
        int index = -1;
        return () => samples[Math.Min(Interlocked.Increment(ref index), samples.Length - 1)];
    }

    /// <summary>A task that never completes, standing in for a runaway render.</summary>
    private static Task<string> NeverCompletes() => new TaskCompletionSource<string>().Task;

    [Fact(Timeout = 600000)]
    public void Returns_Result_When_Test_Stays_Under_The_Limit()
    {
        var guard = new WptMemoryGuard(
            limitBytes: 512 * OneMebibyte,
            ScriptedSampler(100 * OneMebibyte, 180 * OneMebibyte),
            sampleInterval: FastSampling);

        var result = guard.Wait(Task.Delay(30).ContinueWith(_ => "rendered"), TimeSpan.FromSeconds(10));

        Assert.Equal("rendered", result);
        Assert.True(guard.IsEnabled);
    }

    [Fact(Timeout = 600000)]
    public void Aborts_When_A_Test_Grows_Past_The_Limit()
    {
        var guard = new WptMemoryGuard(
            limitBytes: 1024 * OneMebibyte,
            ScriptedSampler(240 * OneMebibyte, 900 * OneMebibyte, 1500 * OneMebibyte),
            sampleInterval: FastSampling);

        var exceeded = Assert.Throws<WptMemoryLimitExceededException>(
            () => guard.Wait(NeverCompletes(), TimeSpan.FromSeconds(10)));

        Assert.Equal(1024 * OneMebibyte, exceeded.LimitBytes);
        Assert.Equal(240 * OneMebibyte, exceeded.BaselineBytes);
        Assert.Equal(1500 * OneMebibyte, exceeded.ObservedBytes);
        Assert.Equal(1260 * OneMebibyte, exceeded.GrowthBytes);
        Assert.Equal("grew from 240 MiB to 1500 MiB, +1260 MiB", exceeded.Describe());
        Assert.Contains("grew by 1260 MiB", exceeded.Message);
        Assert.Contains("over the 1024 MiB limit", exceeded.Message);
        Assert.Equal(1500 * OneMebibyte, guard.PeakBytes);
    }

    [Fact(Timeout = 600000)]
    public void A_Large_Baseline_Left_Behind_By_Earlier_Tests_Is_Not_Charged_To_This_One()
    {
        // A worker that earlier tests grew to 1.8 GiB — or a harness process that cannot
        // hand its pages back — must not fail whichever test happens to run next. Only
        // what this test itself adds counts against the cap.
        var guard = new WptMemoryGuard(
            limitBytes: 1024 * OneMebibyte,
            ScriptedSampler(1800 * OneMebibyte, 1900 * OneMebibyte),
            sampleInterval: FastSampling);

        var result = guard.Wait(Task.Delay(30).ContinueWith(_ => "rendered"), TimeSpan.FromSeconds(10));

        Assert.Equal("rendered", result);
    }

    [Fact(Timeout = 600000)]
    public void Unreadable_Samples_Never_Abort_A_Test()
    {
        // -1 means "the resident set could not be read" (the process exited, or the
        // platform does not report it), which must not be mistaken for a violation.
        var guard = new WptMemoryGuard(
            limitBytes: 1024 * OneMebibyte,
            ScriptedSampler(-1),
            sampleInterval: FastSampling);

        var result = guard.Wait(Task.Delay(40).ContinueWith(_ => "rendered"), TimeSpan.FromSeconds(10));

        Assert.Equal("rendered", result);
        Assert.Equal(-1, guard.PeakBytes);
    }

    [Fact(Timeout = 600000)]
    public void Sampler_Failures_Are_Treated_As_Unreadable()
    {
        var guard = new WptMemoryGuard(
            limitBytes: 1024 * OneMebibyte,
            () => throw new InvalidOperationException("process has exited"),
            sampleInterval: FastSampling);

        Assert.Equal("rendered", guard.Wait(Task.Delay(40).ContinueWith(_ => "rendered"), TimeSpan.FromSeconds(10)));
    }

    [Fact(Timeout = 600000)]
    public void A_Disabled_Limit_Never_Samples()
    {
        var sampled = false;
        var guard = new WptMemoryGuard(
            limitBytes: 0,
            () => { sampled = true; return 8L * 1024 * OneMebibyte; },
            sampleInterval: FastSampling);

        Assert.False(guard.IsEnabled);
        Assert.Equal("rendered", guard.Wait(Task.Delay(30).ContinueWith(_ => "rendered"), TimeSpan.FromSeconds(10)));
        Assert.False(sampled);
    }

    [Fact(Timeout = 600000)]
    public void Timeout_Still_Wins_When_Memory_Stays_Under_The_Limit()
    {
        var guard = new WptMemoryGuard(
            limitBytes: 1024 * OneMebibyte,
            ScriptedSampler(100 * OneMebibyte),
            sampleInterval: FastSampling);

        Assert.Throws<TimeoutException>(
            () => guard.Wait(NeverCompletes(), TimeSpan.FromMilliseconds(50)));
    }

    [Fact(Timeout = 600000)]
    public void A_Faulted_Render_Surfaces_Its_Original_Exception()
    {
        var guard = new WptMemoryGuard(
            limitBytes: 1024 * OneMebibyte,
            ScriptedSampler(100 * OneMebibyte),
            sampleInterval: FastSampling);
        var faulting = Task.Run(new Func<string>(() =>
        {
            Thread.Sleep(20);
            throw new InvalidDataException("bad reference png");
        }));

        var error = Assert.Throws<InvalidDataException>(() => guard.Wait(faulting, TimeSpan.FromSeconds(10)));
        Assert.Equal("bad reference png", error.Message);
    }

    [Fact(Timeout = 600000)]
    public void Process_Samplers_Report_A_Live_Resident_Set()
    {
        using var process = Process.GetCurrentProcess();
        var sampler = WptMemoryGuard.CreateProcessSampler(process);

        Assert.True(sampler() > 0);
        Assert.True(WptMemoryGuard.CreateCurrentProcessSampler()() > 0);
    }

    [Theory]
    [InlineData(0, "0 MiB")]
    [InlineData(1536L * 1024, "1.5 MiB")]
    [InlineData(1024L * 1024 * 1024, "1024 MiB")]
    [InlineData(-1, "unknown")]
    public void Formats_Byte_Counts_As_Mebibytes(long bytes, string expected)
    {
        Assert.Equal(expected, WptMemoryGuard.FormatMebibytes(bytes));
    }

    [Fact(Timeout = 600000)]
    public void Memory_Limit_Defaults_To_One_Gibibyte()
    {
        Assert.True(Program.TryResolveRunTestMemoryLimit(null, out var limitBytes, out var error));
        Assert.Null(error);
        Assert.Equal(1024 * OneMebibyte, limitBytes);
    }

    [Fact(Timeout = 600000)]
    public void Command_Line_Memory_Limit_Wins_And_Zero_Disables_The_Guard()
    {
        Assert.True(Program.TryResolveRunTestMemoryLimit(2048, out var limitBytes, out _));
        Assert.Equal(2048 * OneMebibyte, limitBytes);

        Assert.True(Program.TryResolveRunTestMemoryLimit(0, out var disabledBytes, out _));
        Assert.Equal(0, disabledBytes);
    }

    [Fact(Timeout = 600000)]
    public void Aborts_A_Test_Whose_Render_Balloons_In_Process()
    {
        var testPath = Path.Combine(Path.GetTempPath(), "memory-guard-abort.html");
        Program.RunTestExecutor = static (runner, path, referenceDir, wptPath) =>
        {
            // Let the guard record its baseline before the render starts growing, the
            // ordering the harness sees in a real run.
            Thread.Sleep(100);

            // Touch every page so the allocation lands in the resident set rather than
            // staying a lazily-committed reservation.
            var ballast = new byte[128 * OneMebibyte];
            for (int i = 0; i < ballast.Length; i += 4096)
                ballast[i] = 1;

            Thread.Sleep(TimeSpan.FromSeconds(5));
            GC.KeepAlive(ballast);
            return new WptTestResult { TestPath = path, Passed = true };
        };

        try
        {
            var result = Program.RunTestWithTimeout(
                new WptTestRunner(),
                testPath,
                Path.Combine(Path.GetTempPath(), "references"),
                Path.GetTempPath(),
                TimeSpan.FromSeconds(30),
                workerThreadStarted: null,
                memoryLimitBytes: 16 * OneMebibyte);

            Assert.False(result.Passed);
            Assert.Equal(FailureCategory.MemoryLimitExceeded, result.Category);
            Assert.Contains("exceeding the 16 MiB per-test memory limit", result.Message);
            Assert.NotNull(result.StackTrace);
            Assert.Contains("=== Memory limit diagnostics ===", result.StackTrace);
            Assert.Contains("Memory limit detection stack", result.StackTrace);
        }
        finally
        {
            Program.ResetTestHooks();
        }
    }

    [Fact(Timeout = 600000)]
    public void Leaves_A_Well_Behaved_Test_Alone()
    {
        Program.RunTestExecutor = static (runner, path, referenceDir, wptPath) =>
            new WptTestResult { TestPath = path, Passed = true };

        try
        {
            var result = Program.RunTestWithTimeout(
                new WptTestRunner(),
                Path.Combine(Path.GetTempPath(), "memory-guard-pass.html"),
                Path.Combine(Path.GetTempPath(), "references"),
                Path.GetTempPath(),
                TimeSpan.FromSeconds(30),
                workerThreadStarted: null,
                memoryLimitBytes: 1024 * OneMebibyte);

            Assert.True(result.Passed);
            Assert.Equal(FailureCategory.None, result.Category);
        }
        finally
        {
            Program.ResetTestHooks();
        }
    }
}
