using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.Versioning;

namespace Broiler.App.Graphics;

/// <summary>
/// Entry point for the Broiler browser built on Broiler.Graphics (Win32 + Direct2D).
/// This is the preview shell that replaces the WPF host (<c>Broiler.App</c>).
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // The HTML stack pulls in two physically distinct builds of the same-identity
        // assemblies (e.g. Broiler.Dom is checked out under both Broiler.DOM and the
        // CSS submodule). MSBuild dedups them and drops their runtime entry from deps.json,
        // so the host never probes for them even though the DLLs are in the output folder.
        // Fall back to loading any such assembly directly from the application directory.
        AssemblyLoadContext.Default.Resolving += ResolveFromAppDirectory;

        if (!ConfirmPreviewSafetyNotice())
            return 0;

        _ = SetProcessDpiAwarenessContext(new IntPtr(-4)); // PER_MONITOR_AWARE_V2, best effort.

        bool runUiPhase3Preview = args.Length > 0 && string.Equals(args[0], "--ui-phase3", StringComparison.OrdinalIgnoreCase);
        bool runUiPhase4ToolbarPreview = args.Length > 0 && string.Equals(args[0], "--ui-phase4-toolbar", StringComparison.OrdinalIgnoreCase);
        string? initialUrl = !runUiPhase3Preview && !runUiPhase4ToolbarPreview && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0] : null;

        try
        {
            if (runUiPhase3Preview)
            {
                using var previewWindow = new Phase3UiPreviewWindow();
                return previewWindow.Run();
            }

            if (runUiPhase4ToolbarPreview)
            {
                using var toolbarWindow = new Phase4ToolbarPreviewWindow();
                return toolbarWindow.Run();
            }

            using var window = new BrowserWindow(initialUrl);
            return window.Run();
        }
        catch (Exception ex)
        {
            MessageBox(IntPtr.Zero, ex.ToString(), "Broiler", MbIconError | MbOk);
            return 1;
        }
    }

    private static Assembly? ResolveFromAppDirectory(AssemblyLoadContext context, AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name))
            return null;

        string candidate = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
        return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
    }

    private static bool ConfirmPreviewSafetyNotice()
    {
        const string message =
            "Broiler is an early preview; some human review records are still pending.\n\n" +
            "Risks: HTML/CSS/JS, images, downloads, network/file access, and Windows interop are not security-hardened. JavaScript is not a sandbox.\n\n" +
            "Recommendation: use controlled content only. Test unknown content in a VM or sandbox, restrict host, file, and network access, and use resource limits.\n\n" +
            "Choose OK to continue or Cancel to exit.";

        return MessageBox(IntPtr.Zero, message, "Broiler Preview - Safety Notice", MbIconWarning | MbOkCancel) == IdOk;
    }

    private const uint MbOk = 0x00000000;
    private const uint MbOkCancel = 0x00000001;
    private const uint MbIconError = 0x00000010;
    private const uint MbIconWarning = 0x00000030;
    private const int IdOk = 1;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hwnd, string text, string caption, uint type);
}
