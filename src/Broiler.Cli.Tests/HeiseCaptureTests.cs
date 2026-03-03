namespace Broiler.Cli.Tests;

/// <summary>
/// Integration tests that capture https://www.heise.de/ using the rendering pipeline.
/// Each test includes retry logic to handle transient network exceptions.
/// Exceptions are logged and the test is retried until it passes or the
/// maximum number of attempts is exhausted.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Engine", "Cli")]
public class HeiseCaptureTests : IDisposable
{
    /// <summary>Maximum number of retry attempts for transient failures.</summary>
    private const int MaxRetries = 3;

    /// <summary>Delay in milliseconds between retry attempts.</summary>
    private const int RetryDelayMs = 2000;

    /// <summary>Timeout in seconds for HTTP requests.</summary>
    private const int TimeoutSeconds = 60;

    /// <summary>The reference URL to capture in every test.</summary>
    private const string HeiseUrl = "https://www.heise.de/";

    private readonly string _outputDir;

    public HeiseCaptureTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"broiler-heise-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_outputDir, true); } catch { }
    }

    /// <summary>
    /// Executes an async action with retry logic. On transient exceptions
    /// (network, timeout, task-canceled), the action is retried up to
    /// <see cref="MaxRetries"/> times with exponential back-off.
    /// All encountered exceptions are logged via the provided output helper.
    /// </summary>
    private static async Task ExecuteWithRetryAsync(
        Func<Task> action,
        List<string> exceptionLog)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await action();
                return; // success
            }
            catch (Exception ex) when (
                attempt < MaxRetries &&
                (ex is HttpRequestException
                    or TaskCanceledException
                    or TimeoutException
                    or IOException))
            {
                var delay = RetryDelayMs * attempt; // linear back-off
                exceptionLog.Add(
                    $"[Attempt {attempt}/{MaxRetries}] {ex.GetType().Name}: {ex.Message} — retrying in {delay}ms");
                await Task.Delay(delay);
            }
            // On the final attempt, exceptions propagate to fail the test.
        }
    }

    [Fact]
    public async Task CaptureHtml_HeiseDe_ProducesOutput()
    {
        var exceptionLog = new List<string>();
        var outputPath = Path.Combine(_outputDir, "heise-capture.html");
        var service = new CaptureService();

        await ExecuteWithRetryAsync(async () =>
        {
            await service.CaptureAsync(new CaptureOptions
            {
                Url = HeiseUrl,
                OutputPath = outputPath,
                TimeoutSeconds = TimeoutSeconds,
            });
        }, exceptionLog);

        Assert.True(File.Exists(outputPath), BuildFailureMessage("Captured HTML file should exist.", exceptionLog));
        var content = await File.ReadAllTextAsync(outputPath);
        Assert.True(content.Length > 0, BuildFailureMessage("Captured HTML should not be empty.", exceptionLog));
        Assert.Contains("heise", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaptureImage_HeiseDe_ProducesValidPng()
    {
        var exceptionLog = new List<string>();
        var outputPath = Path.Combine(_outputDir, "heise-capture.png");
        var service = new CaptureService();

        await ExecuteWithRetryAsync(async () =>
        {
            await service.CaptureImageAsync(new ImageCaptureOptions
            {
                Url = HeiseUrl,
                OutputPath = outputPath,
                Width = 1024,
                Height = 768,
                TimeoutSeconds = TimeoutSeconds,
            });
        }, exceptionLog);

        Assert.True(File.Exists(outputPath), BuildFailureMessage("PNG capture file should exist.", exceptionLog));
        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.True(bytes.Length > 100, BuildFailureMessage("PNG file should have meaningful content.", exceptionLog));
        // Verify PNG magic bytes
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    [Fact]
    public async Task CaptureImage_HeiseDe_ProducesValidJpeg()
    {
        var exceptionLog = new List<string>();
        var outputPath = Path.Combine(_outputDir, "heise-capture.jpg");
        var service = new CaptureService();

        await ExecuteWithRetryAsync(async () =>
        {
            await service.CaptureImageAsync(new ImageCaptureOptions
            {
                Url = HeiseUrl,
                OutputPath = outputPath,
                Width = 1024,
                Height = 768,
                TimeoutSeconds = TimeoutSeconds,
            });
        }, exceptionLog);

        Assert.True(File.Exists(outputPath), BuildFailureMessage("JPEG capture file should exist.", exceptionLog));
        var bytes = await File.ReadAllBytesAsync(outputPath);
        Assert.True(bytes.Length > 100, BuildFailureMessage("JPEG file should have meaningful content.", exceptionLog));
        // Verify JPEG magic bytes
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    /// <summary>
    /// Builds a failure message that includes the primary assertion message
    /// and any exception log entries accumulated during retries.
    /// </summary>
    private static string BuildFailureMessage(string message, List<string> exceptionLog)
    {
        if (exceptionLog.Count == 0)
            return message;

        return $"{message}\nException log:\n{string.Join("\n", exceptionLog)}";
    }
}
