using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Broiler.Launcher.Models;
using Broiler.Launcher.Installation;

namespace Broiler.Launcher.Services;

/// <summary>
/// Cross-platform implementation of application discovery and launching for the Broiler suite.
/// Supports user customization, persistent settings, and multi-stage auto-detection.
/// </summary>
public sealed class AppLauncherService : IAppLauncherService
{
    private readonly IReadOnlyList<BroilerAppInfo> _apps;
    private readonly ILauncherSettingsStore _settingsStore;
    private LauncherSettings _settings;
    private readonly bool _isWindows;
    private readonly Func<string, string?> _environment;
    private readonly string _currentDirectory;
    private readonly Func<ProcessStartInfo, int?> _startProcess;
    private readonly IInstallationInventory _inventory;

    public AppLauncherService(
        IReadOnlyList<BroilerAppInfo>? apps = null,
        ILauncherSettingsStore? settingsStore = null,
        bool? isWindows = null,
        Func<string, string?>? environment = null,
        string? currentDirectory = null,
        Func<ProcessStartInfo, int?>? startProcess = null,
        IInstallationInventory? inventory = null)
    {
        _isWindows = isWindows ?? OperatingSystem.IsWindows();
        _inventory = inventory ?? new InstallationInventory(OfficeInstallPaths.CreateDefault());
        _environment = environment ?? Environment.GetEnvironmentVariable;
        _currentDirectory = currentDirectory ?? Environment.CurrentDirectory;
        _startProcess = startProcess ?? (info => { using var process = Process.Start(info); return process?.Id; });
        _apps = apps ?? BroilerAppInfo.CreateDefaultSuite();
        _settingsStore = settingsStore ?? new LauncherSettingsStore();
        _settings = _settingsStore.Load();
    }

    public IReadOnlyList<BroilerAppInfo> Apps => _apps;

    public LauncherSettings Settings => _settings;

    public string? SettingsLoadError => _settingsStore.LastLoadError;

    public void UpdateSettings(LauncherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        LauncherSettingsValidation.Validate(settings);
        _settingsStore.Save(settings);
        _settings = settings;
    }

    public AppResolution ResolveApp(BroilerAppInfo app, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.GetPlatform(_isWindows) is null)
            return AppResolution.NotFound("Unsupported on this platform");
        AppCustomSettings appSetting = _settings.GetOrCreate(app.Id);

        // If custom path is enabled and not in pure auto-detect mode
        if (!appSetting.UseAutoDetect)
        {
            if (string.IsNullOrWhiteSpace(appSetting.ExecutablePath)) return AppResolution.NotFound("Custom path is empty; choose a path or select Auto-detect in Settings.");
            string expandedPath = Environment.ExpandEnvironmentVariables(appSetting.ExecutablePath);
            if (File.Exists(expandedPath))
            {
                bool isDll = expandedPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                return AppResolution.Available(
                    Path.GetFullPath(expandedPath),
                    launchWithDotnet: isDll,
                    statusText: "Ready (Custom)",
                    isCustom: true);
            }

            return AppResolution.NotFound("Custom Path Not Found");
        }

        // Auto-detect executable
        return AutoDetect(app, baseDirectory);
    }

    public AppResolution AutoDetect(BroilerAppInfo app, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        string searchBase = baseDirectory ?? AppContext.BaseDirectory;
        bool isWindows = _isWindows;
        var platform = app.GetPlatform(isWindows);
        if (platform is null) return AppResolution.NotFound("Unsupported on this platform");

        // Managed scope identity comes from receipts, independently of portable/workspace discovery.
        var preferredScope = _settings.GetOrCreate(app.Id).PreferredInstalledScope;
        foreach (var scope in new[] { preferredScope, preferredScope == InstallScope.User ? InstallScope.Machine : InstallScope.User })
        {
            var installed = _inventory.Find(app.Id.ToString().ToLowerInvariant(), scope);
            if (_isWindows && installed.State == InstallationState.Installed && installed.Receipt?.Rid == "win-x64")
                return AppResolution.Available(installed.ExecutablePath!, installed.Receipt!.Invocation == "dotnet",
                    $"Installed for {(scope == InstallScope.User ? "user" : "all users")} ({installed.Receipt.Version})") with
                { InstallScope = scope, InstalledVersion = installed.Receipt.Version };
        }

        // 1. Direct local lookup in application directory
        string directExe = Path.Combine(searchBase, platform.BinaryName);
        if (File.Exists(directExe))
        {
            return AppResolution.Available(Path.GetFullPath(directExe), launchWithDotnet: directExe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase), statusText: "Ready (Local)");
        }

        string directDll = Path.Combine(searchBase, platform.DllName);
        if (File.Exists(directDll))
        {
            return AppResolution.Available(Path.GetFullPath(directDll), launchWithDotnet: true, statusText: "Ready (Local)");
        }

        // Isolated Office portable payloads live alongside the launcher directory.
        string bundleRoot = Path.GetFileName(Path.TrimEndingDirectorySeparator(searchBase)).Equals("launcher", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(searchBase))! : searchBase;
        string portable = Path.Combine(bundleRoot, "products", app.Id.ToString().ToLowerInvariant());
        foreach (string name in new[] { platform.BinaryName, platform.DllName })
        {
            string candidate = Path.Combine(portable, name);
            if (File.Exists(candidate)) return AppResolution.Available(Path.GetFullPath(candidate), name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase), "Ready (Portable)");
        }

        // 2. Probing workspace tree upward from base directory and current directory
        var candidateRoots = new List<string>();
        CollectCandidateRoots(searchBase, candidateRoots);
        CollectCandidateRoots(_currentDirectory, candidateRoots);

        foreach (string root in candidateRoots)
        {
            foreach (string relative in platform.ProbePaths)
            {
                string candidate = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate))
                {
                    bool isDll = candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
                    return AppResolution.Available(Path.GetFullPath(candidate), launchWithDotnet: isDll, statusText: "Ready (Workspace)");
                }
            }
        }

        // 3. Search system PATH
        string targetName = platform.BinaryName;
        string? pathEnv = _environment("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            char separator = Path.PathSeparator;
            foreach (string entry in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(entry, targetName);
                    if (File.Exists(candidate))
                    {
                        return AppResolution.Available(Path.GetFullPath(candidate), launchWithDotnet: candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase), statusText: "Ready (PATH)");
                    }
                }
                catch
                {
                    // Ignore invalid entries in PATH
                }
            }
        }

        return AppResolution.NotFound();
    }

    public LaunchResult LaunchApp(BroilerAppInfo app, string? arguments = null, string? baseDirectory = null)
    {
        try { return LaunchAppWithArguments(app, LaunchArguments.Parse(arguments), baseDirectory); }
        catch (FormatException ex) { return LaunchResult.Failure(ex.Message, ex); }
    }

    public LaunchResult LaunchAppWithArguments(BroilerAppInfo app, IReadOnlyList<string> arguments, string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(arguments);
        try
        {
            AppResolution resolution = ResolveApp(app, baseDirectory);
            if (!resolution.IsAvailable || string.IsNullOrEmpty(resolution.ResolvedPath))
                return LaunchResult.Failure($"Cannot launch {app.Title}: {resolution.StatusText}.");
            var info = new ProcessStartInfo
            {
                FileName = resolution.LaunchWithDotnet ? "dotnet" : resolution.ResolvedPath,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(resolution.ResolvedPath) ?? string.Empty,
            };
            if (resolution.LaunchWithDotnet) info.ArgumentList.Add(resolution.ResolvedPath);
            foreach (var token in LaunchArguments.Parse(_settings.GetOrCreate(app.Id).Arguments)) info.ArgumentList.Add(token);
            foreach (var token in arguments) info.ArgumentList.Add(token);
            int? pid = _startProcess(info);
            return pid.HasValue
                ? LaunchResult.Success(pid.Value, $"Launched {app.Title} (PID: {pid})", resolution.ResolvedPath)
                : LaunchResult.Failure($"Failed to start process for {app.Title}.");
        }
        catch (Exception ex)
        {
            return LaunchResult.Failure($"Error launching {app.Title}: {ex.Message}" +
                " Check executable permissions and required .NET/native runtimes.", ex);
        }
    }

    private void CollectCandidateRoots(string startingDir, List<string> roots)
    {
        try
        {
            var dir = new DirectoryInfo(startingDir);
            while (dir is not null && dir.Exists)
            {
                if (!roots.Contains(dir.FullName, _isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal))
                    roots.Add(dir.FullName);

                // Stop traversing beyond drive root or repository boundary
                if (File.Exists(Path.Combine(dir.FullName, ".git")) ||
                    Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                    File.Exists(Path.Combine(dir.FullName, "Broiler.sln")) ||
                    File.Exists(Path.Combine(dir.FullName, "Broiler.slnx")))
                {
                    break;
                }

                dir = dir.Parent;
            }
        }
        catch
        {
            // Directory access security or parsing failure
        }
    }
}
