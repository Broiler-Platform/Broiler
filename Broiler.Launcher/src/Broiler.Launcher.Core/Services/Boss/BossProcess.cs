using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace Broiler.Launcher.Services.Boss;

public sealed record BossLaunch(BossResolution Resolution, Uri Address, string InstanceId, string PipeName, string Token, string LogPath)
{
    public IReadOnlyList<string> AdditionalArguments { get; init; } = [];
}
public interface IBossProcess : IDisposable
{
    int ProcessId { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    Task RequestStopAsync(CancellationToken token);
    Task WaitForExitAsync(CancellationToken token);
    void Kill();
}
public interface IBossProcessRunner { IBossProcess Start(BossLaunch launch); }

public sealed class BossProcessRunner : IBossProcessRunner
{
    public IBossProcess Start(BossLaunch launch)
    {
        var info = new ProcessStartInfo(launch.Resolution.WithDotnet ? "dotnet" : launch.Resolution.Path!)
        {
            UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(launch.Resolution.Path!)!,
        };
        if (launch.Resolution.WithDotnet) info.ArgumentList.Add(launch.Resolution.Path!);
        foreach (string arg in new[] { "--urls", launch.Address.AbsoluteUri.TrimEnd('/'), "--contentRoot", info.WorkingDirectory }) info.ArgumentList.Add(arg);
        foreach (string arg in launch.AdditionalArguments) info.ArgumentList.Add(arg);
        info.Environment.Remove("ASPNETCORE_URLS");
        info.Environment.Remove("DOTNET_URLS");
        info.Environment["BROILER_BOSS_INSTANCE_ID"] = launch.InstanceId;
        info.Environment["BROILER_BOSS_CONTROL_PIPE"] = launch.PipeName;
        info.Environment["BROILER_BOSS_CONTROL_TOKEN"] = launch.Token;
        info.Environment["BROILER_BOSS_LOG_PATH"] = launch.LogPath;
        // BOSS writes its own file log. No parent-owned stdout pipe can break when the launcher exits.
        var process = Process.Start(info) ?? throw new IOException("Could not start the BOSS process.");
        return new OwnedProcess(process, launch);
    }
    private sealed class OwnedProcess(Process process, BossLaunch launch) : IBossProcess
    {
        public int ProcessId => process.Id;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public async Task RequestStopAsync(CancellationToken token)
        {
            using var pipe = new NamedPipeClientStream(".", launch.PipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(1000, token).ConfigureAwait(false);
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(launch.Token + "\n"), token).ConfigureAwait(false);
            await pipe.FlushAsync(token).ConfigureAwait(false);
        }
        public Task WaitForExitAsync(CancellationToken token) => process.WaitForExitAsync(token);
        public void Kill() { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        public void Dispose() => process.Dispose();
    }
}
