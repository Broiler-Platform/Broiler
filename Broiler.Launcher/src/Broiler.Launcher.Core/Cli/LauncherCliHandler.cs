using System;
using System.IO;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;

namespace Broiler.Launcher.Cli;

/// <summary>
/// Handles command-line arguments for launching apps or inspecting discovery without opening a window.
/// </summary>
public static class LauncherCliHandler
{
    public static bool TryHandleCli(string[] args, out int exitCode, IAppLauncherService? launcherService = null)
    {
        launcherService ??= new AppLauncherService();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg is "--help" or "-h" or "-?")
            {
                PrintHelp(launcherService);
                exitCode = 0;
                return true;
            }

            if (arg is "--list" or "-l")
            {
                PrintList(launcherService);
                exitCode = 0;
                return true;
            }

            if (arg is "--config" or "-c" or "--settings")
            {
                PrintConfig(launcherService);
                exitCode = 0;
                return true;
            }

            if (arg is "--app" or "-a" or "--launch")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine("Error: Missing application name after " + arg);
                    exitCode = 1;
                    return true;
                }

                string targetName = args[i + 1];
                BroilerAppInfo? targetApp = FindAppByName(launcherService, targetName);
                if (targetApp is null)
                {
                    Console.Error.WriteLine($"Error: Unknown application '{targetName}'. Available: browser, writer, cell, plate.");
                    exitCode = 1;
                    return true;
                }

                LaunchResult result = launcherService.LaunchAppWithArguments(targetApp, args[(i + 2)..]);
                if (result.Succeeded)
                {
                    Console.WriteLine(result.Message);
                    exitCode = 0;
                }
                else
                {
                    Console.Error.WriteLine(result.Message);
                    exitCode = 1;
                }
                return true;
            }
        }

        exitCode = 0;
        return false;
    }

    private static BroilerAppInfo? FindAppByName(IAppLauncherService launcherService, string name)
    {
        foreach (BroilerAppInfo app in launcherService.Apps)
        {
            if (string.Equals(app.ShortName, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(app.Id.ToString(), name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(app.Title, name, StringComparison.OrdinalIgnoreCase))
            {
                return app;
            }
        }
        return null;
    }

    private static void PrintHelp(IAppLauncherService launcherService)
    {
        Console.WriteLine("Broiler Launcher — Unified workspace launcher for Broiler Platform applications");
        Console.WriteLine();
        Console.WriteLine("Usage: Broiler.Launcher [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --app, -a <name>   Directly launch a specified application and exit");
        Console.WriteLine("  --list, -l         List all supported applications and their resolved binary paths");
        Console.WriteLine("  --config, -c       Display persistent configuration file location and custom settings");
        Console.WriteLine("  --help, -h         Display this help text");
        Console.WriteLine();
        Console.WriteLine("Applications:");
        foreach (BroilerAppInfo app in launcherService.Apps)
        {
            Console.WriteLine($"  {app.ShortName,-10} {app.Title} ({app.Tagline})");
        }
    }

    private static void PrintList(IAppLauncherService launcherService)
    {
        Console.WriteLine("Broiler Suite Applications:");
        Console.WriteLine();
        foreach (BroilerAppInfo app in launcherService.Apps)
        {
            AppResolution resolution = launcherService.ResolveApp(app);
            AppCustomSettings custom = launcherService.Settings.GetOrCreate(app.Id);
            string statusTag = resolution.IsAvailable
                ? (resolution.IsCustom ? "[Ready - Custom]" : "[Ready - Auto]")
                : $"[{resolution.StatusText}]";
            string pathText = resolution.ResolvedPath ?? "(no binary found)";
            Console.WriteLine($"  {app.Title,-18} {statusTag,-18} {pathText}");
            if (!string.IsNullOrWhiteSpace(custom.Arguments))
            {
                Console.WriteLine($"  {"",-18} {"Custom Args:",-18} {custom.Arguments}");
            }
        }
    }

    private static void PrintConfig(IAppLauncherService launcherService)
    {
        Console.WriteLine("Broiler Launcher Configuration:");
        Console.WriteLine();
        var store = new LauncherSettingsStore();
        Console.WriteLine($"  Config file: {store.SettingsFilePath}");
        Console.WriteLine($"  File exists: {File.Exists(store.SettingsFilePath)}");
        Console.WriteLine();
        Console.WriteLine("Customizations:");
        int customCount = 0;
        foreach (BroilerAppInfo app in launcherService.Apps)
        {
            AppCustomSettings custom = launcherService.Settings.GetOrCreate(app.Id);
            bool hasCustom = !string.IsNullOrEmpty(custom.ExecutablePath) || !string.IsNullOrEmpty(custom.Arguments) || !custom.UseAutoDetect;
            if (hasCustom)
            {
                customCount++;
                Console.WriteLine($"  {app.Title}:");
                Console.WriteLine($"    Executable:  {custom.ExecutablePath ?? "(auto)"}");
                Console.WriteLine($"    Arguments:   {custom.Arguments ?? "(none)"}");
                Console.WriteLine($"    Auto-Detect: {custom.UseAutoDetect}");
            }
        }

        if (customCount == 0)
        {
            Console.WriteLine("  (No customizations set; all applications use automatic detection)");
        }
    }
}
