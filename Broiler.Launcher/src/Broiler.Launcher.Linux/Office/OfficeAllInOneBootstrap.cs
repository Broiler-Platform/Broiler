#if BROILER_OFFICE_ALL_IN_ONE
using System;
using Broiler.Launcher.Models;
using Broiler.Launcher.Office;
using Broiler.Launcher.Services;

namespace Broiler.Launcher.Linux.Office;

/// <summary>
/// Registers all built-in Broiler Office applications into <see cref="BuiltInAppRegistry"/> on Linux.
/// </summary>
internal static class OfficeAllInOneBootstrap
{
    public static void Register()
    {
        BuiltInAppRegistry.Register(BroilerAppId.Mail, RunMail, OpenMail);
        BuiltInAppRegistry.Register(BroilerAppId.Writer, RunWriter, OpenWriter);
        BuiltInAppRegistry.Register(BroilerAppId.Cell, RunCells, OpenCells);
        BuiltInAppRegistry.Register(BroilerAppId.Plate, RunPlate, OpenPlate);
#if BROILER_INCLUDE_BROWSER
        BuiltInAppRegistry.Register(BroilerAppId.Browser, RunBrowser);
#endif
    }

    private static int RunMail(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: true);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        if (options.ShowHelp)
        {
            Console.WriteLine("Broiler Mail (Broiler Office built-in) [--offscreen] [--window] [--width <px>] [--height <px>] [--data-directory <path>]");
            return 0;
        }

        return OfficeLinuxHostRunner.RunMail(options);
    }

    private static void OpenMail(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: true);
        OfficeLinuxHostRunner.OpenMail(OfficeAppOptions.Parse(args));
    }

    private static int RunWriter(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: true);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        if (options.ShowHelp)
        {
            Console.WriteLine("Broiler Writer (Broiler Office built-in) [--offscreen] [--window] [--width <px>] [--height <px>] [document-path]");
            return 0;
        }

        return OfficeLinuxHostRunner.RunWriter(options);
    }

    private static void OpenWriter(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: true);
        OfficeLinuxHostRunner.OpenWriter(OfficeAppOptions.Parse(args));
    }

    private static int RunCells(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: true);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        if (options.ShowHelp)
        {
            Console.WriteLine("Broiler Cells (Broiler Office built-in) [--offscreen] [--window] [--width <px>] [--height <px>]");
            return 0;
        }

        return OfficeLinuxHostRunner.RunCells(options);
    }

    private static void OpenCells(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: true);
        OfficeLinuxHostRunner.OpenCells(OfficeAppOptions.Parse(args));
    }

    private static int RunPlate(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: true);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        if (options.ShowHelp)
        {
            Console.WriteLine("Broiler Plate (Broiler Office built-in) [--offscreen] [--window] [--width <px>] [--height <px>] [file-or-folder-path]");
            return 0;
        }

        return OfficeLinuxHostRunner.RunPlate(options);
    }

    private static void OpenPlate(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: true);
        OfficeLinuxHostRunner.OpenPlate(OfficeAppOptions.Parse(args));
    }

#if BROILER_INCLUDE_BROWSER
    private static int RunBrowser(string[] args)
    {
        Console.Error.WriteLine("Broiler Browser integration is not enabled for this release.");
        return 1;
    }
#endif
}
#endif
