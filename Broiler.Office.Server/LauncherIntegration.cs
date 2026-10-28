using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>Opt-in local lifecycle support for a launcher-owned BOSS process.</summary>
internal sealed class LauncherIntegration : IDisposable
{
    public string InstanceId { get; }
    public ILoggerProvider Logs { get; }
    private readonly string _pipe;
    private readonly byte[] _token;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _control;
    private CancellationTokenRegistration _stopping;
    private LauncherIntegration(string id, string pipe, string token, string logPath)
    {
        InstanceId = id; _pipe = pipe; _token = Encoding.UTF8.GetBytes(token);
        Logs = new FileLogs(logPath);
    }
    public static LauncherIntegration? FromEnvironment()
    {
        string? id = Environment.GetEnvironmentVariable("BROILER_BOSS_INSTANCE_ID");
        if (id is null) return null; // Console and administrator-managed services retain their existing behavior.
        string? pipe = Environment.GetEnvironmentVariable("BROILER_BOSS_CONTROL_PIPE");
        string? token = Environment.GetEnvironmentVariable("BROILER_BOSS_CONTROL_TOKEN");
        string? log = Environment.GetEnvironmentVariable("BROILER_BOSS_LOG_PATH");
        if (!Regex.IsMatch(id, @"^[a-f0-9]{32}\z") || pipe != "broiler-boss-" + id ||
            token is null || !Regex.IsMatch(token, @"^[a-f0-9]{64}\z") || log is null || !Path.IsPathFullyQualified(log))
            throw new InvalidOperationException("Invalid launcher lifecycle environment.");
        return new(id, pipe, token, log);
    }
    public void Start(IHostApplicationLifetime lifetime)
    {
        _stopping = lifetime.ApplicationStopping.Register(_shutdown.Cancel);
        _control = Task.Run(async () =>
        {
            while (!_shutdown.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(_pipe, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(_shutdown.Token);
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    deadline.CancelAfter(TimeSpan.FromSeconds(2));
                    byte[] input = new byte[65];
                    int total = 0;
                    while (total < input.Length)
                    {
                        int read = await pipe.ReadAsync(input.AsMemory(total), deadline.Token);
                        if (read == 0) break;
                        total += read;
                        if (input.AsSpan(0, total).Contains((byte)'\n')) break;
                    }
                    if (total == 65 && input[64] == '\n' && CryptographicOperations.FixedTimeEquals(input.AsSpan(0, 64), _token))
                    {
                        lifetime.StopApplication();
                        return;
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException) { if (!_shutdown.IsCancellationRequested) await Task.Delay(100); }
            }
        });
    }
    public void Dispose()
    {
        _shutdown.Cancel();
        _stopping.Dispose();
        try { _control?.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        Logs.Dispose();
        _shutdown.Dispose();
    }
    private sealed class FileLogs : ILoggerProvider
    {
        private readonly object _gate = new();
        private readonly StreamWriter _writer;
        private long _written;
        private bool _closed;
        public FileLogs(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _writer = new(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        }
        public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
        private void Write(string line)
        {
            lock (_gate)
            {
                if (_closed || _written >= 10 * 1024 * 1024) return;
                // Bound each entry as well as the total log; log content never controls lifecycle.
                if (line.Length > 16384) line = line[..16384];
                _writer.WriteLine(line);
                _written += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
            }
        }
        public void Dispose() { lock (_gate) { if (!_closed) { _closed = true; _writer.Dispose(); } } }
        private sealed class FileLogger(FileLogs owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(level)) owner.Write($"{DateTimeOffset.UtcNow:O} {level} {category}: {formatter(state, exception)}{(exception is null ? "" : " " + exception)}");
            }
        }
    }
}
