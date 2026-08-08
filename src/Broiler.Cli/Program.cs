using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Broiler.HTML.Image;
using Broiler.HtmlBridge.Logging;

namespace Broiler.Cli;

// Disambiguate the unqualified `DateTime` type: the Broiler.JS engine now exposes a top-level
// `Broiler.DateTime` namespace which, from this `Broiler.*` namespace, otherwise shadows
// System.DateTime by simple-name lookup. The alias must sit inside the Broiler.Cli namespace
// scope so it is resolved before the enclosing `Broiler` namespace's `DateTime` member.
using DateTime = System.DateTime;

/// <summary>
/// Entry point for the Broiler CLI tool.
/// Supports website capture via local rendering engines and engine smoke testing.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Composition root: register the concrete image codecs Broiler.Graphics decodes/encodes with.
        Broiler.Graphics.BImageCodecs.Use(
            new Broiler.Media.MediaCodecCatalog(Broiler.Media.Image.Managed.ManagedImageCodecs.CreateCodecs()));

        string? pdfInputPath = null;
        // Repeatable inputs: one is the single-input path this CLI has always had, more than
        // one is a batch. Accumulating rather than overwriting is what makes `--convert-doc a
        // --convert-doc b` mean both instead of only the last.
        var convertDocInputs = new List<string>();
        var urls = new List<string>();
        var captureImageUrls = new List<string>();
        string? output = null;
        string? outputDir = null;
        string? outputFormat = null;
        int? threads = null;
        int? fuzzSeed = null;
        // Set by the parent on a fuzz shard so the child appends a machine-readable totals line
        // for it to sum. Not part of the documented surface — it only means anything to a parent.
        bool emitFuzzTotals = false;
        bool preservePdfLayout = false;
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
                case "--convert-pdf" when i + 1 < args.Length:
                    pdfInputPath = args[++i];
                    break;
                case "--convert-doc" when i + 1 < args.Length:
                    convertDocInputs.Add(args[++i]);
                    break;
                case "--url" when i + 1 < args.Length:
                    urls.Add(args[++i]);
                    break;
                case "--capture-image" when i + 1 < args.Length:
                    captureImageUrls.Add(args[++i]);
                    break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--output-dir" when i + 1 < args.Length:
                    outputDir = args[++i];
                    break;
                case "--output-format" when i + 1 < args.Length:
                    outputFormat = args[++i];
                    break;
                case "--threads" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out int parsedThreads) || parsedThreads <= 0)
                    {
                        Console.Error.WriteLine("Error: '--threads' must be a positive integer.");
                        return 1;
                    }

                    threads = parsedThreads;
                    break;
                case "--seed" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out int parsedSeed))
                    {
                        Console.Error.WriteLine("Error: '--seed' must be an integer.");
                        return 1;
                    }

                    fuzzSeed = parsedSeed;
                    break;
                case "--preserve-layout":
                    preservePdfLayout = true;
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
                case "--emit-totals":
                    emitFuzzTotals = true;
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
                case "--convert-pdf":
                case "--convert-doc":
                case "--url":
                case "--capture-image":
                case "--output":
                case "--output-dir":
                case "--output-format":
                case "--threads":
                case "--seed":
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

        if (convertDocInputs.Count > 0)
            return RunDocumentConversion(convertDocInputs, output, outputDir, outputFormat, threads);

        if (testEngines)
        {
            return RunEngineTests();
        }

        if (fuzzLayout)
        {
            var fuzzService = new LayoutFuzzService();
            return fuzzService.Run(
                fuzzCount,
                seed: fuzzSeed,
                outputDir: output,
                threads: threads,
                emitTotals: emitFuzzTotals);
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

        // Batch capture runs each input as its own child process rather than on a thread: the
        // render path still has unsynchronised caches on process-wide singletons (roadmap items
        // #9/#8), so a second render in this process would be a data race. See BatchRunner.
        if (captureImageUrls.Count > 1 || urls.Count > 1)
        {
            exitCode = RunBatchCapture(
                captureImageUrls,
                urls,
                outputDir,
                outputFormat,
                threads,
                width,
                height,
                fullPage,
                followFirstLink,
                timeoutSeconds);
            EmitDiagnostics(diagHandler, diagnosticEntries);
            return exitCode;
        }

        string? url = urls.Count > 0 ? urls[0] : null;
        string? captureImageUrl = captureImageUrls.Count > 0 ? captureImageUrls[0] : null;

        if (pdfInputPath is not null)
        {
            try
                {
                    var converter = new PdfConverterProcessRunner();
                    exitCode = await converter.RunAsync(pdfInputPath, output, preservePdfLayout);
                }
                catch (FileNotFoundException ex)
                {
                    Console.Error.WriteLine($"PDF conversion failed: {ex.Message}");
                exitCode = 1;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"File I/O error: {ex.Message}");
                exitCode = 1;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"PDF conversion failed: {ex.Message}");
                exitCode = 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error: {ex.Message}");
                exitCode = 1;
            }
        }
        else if (captureImageUrl is not null)
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
                        CaptureArtifactMetadata.WriteImageSidecar(output);

                        Console.WriteLine($"Image capture saved to {output} ({CaptureArtifactMetadata.CurrentRenderBackend.Label})");
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

    /// <summary>
    /// Converts one document, or a batch of them across threads. Conversion touches no engine
    /// state — Broiler.Documents has no assignable statics, only lookup tables — so a batch runs
    /// in this process rather than paying for one child per file.
    /// </summary>
    private static int RunDocumentConversion(
        IReadOnlyList<string> inputs,
        string? output,
        string? outputDir,
        string? outputFormat,
        int? threads)
    {
        if (inputs.Count == 1 && outputDir is null)
        {
            if (output is null)
            {
                Console.Error.WriteLine("Error: '--convert-doc' requires '--output <file.txt|file.rtf|file.docx|file.html|file.md>' (or '--output-dir <DIR> --output-format <EXT>').");
                return 1;
            }

            return DocumentConvertService.Convert(inputs[0], output);
        }

        if (outputDir is null)
        {
            Console.Error.WriteLine("Error: converting several documents requires '--output-dir <DIR>'.");
            return 1;
        }

        if (outputFormat is null)
        {
            Console.Error.WriteLine("Error: '--output-dir' requires '--output-format <txt|rtf|docx|html|md>' — the output format comes from the file extension, which a derived name has to be told.");
            return 1;
        }

        var items = BatchRunner.DeriveOutputPaths(inputs, outputDir, outputFormat);
        int degree = BatchRunner.ResolveDegreeOfParallelism(threads, items.Count, Environment.ProcessorCount);
        Console.WriteLine($"Converting {items.Count} document(s) on {degree} thread(s)…");

        return BatchRunner.RunInProcess(
            items,
            degree,
            (item, standardOutput, standardError) =>
                DocumentConvertService.Convert(item.Input, item.OutputPath, standardOutput, standardError))
            .ExitCode;
    }

    /// <summary>
    /// Captures several URLs concurrently, each in its own child <c>Broiler.Cli</c> process.
    /// The child invocation is an ordinary single-URL capture, so a batched capture and a
    /// hand-run one produce the same file by the same code path.
    /// </summary>
    private static int RunBatchCapture(
        IReadOnlyList<string> captureImageUrls,
        IReadOnlyList<string> urls,
        string? outputDir,
        string? outputFormat,
        int? threads,
        int width,
        int height,
        bool fullPage,
        bool followFirstLink,
        int timeoutSeconds)
    {
        if (captureImageUrls.Count > 0 && urls.Count > 0)
        {
            Console.Error.WriteLine("Error: '--capture-image' and '--url' cannot be batched together in one run.");
            return 1;
        }

        if (outputDir is null)
        {
            Console.Error.WriteLine("Error: capturing several URLs requires '--output-dir <DIR>'.");
            return 1;
        }

        bool imageMode = captureImageUrls.Count > 0;
        var inputs = imageMode ? captureImageUrls : urls;
        var extension = outputFormat ?? (imageMode ? "png" : "html");
        var items = BatchRunner.DeriveOutputPaths(inputs, outputDir, extension);
        int degree = BatchRunner.ResolveDegreeOfParallelism(threads, items.Count, Environment.ProcessorCount);

        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"Capturing {items.Count} URL(s) across {degree} worker process(es)…");

        return BatchRunner.RunInChildProcesses(items, degree, (item, _) =>
        {
            var arguments = new List<string>
            {
                imageMode ? "--capture-image" : "--url",
                item.Input,
                "--output",
                item.OutputPath,
                "--timeout",
                timeoutSeconds.ToString(CultureInfo.InvariantCulture),
            };

            if (imageMode)
            {
                arguments.Add("--width");
                arguments.Add(width.ToString(CultureInfo.InvariantCulture));
                arguments.Add("--height");
                arguments.Add(height.ToString(CultureInfo.InvariantCulture));
            }

            if (fullPage)
                arguments.Add("--full-page");
            if (followFirstLink)
                arguments.Add("--follow-first-link");

            return arguments;
        }).ExitCode;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: Broiler.Cli --convert-pdf <PDF> [--output <FILE|DIR>] [--preserve-layout]");
        Console.WriteLine("Usage: Broiler.Cli --convert-doc <RTF|DOCX|HTML|MARKDOWN> --output <FILE.txt|FILE.rtf|FILE.docx|FILE.html|FILE.md>");
        Console.WriteLine("Usage: Broiler.Cli --url <URL> --output <FILE> [OPTIONS]");
        Console.WriteLine("       Broiler.Cli --capture-image <URL> --output <FILE> [OPTIONS]");
        Console.WriteLine("       Broiler.Cli --test-engines");
        Console.WriteLine("       Broiler.Cli --fuzz-layout [--count <N>] [--output <DIR>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --convert-pdf <PDF>    Convert a PDF file via the external Broiler.Pdf app");
        Console.WriteLine("  --convert-doc <FILE>   Convert RTF/DOCX/HTML/Markdown to TXT, RTF, DOCX, HTML, or Markdown through Broiler.Documents");
        Console.WriteLine("  --url <URL>            The URL of the website to capture");
        Console.WriteLine("  --capture-image <URL>  Capture the website as an image (PNG or JPEG)");
        Console.WriteLine("  --output <FILE|DIR>    Output file path, or output directory for PDF conversion");
        Console.WriteLine("  --output-dir <DIR>     Output directory for a batch. --convert-doc, --url and");
        Console.WriteLine("                         --capture-image may each be repeated; with more than one input");
        Console.WriteLine("                         the items run concurrently and each result is written to");
        Console.WriteLine("                         <DIR>/<input name>.<format>");
        Console.WriteLine("  --output-format <EXT>  Output extension for a batch (default: png for --capture-image,");
        Console.WriteLine("                         html for --url; required for --convert-doc batches)");
        Console.WriteLine("  --threads <N>          Concurrency for a batch or for --fuzz-layout (default: one per");
        Console.WriteLine("                         core). --threads 1 reproduces the sequential run exactly.");
        Console.WriteLine("  --seed <N>             Base seed for --fuzz-layout (default: a clock reading)");
        Console.WriteLine("  --preserve-layout      Preserve PDF page layout and styling during PDF conversion");
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
        Console.WriteLine();
        Console.WriteLine("PDF conversion requires the standalone Broiler.Pdf app.");
        Console.WriteLine("Set BROILER_PDF_APP or place Broiler.Pdf beside Broiler.Cli.");
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

        var renderBackend = CaptureArtifactMetadata.CurrentRenderBackend;
        var jsonEntries = snapshot.Select(e => new
        {
            timestamp = e.Timestamp.ToString("o"),
            category = e.Category.ToString(),
            level = e.Level.ToString(),
            context = e.Context,
            message = e.Message,
            exception = e.Exception?.ToString(),
            renderBackendId = renderBackend.Id,
            renderBackendDisplayName = renderBackend.DisplayName,
            renderBackendLabel = renderBackend.Label,
        });

        var json = JsonSerializer.Serialize(jsonEntries, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }
}

internal sealed record CaptureRenderBackendMetadata(
    string Id,
    string DisplayName,
    string Label);

internal static class CaptureArtifactMetadata
{
    internal static CaptureRenderBackendMetadata CurrentRenderBackend =>
        new(
            BGraphicsBackend.CurrentId,
            BGraphicsBackend.CurrentDisplayName,
            BGraphicsBackend.CurrentLabel);

    internal static string GetSidecarPath(string outputPath) => $"{outputPath}.metadata.json";

    internal static void WriteImageSidecar(string outputPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        var renderBackend = CurrentRenderBackend;
        var metadata = new Dictionary<string, object?>
        {
            ["generatedAt"] = DateTime.UtcNow.ToString("o"),
            ["imagePath"] = Path.GetFileName(outputPath),
            ["renderBackend"] = new Dictionary<string, string>
            {
                ["id"] = renderBackend.Id,
                ["displayName"] = renderBackend.DisplayName,
                ["label"] = renderBackend.Label,
            },
        };

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        File.WriteAllText(GetSidecarPath(outputPath), json);
    }
}

internal sealed class PdfConverterProcessRunner
{
    private const string PdfAppEnvironmentVariable = "BROILER_PDF_APP";
    private const string PdfAppName = "Broiler.Pdf";

    public async Task<int> RunAsync(string inputPdfPath, string? outputPath, bool preserveLayout = false)
    {
        var command = ResolveCommand();
        using var process = new Process
        {
            StartInfo = CreateStartInfo(command, inputPdfPath, outputPath, preserveLayout),
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start the external PDF converter '{command.FileName}': {ex.Message}",
                ex);
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (!string.IsNullOrEmpty(standardOutput))
            Console.Out.Write(standardOutput);

        if (!string.IsNullOrEmpty(standardError))
            Console.Error.Write(standardError);

        return process.ExitCode;
    }

    internal static PdfProcessCommand ResolveCommand()
    {
        var configuredCommand = TryResolveConfiguredCommand();
        if (configuredCommand is not null)
            return configuredCommand;

        var adjacentCommand = TryResolveAdjacentCommand();
        if (adjacentCommand is not null)
            return adjacentCommand;

        var sourceProjectCommand = TryResolveSourceProjectCommand();
        if (sourceProjectCommand is not null)
            return sourceProjectCommand;

        throw new InvalidOperationException(
            "Broiler PDF conversion now lives in the standalone Broiler.Pdf app. " +
            "To continue, use one of these options: " +
            "1) place Broiler.Pdf beside Broiler.Cli, " +
            "2) set BROILER_PDF_APP to the Broiler.Pdf executable or .dll path, or " +
            "3) run 'dotnet run --project src/Broiler.Pdf -- --input <PDF> [--output <FILE|DIR>]'.");
    }

    private static ProcessStartInfo CreateStartInfo(PdfProcessCommand command, string inputPdfPath, string? outputPath, bool preserveLayout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in command.BaseArguments)
            startInfo.ArgumentList.Add(argument);

        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add(inputPdfPath);

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add(outputPath);
        }

        if (preserveLayout)
            startInfo.ArgumentList.Add("--preserve-layout");

        return startInfo;
    }

    private static PdfProcessCommand? TryResolveConfiguredCommand()
    {
        var configuredPath = Environment.GetEnvironmentVariable(PdfAppEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        var fullPath = Path.GetFullPath(configuredPath);
        return CreateCommandForPath(fullPath)
            ?? throw new InvalidOperationException(
                $"{PdfAppEnvironmentVariable} points to '{fullPath}', but that file does not exist.");
    }

    private static PdfProcessCommand? TryResolveAdjacentCommand()
    {
        var baseDirectory = AppContext.BaseDirectory;
        foreach (var candidate in new[]
        {
            Path.Combine(baseDirectory, PdfAppName),
            Path.Combine(baseDirectory, PdfAppName + ".exe"),
            Path.Combine(baseDirectory, PdfAppName + ".dll"),
        })
        {
            var command = CreateCommandForPath(candidate);
            if (command is not null)
                return command;
        }

        return null;
    }

    private static PdfProcessCommand? TryResolveSourceProjectCommand()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var projectPath = Path.Combine(directory.FullName, "src", PdfAppName, PdfAppName + ".csproj");
            if (File.Exists(projectPath))
            {
                return new PdfProcessCommand(
                    "dotnet",
                    ["run", "--project", projectPath, "--"]);
            }
        }

        return null;
    }

    private static PdfProcessCommand? CreateCommandForPath(string candidatePath)
    {
        if (!File.Exists(candidatePath))
            return null;

        if (string.Equals(Path.GetExtension(candidatePath), ".dll", StringComparison.OrdinalIgnoreCase))
            return new PdfProcessCommand("dotnet", [candidatePath]);

        return new PdfProcessCommand(candidatePath, []);
    }
}

internal sealed record PdfProcessCommand(string FileName, IReadOnlyList<string> BaseArguments);
