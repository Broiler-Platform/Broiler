using System.Text.Json;

namespace Broiler.Launcher.Services.Boss;

public static class BossCli
{
    public static bool TryHandle(string[] args, out int exitCode, IAppLauncherService? launcherService = null)
    {
        exitCode = 0;
        if (args.Length == 0 || args[0] is not ("--boss-start" or "--boss-status")) return false;
        try
        {
            if (args.Length != 1) throw new ArgumentException("Usage: --boss-start or --boss-status. Configure BOSS through launcher settings JSON.");
            using var controller = BossController.CreateDefault(launcherService ?? new AppLauncherService());
            var state = (args[0] == "--boss-start" ? controller.StartAsync() : controller.RefreshAsync()).GetAwaiter().GetResult();
            Console.WriteLine(JsonSerializer.Serialize(new { status = state.Status.ToString(), state.Message, state.Address, state.IsOwned, state.LogPath, state.ProcessId }));
            exitCode = state.Status is BossStatus.Failed or BossStatus.Missing or BossStatus.PortConflict ? 1 : 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); exitCode = 1; }
        return true;
    }
}
