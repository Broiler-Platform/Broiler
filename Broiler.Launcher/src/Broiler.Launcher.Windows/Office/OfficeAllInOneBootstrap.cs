#if BROILER_OFFICE_ALL_IN_ONE
using System;
using System.Runtime.Versioning;
using Broiler.Launcher.Models;
using Broiler.Launcher.Office;
using Broiler.Launcher.Services;
using Broiler.UI.Standard;

namespace Broiler.Launcher.Windows.Office;

/// <summary>
/// Registers all built-in Broiler Office applications into <see cref="BuiltInAppRegistry"/> on Windows.
/// </summary>
[SupportedOSPlatform("windows7.0")]
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
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: false);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        if (options.ShowHelp)
        {
            Console.WriteLine("Broiler Mail (Broiler Office built-in) [--data-directory <path>]");
            return 0;
        }

        var (window, application) = CreateMailWindow(options, ownsMessageLoop: false);
        return OfficeWindowCoordinator.Run(() => window, application);
    }

    private static void OpenMail(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: false);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        OfficeWindowCoordinator.OpenNonModal(() =>
        {
            var (window, application) = CreateMailWindow(options, ownsMessageLoop: false);
            return (window, application);
        });
    }

    private static (OfficeMailWindow Window, IDisposable Companion) CreateMailWindow(OfficeAppOptions options, bool ownsMessageLoop)
    {
        StandardControlPaint.ApplyTheme(StandardThemeTokens.Light);
        var application = OfficeComposition.CreateMailApplication(
            new OfficeWindowsCredentialStore(),
            options.DataDirectory,
            directory => OpenMail(["--data-directory", directory]));
        try
        {
            application.InitializeAsync().GetAwaiter().GetResult();
            return (new OfficeMailWindow(application, ownsMessageLoop), application);
        }
        catch
        {
            application.Dispose();
            throw;
        }
    }

    private static int RunWriter(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: false);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        if (options.ShowHelp)
        {
            Console.WriteLine("Broiler Writer (Broiler Office built-in) [document-path]");
            return 0;
        }

        return OfficeWindowCoordinator.Run(() => new OfficeWriterWindow(
            OfficeComposition.CreateWriterDocumentFormats(),
            options.InitialPath,
            ownsMessageLoop: false));
    }

    private static void OpenWriter(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: false);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        OfficeWindowCoordinator.OpenNonModal(() => (new OfficeWriterWindow(
            OfficeComposition.CreateWriterDocumentFormats(),
            options.InitialPath,
            ownsMessageLoop: false), null));
    }

    private static int RunCells(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: false);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        if (options.ShowHelp)
        {
            Console.WriteLine("Broiler Cells (Broiler Office built-in)");
            return 0;
        }

        return OfficeWindowCoordinator.Run(() => new OfficeCellsWindow(ownsMessageLoop: false));
    }

    private static void OpenCells(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: false);
        _ = OfficeAppOptions.Parse(args);
        OfficeWindowCoordinator.OpenNonModal(() => (new OfficeCellsWindow(ownsMessageLoop: false), null));
    }

    private static int RunPlate(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: false);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        if (options.ShowHelp)
        {
            Console.WriteLine("Broiler Plate (Broiler Office built-in) [file-or-folder-path]");
            return 0;
        }

        return OfficeWindowCoordinator.Run(() => new OfficePlateWindow(
            OfficeComposition.CreatePlateFileFormats(),
            options.InitialPath,
            ownsMessageLoop: false));
    }

    private static void OpenPlate(string[] args)
    {
        OfficeComposition.EnsureInitialized(installLinuxFontResolver: false);
        OfficeAppOptions options = OfficeAppOptions.Parse(args);
        OfficeWindowCoordinator.OpenNonModal(() => (new OfficePlateWindow(
            OfficeComposition.CreatePlateFileFormats(),
            options.InitialPath,
            ownsMessageLoop: false), null));
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
