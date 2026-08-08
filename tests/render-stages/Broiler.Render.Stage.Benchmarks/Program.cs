using System;
using System.Globalization;
using BenchmarkDotNet.Running;

namespace Broiler.Render.Stage.Benchmarks;

/// <summary>
/// Entry point. Two modes, because P0-a asks two different questions.
/// </summary>
/// <remarks>
/// <c>--profile</c> answers "what share of a render is each stage" and is the command the P0-a exit
/// gate is checked with — it exits non-zero when the gate is missed. Everything else falls through
/// to BenchmarkDotNet, which answers "how long does one stage take, precisely". Mixing the two into
/// one command would mean either paying BenchmarkDotNet's multi-minute run to get a share, or
/// quoting a stopwatch figure as if it had BenchmarkDotNet's error bars.
/// </remarks>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "--profile")
        {
            var iterations = ArgValue(args, "--iterations", 15);
            var warmup = ArgValue(args, "--warmup", 3);
            var json = ArgString(args, "--json");
            return StageProfile.Run(iterations, warmup, json);
        }

        if (args.Length >= 1 && args[0] == "--decode-scaling")
        {
            return DecodeScaling.Run(ArgValue(args, "--iterations", 11), ArgValue(args, "--warmup", 3));
        }

        if (args.Length >= 1 && args[0] == "--raster-scaling")
        {
            return RasterScaling.Run(ArgValue(args, "--iterations", 9), ArgValue(args, "--warmup", 2));
        }

        if (args.Length >= 1 && args[0] == "--tile-scaling")
        {
            return TileScaling.Run(ArgValue(args, "--iterations", 9), ArgValue(args, "--warmup", 2));
        }

        if (args.Length >= 1 && args[0] == "--image-prefetch-scaling")
        {
            return ImagePrefetchScaling.Run(ArgValue(args, "--iterations", 9), ArgValue(args, "--warmup", 2));
        }

        if (args.Length >= 1 && args[0] == "--gc-config")
        {
            GcConfigurationMetrics.Run(ArgValue(args, "--rounds", 20));
            return 0;
        }

        if (args.Length >= 1 && (args[0] == "--help" || args[0] == "-h"))
        {
            WriteUsage();
            return 0;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }

    private static void WriteUsage()
    {
        Console.WriteLine("Broiler render-stage benchmarks (multithreading roadmap P0-a, #19).");
        Console.WriteLine();
        Console.WriteLine("  --profile [--iterations N] [--warmup N] [--json PATH]");
        Console.WriteLine("        Attribute headless-render wall time to named stages over the corpus.");
        Console.WriteLine("        Exits 1 if any page attributes less than 90% to named stages.");
        Console.WriteLine();
        Console.WriteLine("  --decode-scaling [--iterations N] [--warmup N]");
        Console.WriteLine("        PNG/JPEG decode time against the thread budget (items #6, #7).");
        Console.WriteLine("        Exits 1 if any thread setting decodes different pixels.");
        Console.WriteLine();
        Console.WriteLine("  --raster-scaling [--iterations N] [--warmup N]");
        Console.WriteLine("        Corpus render time against the raster thread budget (item #4).");
        Console.WriteLine("        Exits 1 if any thread setting renders different pixels.");
        Console.WriteLine();
        Console.WriteLine("  --tile-scaling [--iterations N] [--warmup N]");
        Console.WriteLine("        Corpus render time against the raster tile budget (item #5).");
        Console.WriteLine("        Exits 1 if any tile setting renders different pixels.");
        Console.WriteLine();
        Console.WriteLine("  --image-prefetch-scaling [--iterations N] [--warmup N]");
        Console.WriteLine("        Render time of an image-heavy page against the concurrent-load budget (item #8).");
        Console.WriteLine("        Exits 1 if any setting renders different pixels.");
        Console.WriteLine();
        Console.WriteLine("  --gc-config [--rounds N]");
        Console.WriteLine("        Throughput and GC counters for the process's GC mode (item #19).");
        Console.WriteLine();
        Console.WriteLine("  --filter <pattern> [BenchmarkDotNet options]");
        Console.WriteLine("        Run the BenchmarkDotNet harnesses. See the project README.");
    }

    private static int ArgValue(string[] args, string name, int fallback)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return int.Parse(args[i + 1], CultureInfo.InvariantCulture);
        }

        return fallback;
    }

    private static string? ArgString(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }

        return null;
    }
}
