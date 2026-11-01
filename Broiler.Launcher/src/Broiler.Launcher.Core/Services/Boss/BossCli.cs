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
            Console.WriteLine(new System.Text.Json.Nodes.JsonObject { ["status"] = state.Status.ToString(), ["Message"] = state.Message, ["Address"] = state.Address?.ToString(), ["IsOwned"] = state.IsOwned, ["LogPath"] = state.LogPath, ["ProcessId"] = state.ProcessId }.ToJsonString());
            exitCode = state.Status is BossStatus.Failed or BossStatus.Missing or BossStatus.PortConflict ? 1 : 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); exitCode = 1; }
        return true;
    }
}
