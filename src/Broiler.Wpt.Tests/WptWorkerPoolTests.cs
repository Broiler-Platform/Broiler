using System.Diagnostics;

namespace Broiler.Wpt.Tests;

/// <summary>
/// Covers the parallel WPT worker pool: how the pool is sized, and the property the
/// exit gate rests on — that a run's results, and therefore its pass/fail/skip
/// classification, are identical at 1 and <em>N</em> workers.
/// </summary>
public sealed class WptWorkerPoolTests
{
    private const long OneGibibyte = 1024L * 1024 * 1024;

    private static Program.TestQueueOptions QueueOptions(string wptPath, int workerCount) => new(
        WptPath: wptPath,
        ReferenceDir: Path.Combine(wptPath, "references"),
        FailureImagesDir: null,
        VerifyReference: false,
        MemoryLimitBytes: 0,
        PassThresholdPercent: 99,
        ReferenceTestsOnly: false,
        Timeout: TimeSpan.FromSeconds(30),
        WorkerCount: workerCount,
        // In-process: the queue is exercised through the RunTestExecutor override rather
        // than by starting real worker processes, which keeps these tests to milliseconds.
        UseWorkerIsolation: false);

    private static IReadOnlyList<WptTestResult> RunQueue(
        IReadOnlyList<string> tests,
        int workerCount,
        Func<string, WptTestResult> executor)
    {
        Program.RunTestExecutor = (_, testPath, _, _) => executor(testPath);
        try
        {
            return Program.RunDiscoveredTests(
                tests,
                QueueOptions(Path.GetTempPath(), workerCount),
                new WptTestRunner(),
                new Program.WptRunProgress(tests.Count),
                Stopwatch.StartNew());
        }
        finally
        {
            Program.RunTestExecutor = null!;
        }
    }

    private static List<string> TestPaths(int count) =>
        Enumerable.Range(0, count).Select(i => Path.Combine(Path.GetTempPath(), $"test-{i:D3}.html")).ToList();

    [Fact(Timeout = 600000)]
    public void Results_Are_In_Discovery_Order_Even_When_Tests_Finish_Out_Of_Order()
    {
        // The pool's whole safety argument: a slot writes its result into the index it
        // claimed, so completion order — which is what parallelism actually changes —
        // never reaches the reporting code.
        var tests = TestPaths(24);
        var results = RunQueue(tests, workerCount: 6, testPath =>
        {
            // Earlier tests take longest, so completion order is close to reversed.
            int ordinal = int.Parse(Path.GetFileNameWithoutExtension(testPath)["test-".Length..]);
            Thread.Sleep((24 - ordinal) * 2);
            return new WptTestResult { TestPath = testPath, Passed = true };
        });

        Assert.Equal(tests, results.Select(r => r.TestPath).ToList());
    }

    [Fact(Timeout = 600000)]
    public void Classification_Is_Identical_At_One_And_Many_Workers()
    {
        var tests = TestPaths(30);

        WptTestResult Decide(string testPath)
        {
            int ordinal = int.Parse(Path.GetFileNameWithoutExtension(testPath)["test-".Length..]);
            return (ordinal % 3) switch
            {
                0 => new WptTestResult { TestPath = testPath, Passed = true, MatchPercent = 100 },
                1 => new WptTestResult { TestPath = testPath, Passed = false, MatchPercent = 42 },
                _ => new WptTestResult { TestPath = testPath, Skipped = true, SkipReason = SkipReason.MissingReferenceImage },
            };
        }

        var sequential = RunQueue(tests, workerCount: 1, Decide);
        var parallel = RunQueue(tests, workerCount: 8, Decide);

        Assert.Equal(
            sequential.Select(r => (r.TestPath, r.Passed, r.Skipped, r.MatchPercent)).ToList(),
            parallel.Select(r => (r.TestPath, r.Passed, r.Skipped, r.MatchPercent)).ToList());
    }

    [Fact(Timeout = 600000)]
    public void Every_Test_Runs_Exactly_Once_Across_The_Pool()
    {
        var tests = TestPaths(50);
        var seen = new System.Collections.Concurrent.ConcurrentBag<string>();

        var results = RunQueue(tests, workerCount: 8, testPath =>
        {
            seen.Add(testPath);
            return new WptTestResult { TestPath = testPath, Passed = true };
        });

        Assert.Equal(tests.Count, results.Count);
        Assert.Equal(tests.OrderBy(p => p, StringComparer.Ordinal), seen.OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact(Timeout = 600000)]
    public void An_Empty_Test_Set_Runs_No_Slots()
    {
        var results = RunQueue([], workerCount: 8, _ => throw new InvalidOperationException("should not run"));

        Assert.Empty(results);
    }

    [Fact(Timeout = 600000)]
    public void Pool_Is_Sized_By_Memory_When_Memory_Is_Scarcer_Than_Cores()
    {
        // 16 cores but 4 GiB free: 1.5 GiB per worker allows 2, and RAM is the binding
        // constraint because each worker may hold a full 1 GiB per-test allowance.
        Assert.Equal(
            2,
            Program.ResolveWorkerCount(
                requested: null,
                useWorkerIsolation: true,
                processorCount: 16,
                availableMemoryBytes: 4 * OneGibibyte));
    }

    [Fact(Timeout = 600000)]
    public void Pool_Is_Sized_By_Cores_When_Memory_Is_Plentiful()
    {
        Assert.Equal(
            4,
            Program.ResolveWorkerCount(
                requested: null,
                useWorkerIsolation: true,
                processorCount: 4,
                availableMemoryBytes: 64 * OneGibibyte));
    }

    [Fact(Timeout = 600000)]
    public void Pool_Never_Drops_Below_One_Worker()
    {
        Assert.Equal(
            1,
            Program.ResolveWorkerCount(
                requested: null,
                useWorkerIsolation: true,
                processorCount: 8,
                availableMemoryBytes: 256L * 1024 * 1024));
    }

    [Fact(Timeout = 600000)]
    public void Unknown_Available_Memory_Does_Not_Parallelize()
    {
        // Guessing high on an unreadable memory figure is how a runner OOM-kills a CI box,
        // so an unknown budget means one worker, exactly as before this option existed.
        Assert.Equal(
            1,
            Program.ResolveWorkerCount(
                requested: null,
                useWorkerIsolation: true,
                processorCount: 32,
                availableMemoryBytes: 0));
    }

    [Fact(Timeout = 600000)]
    public void An_Explicit_Worker_Count_Overrides_The_Automatic_Sizing()
    {
        Assert.Equal(
            12,
            Program.ResolveWorkerCount(
                requested: 12,
                useWorkerIsolation: true,
                processorCount: 4,
                availableMemoryBytes: 2 * OneGibibyte));
    }

    [Fact(Timeout = 600000)]
    public void In_Process_Mode_Is_Always_One_Worker()
    {
        // The engine is single-threaded by design; two documents in one process would race
        // the shared render state the static-state audit enumerates.
        Assert.Equal(
            1,
            Program.ResolveWorkerCount(
                requested: 8,
                useWorkerIsolation: false,
                processorCount: 16,
                availableMemoryBytes: 64 * OneGibibyte));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("16", 16)]
    public void Worker_Count_Parses_Positive_Integers(string value, int expected)
    {
        Assert.True(Program.TryParseWorkerCount(value, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Fact(Timeout = 600000)]
    public void Worker_Count_Accepts_Auto_As_No_Explicit_Request()
    {
        Assert.True(Program.TryParseWorkerCount("auto", out var parsed));
        Assert.Null(parsed);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("many")]
    [InlineData("")]
    public void Worker_Count_Rejects_Values_That_Cannot_Size_A_Pool(string value)
    {
        Assert.False(Program.TryParseWorkerCount(value, out _));
    }

    [Fact(Timeout = 600000)]
    public void Available_Memory_Is_Reported_As_A_Usable_Figure()
    {
        // Either source may be missing on a given platform, but both returning nothing
        // would silently pin every run to one worker — so assert the runner can read one.
        Assert.True(Program.GetAvailableMemoryBytes() > 0);
    }

    [Fact(Timeout = 600000)]
    public void Termination_Diagnostics_Name_Every_In_Flight_Test()
    {
        var progress = new Program.WptRunProgress(totalTests: 100);
        progress.StartTest(slot: 0, testPath: "/wpt/css/a.html", displayPath: "css/a.html", ordinal: 41);
        progress.StartTest(slot: 2, testPath: "/wpt/css/c.html", displayPath: "css/c.html", ordinal: 43);
        progress.SetWorkerProcessId(slot: 0, workerProcessId: 111);
        progress.SetWorkerProcessId(slot: 2, workerProcessId: 333);

        var diagnostics = Program.CreateTerminationDiagnostics("SIGTERM", progress.Snapshot());

        Assert.Contains("Current test: (41/100) css/a.html", diagnostics);
        Assert.Contains("In-flight tests: 2", diagnostics);
        Assert.Contains("(41/100) css/a.html [pid 111", diagnostics);
        Assert.Contains("(43/100) css/c.html [pid 333", diagnostics);
    }

    [Fact(Timeout = 600000)]
    public void A_Completed_Slot_Leaves_The_In_Flight_List()
    {
        var progress = new Program.WptRunProgress(totalTests: 10);
        progress.StartTest(slot: 0, testPath: "/wpt/css/a.html", displayPath: "css/a.html", ordinal: 1);
        progress.StartTest(slot: 1, testPath: "/wpt/css/b.html", displayPath: "css/b.html", ordinal: 2);
        progress.CompleteTest(slot: 0, completed: 1, passed: 1, failed: 0, skipped: 0);

        var snapshot = progress.Snapshot();

        Assert.Equal(1, snapshot.Completed);
        Assert.Equal("css/b.html", snapshot.CurrentDisplayPath);
        Assert.Single(snapshot.InFlight!);
    }

    /// <summary>
    /// The pool and the in-process render threads multiply if nobody divides them; these cover the
    /// division and the one case where the runner must keep its hands off.
    /// </summary>
    [Theory]
    [InlineData(4, 4, "1 per worker")]
    [InlineData(1, 4, "4 per worker")]
    [InlineData(2, 8, "4 per worker")]
    [InlineData(8, 4, "1 per worker")]   // more workers than cores: never below one thread each
    public void Render_Threads_Are_Cores_Divided_By_Workers(int workers, int cores, string expected)
    {
        var previousRaster = Environment.GetEnvironmentVariable("BROILER_RASTER_THREADS");
        var previousTiles = Environment.GetEnvironmentVariable("BROILER_RASTER_TILES");
        var previousDecode = Environment.GetEnvironmentVariable("BROILER_IMAGE_DECODE_THREADS");
        var previousStyle = Environment.GetEnvironmentVariable("BROILER_STYLE_THREADS");
        try
        {
            Environment.SetEnvironmentVariable("BROILER_RASTER_THREADS", null);
            Environment.SetEnvironmentVariable("BROILER_RASTER_TILES", null);
            Environment.SetEnvironmentVariable("BROILER_IMAGE_DECODE_THREADS", null);
            Environment.SetEnvironmentVariable("BROILER_STYLE_THREADS", null);

            var description = Program.ApplyRenderThreadBudget(workers, cores);

            Assert.StartsWith(expected, description, StringComparison.Ordinal);
            var perWorker = expected.Split(' ')[0];
            Assert.Equal(perWorker, Environment.GetEnvironmentVariable("BROILER_RASTER_THREADS"));

            // The tile budget gets the whole per-worker figure rather than a share of it: a tile
            // view runs its bands inline, so the two never spend cores at the same time.
            Assert.Equal(perWorker, Environment.GetEnvironmentVariable("BROILER_RASTER_TILES"));
            Assert.Equal(perWorker, Environment.GetEnvironmentVariable("BROILER_IMAGE_DECODE_THREADS"));

            // Item #12's warm pass is the earliest of the four to spend a thread — it runs before
            // layout, let alone paint — so a pool that divided the others and not this one would
            // open every worker's render with `cores` threads on the cascade.
            Assert.Equal(perWorker, Environment.GetEnvironmentVariable("BROILER_STYLE_THREADS"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BROILER_RASTER_THREADS", previousRaster);
            Environment.SetEnvironmentVariable("BROILER_RASTER_TILES", previousTiles);
            Environment.SetEnvironmentVariable("BROILER_IMAGE_DECODE_THREADS", previousDecode);
            Environment.SetEnvironmentVariable("BROILER_STYLE_THREADS", previousStyle);
        }
    }

    [Fact(Timeout = 600000)]
    public void An_Explicit_Render_Thread_Setting_Is_Left_Alone()
    {
        var previousRaster = Environment.GetEnvironmentVariable("BROILER_RASTER_THREADS");
        var previousTiles = Environment.GetEnvironmentVariable("BROILER_RASTER_TILES");
        var previousDecode = Environment.GetEnvironmentVariable("BROILER_IMAGE_DECODE_THREADS");
        var previousStyle = Environment.GetEnvironmentVariable("BROILER_STYLE_THREADS");
        try
        {
            Environment.SetEnvironmentVariable("BROILER_RASTER_THREADS", "7");
            Environment.SetEnvironmentVariable("BROILER_RASTER_TILES", "5");
            Environment.SetEnvironmentVariable("BROILER_IMAGE_DECODE_THREADS", "3");
            Environment.SetEnvironmentVariable("BROILER_STYLE_THREADS", "2");

            Program.ApplyRenderThreadBudget(workerCount: 4, processorCount: 4);

            Assert.Equal("7", Environment.GetEnvironmentVariable("BROILER_RASTER_THREADS"));
            Assert.Equal("5", Environment.GetEnvironmentVariable("BROILER_RASTER_TILES"));
            Assert.Equal("3", Environment.GetEnvironmentVariable("BROILER_IMAGE_DECODE_THREADS"));
            Assert.Equal("2", Environment.GetEnvironmentVariable("BROILER_STYLE_THREADS"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BROILER_RASTER_THREADS", previousRaster);
            Environment.SetEnvironmentVariable("BROILER_RASTER_TILES", previousTiles);
            Environment.SetEnvironmentVariable("BROILER_IMAGE_DECODE_THREADS", previousDecode);
            Environment.SetEnvironmentVariable("BROILER_STYLE_THREADS", previousStyle);
        }
    }
}
