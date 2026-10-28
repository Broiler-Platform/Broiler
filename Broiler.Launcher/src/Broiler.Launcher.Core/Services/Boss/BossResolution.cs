using Broiler.Launcher.Installation;
using Broiler.Launcher.Models;

namespace Broiler.Launcher.Services.Boss;

public sealed record BossResolution(bool Available, string? Path, bool WithDotnet, string Origin, string? Error = null);
public interface IBossResolver { BossResolution Resolve(BossSettings settings); }

public sealed class BossResolver(IInstallationInventory inventory, string baseDirectory, string currentDirectory) : IBossResolver
{
    public static BossResolver CreateDefault() => new(new InstallationInventory(OfficeInstallPaths.CreateDefault()), AppContext.BaseDirectory, Environment.CurrentDirectory);
    public BossResolution Resolve(BossSettings settings)
    {
        try
        {
            if (!settings.UseAutoDetect)
                return Validate(Environment.ExpandEnvironmentVariables(settings.ExecutablePath ?? ""), "Custom");
            foreach (var scope in new[] { InstallScope.User, InstallScope.Machine })
            {
                var installed = inventory.Find("boss", scope);
                if (installed.State == InstallationState.Installed) return Validate(installed.ExecutablePath!, scope.ToString());
            }
            string root = Path.GetFileName(Path.TrimEndingDirectorySeparator(baseDirectory)).Equals("launcher", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(baseDirectory))! : baseDirectory;
            var directories = new List<(string Path, string Origin)> { (Path.Combine(root, "products", "boss"), "Portable"), (baseDirectory, "Local") };
            foreach (string start in new[] { baseDirectory, currentDirectory })
                for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
                {
                    foreach (string configuration in new[] { "Release-Windows", "Release", "Debug" })
                    {
                        directories.Add((Path.Combine(dir.FullName, "Broiler.Office.Server", "bin", configuration, "net10.0", "win-x64", "publish"), "Workspace"));
                        directories.Add((Path.Combine(dir.FullName, "Broiler.Office.Server", "bin", configuration, "net10.0"), "Workspace"));
                    }
                    if (File.Exists(Path.Combine(dir.FullName, ".git")) || Directory.Exists(Path.Combine(dir.FullName, ".git"))) break;
                }
            foreach (string pathEntry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                directories.Add((pathEntry, "PATH"));
            var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            string nativeName = OperatingSystem.IsWindows() ? "Broiler.Office.Server.exe" : "Broiler.Office.Server";
            BossResolution? incomplete = null;
            foreach (var directory in directories)
                foreach (string name in new[] { nativeName, "Broiler.Office.Server.dll" })
                {
                    string candidate;
                    try { candidate = Path.GetFullPath(Path.Combine(directory.Path, name)); }
                    catch (ArgumentException) { continue; }
                    if (!seen.Add(candidate) || !File.Exists(candidate)) continue;
                    var result = Validate(candidate, directory.Origin);
                    if (result.Available) return result;
                    incomplete ??= result;
                }
            return incomplete ?? new(false, null, false, "Missing", "BOSS was not found. Use Tools → BOSS → Install for user/all.");
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        { return new(false, null, false, "Invalid", ex.Message); }
    }
    private static BossResolution Validate(string path, string origin)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new(false, null, false, origin, "The BOSS executable was not found.");
        path = Path.GetFullPath(path);
        string root = Path.GetDirectoryName(path)!;
        string framework = Path.Combine(root, "wwwroot", "_framework");
        if (!File.Exists(Path.Combine(root, "wwwroot", "index.html")) || !Directory.Exists(framework) || !Directory.EnumerateFiles(framework, "*.js").Any())
            return new(false, path, false, origin, "The BOSS web bundle is missing. Repair the BOSS installation.");
        bool dll = path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        if (dll && !File.Exists(Path.ChangeExtension(path, ".runtimeconfig.json")))
            return new(false, path, true, origin, "The BOSS .NET runtime configuration is missing.");
        return new(true, path, dll, origin);
    }
}
