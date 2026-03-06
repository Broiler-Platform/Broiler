using System.Text.Json;
using Broiler.App.Rendering;

namespace Broiler.Cli;

/// <summary>
/// Entry point for the Broiler CLI tool.
/// Supports website capture via local rendering engines and engine smoke testing.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        string? url = null;
        string? captureImageUrl = null;
        string? output = null;
        bool fullPage = false;
        bool testEngines = false;
        bool fuzzLayout = false;
        bool followFirstLink = false;
        bool diagnostics = false;
        int fuzzCount = 1000;
        int timeoutSeconds = 30;
        int width = 1024;
        int height = 768;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url" when i + 1 < args.Length:
                    url = args[++i];
                    break;
                case "--capture-image" when i + 1 < args.Length:
                    captureImageUrl = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--timeout" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out timeoutSeconds) || timeoutSeconds <= 0)
                    {
                        Console.Error.WriteLine("Error: '--timeout' must be a positive integer (seconds).");
                        return 1;
                    }
                    break;
                case "--width" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out width) || width <= 0)
                    {
                        Console.Error.WriteLine("Error: '--width' must be a positive integer.");
                        return 1;
                    }
                    break;
                case "--height" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out height) || height <= 0)
                    {
                        Console.Error.WriteLine("Error: '--height' must be a positive integer.");
                        return 1;
                    }
                    break;
                case "--full-page":
                    fullPage = true;
                    break;
                case "--follow-first-link":
                    followFirstLink = true;
                    break;
                case "--test-engines":
                    testEngines = true;
                    break;
                case "--fuzz-layout":
                    fuzzLayout = true;
                    break;
                case "--diagnostics":
                    diagnostics = true;
                    break;
                case "--count" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out fuzzCount) || fuzzCount <= 0)
                    {
                        Console.Error.WriteLine("Error: '--count' must be a positive integer.");
                        return 1;
                    }
                    break;
                case "--url":
                case "--capture-image":
                case "--output":
                case "--timeout":
                case "--width":
                case "--height":
                case "--count":
                    Console.Error.WriteLine($"Error: '{args[i]}' requires a value.");
                    PrintUsage();
                    return 1;
                case "--help":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine($"Error: Unrecognized argument '{args[i]}'.");
                    PrintUsage();
                    return 1;
            }
        }

        if (testEngines)
        {
            return RunEngineTests();
        }

        if (fuzzLayout)
        {
            var fuzzService = new LayoutFuzzService();
            return fuzzService.Run(fuzzCount, outputDir: output);
        }

        // When --diagnostics is active, subscribe to the render logger and
        // collect entries to emit as structured JSON on stdout after the
        // main operation completes.
        List<RenderLogEntry>? diagnosticEntries = null;
        Action<RenderLogEntry>? diagHandler = null;
        if (diagnostics)
        {
            diagnosticEntries = [];
            diagHandler = entry => { lock (diagnosticEntries) diagnosticEntries.Add(entry); };
            RenderLogger.EntryLogged += diagHandler;
        }

        int exitCode;

        if (captureImageUrl is not null)
        {
            if (output is null)
            {
                Console.Error.WriteLine("Error: '--output' is required when using '--capture-image'.");
                PrintUsage();
                exitCode = 1;
            }
            else
            {
                // Support bare file paths by converting to file:// URIs.
                // Separate any fragment (e.g. "#top") before checking the filesystem.
                string? captureFragment = null;
                var hashIdx = captureImageUrl.IndexOf('#');
                var captureFilePath = hashIdx >= 0 ? captureImageUrl[..hashIdx] : captureImageUrl;
                if (hashIdx >= 0)
                    captureFragment = captureImageUrl[hashIdx..]; // includes '#'

                if (File.Exists(captureFilePath))
                {
                    captureImageUrl = new Uri(Path.GetFullPath(captureFilePath)).AbsoluteUri
                                      + (captureFragment ?? string.Empty);
                }

                if (!Uri.TryCreate(captureImageUrl, UriKind.Absolute, out var imgUri)
                    || (imgUri.Scheme != "http" && imgUri.Scheme != "https" && imgUri.Scheme != "file"))
                {
                    Console.Error.WriteLine($"Error: '{captureImageUrl}' is not a valid HTTP, HTTPS, or file URL.");
                    exitCode = 1;
                }
                else
                {
                    var imageOptions = new ImageCaptureOptions
                    {
                        Url = captureImageUrl,
                        OutputPath = output,
                        Width = width,
                        Height = height,
                        FullPage = fullPage,
                        FollowFirstLink = followFirstLink,
                        TimeoutSeconds = timeoutSeconds,
                    };

                    try
                    {
                        var service = new CaptureService();
                        await service.CaptureImageAsync(imageOptions);

                        Console.WriteLine($"Image capture saved to {output}");
                        exitCode = 0;
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.Error.WriteLine($"Capture failed: {ex.Message}");
                        exitCode = 1;
                    }
                    catch (IOException ex)
                    {
                        Console.Error.WriteLine($"File I/O error: {ex.Message}");
                        exitCode = 1;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Unexpected error: {ex.Message}");
                        exitCode = 1;
                    }
                }
            }
        }
        else if (url is null || output is null)
        {
            Console.Error.WriteLine("Error: Both --url and --output arguments are required.");
            PrintUsage();
            exitCode = 1;
        }
        else
        {
            // Support bare file paths by converting to file:// URIs.
            if (File.Exists(url))
            {
                url = new Uri(Path.GetFullPath(url)).AbsoluteUri;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "file"))
            {
                Console.Error.WriteLine($"Error: '{url}' is not a valid HTTP, HTTPS, or file URL.");
                exitCode = 1;
            }
            else
            {
                var captureOptions = new CaptureOptions
                {
                    Url = url,
                    OutputPath = output,
                    FullPage = fullPage,
                    FollowFirstLink = followFirstLink,
                    TimeoutSeconds = timeoutSeconds,
                };

                try
                {
                    var service = new CaptureService();
                    await service.CaptureAsync(captureOptions);

                    Console.WriteLine($"Capture saved to {output}");
                    exitCode = 0;
                }
                catch (HttpRequestException ex)
                {
                    Console.Error.WriteLine($"Capture failed: {ex.Message}");
                    exitCode = 1;
                }
                catch (IOException ex)
                {
                    Console.Error.WriteLine($"File I/O error: {ex.Message}");
                    exitCode = 1;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Unexpected error: {ex.Message}");
                    exitCode = 1;
                }
            }
        }

        EmitDiagnostics(diagHandler, diagnosticEntries);
        return exitCode;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Broiler.Cli --url <URL> --output <FILE> [OPTIONS]");
        Console.WriteLine("       Broiler.Cli --capture-image <URL> --output <FILE> [OPTIONS]");
        Console.WriteLine("       Broiler.Cli --test-engines");
        Console.WriteLine("       Broiler.Cli --fuzz-layout [--count <N>] [--output <DIR>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --url <URL>            The URL of the website to capture");
        Console.WriteLine("  --capture-image <URL>  Capture the website as an image (PNG or JPEG)");
        Console.WriteLine("  --output <FILE>        The output file path for the captured content");
        Console.WriteLine("  --width <PIXELS>       Image width in pixels (default: 1024, used with --capture-image)");
        Console.WriteLine("  --height <PIXELS>      Image height in pixels (default: 768, used with --capture-image)");
        Console.WriteLine("  --full-page            Capture the full page content");
        Console.WriteLine("  --follow-first-link    Follow the first link on the page before rendering");
        Console.WriteLine("  --timeout <SECS>       Navigation timeout in seconds (default: 30)");
        Console.WriteLine("  --test-engines         Run smoke tests for the embedded rendering engines");
        Console.WriteLine("  --fuzz-layout          Run layout fuzz testing with random HTML/CSS");
        Console.WriteLine("  --count <N>            Number of fuzz cases to generate (default: 1000)");
        Console.WriteLine("  --diagnostics          Emit structured JSON log output on stdout after the operation");
        Console.WriteLine("  --help                 Show this help message");
    }

    /// <summary>
    /// Runs smoke tests for all embedded rendering engines and reports results.
    /// Returns 0 if all engines pass, 1 if any engine fails.
    /// </summary>
    internal static int RunEngineTests()
    {
        var service = new EngineTestService();
        var results = service.RunAll();
        bool allPassed = true;

        foreach (var result in results)
        {
            if (result.Passed)
            {
                Console.WriteLine($"[PASS] {result.EngineName}");
            }
            else
            {
                Console.WriteLine($"[FAIL] {result.EngineName}: {result.Error}");
                allPassed = false;
            }
        }

        Console.WriteLine();
        Console.WriteLine(allPassed ? "All engine tests passed." : "Some engine tests failed.");

        return allPassed ? 0 : 1;
    }

    /// <summary>
    /// If <c>--diagnostics</c> was active, unsubscribes the handler and
    /// writes collected log entries to stdout as a JSON array.
    /// </summary>
    private static void EmitDiagnostics(
        Action<RenderLogEntry>? diagHandler,
        List<RenderLogEntry>? diagnosticEntries)
    {
        if (diagHandler is not null)
            RenderLogger.EntryLogged -= diagHandler;

        if (diagnosticEntries is null)
            return;

        RenderLogEntry[] snapshot;
        lock (diagnosticEntries) snapshot = diagnosticEntries.ToArray();

        if (snapshot.Length == 0)
            return;

        var jsonEntries = snapshot.Select(e => new
        {
            timestamp = e.Timestamp.ToString("o"),
            category = e.Category.ToString(),
            level = e.Level.ToString(),
            context = e.Context,
            message = e.Message,
            exception = e.Exception?.ToString(),
        });

        var json = JsonSerializer.Serialize(jsonEntries, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }
}
