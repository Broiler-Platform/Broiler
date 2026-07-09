using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.UI.Standard;

namespace Broiler.UI.Win32.Demo;

[SupportedOSPlatform("windows7.0")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        _ = SetProcessDpiAwarenessContext(new IntPtr(-4)); // PER_MONITOR_AWARE_V2, best effort.

        // Apply the palette before the control tree is built; standard controls
        // capture their role colors at construction. Choose via CLI flag
        // (--dark / --light / --high-contrast) or the BROILER_UI_THEME env var.
        StandardControlPaint.ApplyTheme(SelectTheme(args));

        try
        {
            using var window = new Win32DemoWindow();
            return window.Run();
        }
        catch (Exception ex)
        {
            MessageBox(IntPtr.Zero, ex.ToString(), "Broiler.UI Win32 Demo", MbIconError | MbOk);
            return 1;
        }
    }

    private static StandardThemeTokens SelectTheme(string[] args)
    {
        string? choice = null;
        foreach (string arg in args)
        {
            switch (arg.Trim().ToLowerInvariant())
            {
                case "--dark":
                    choice = "dark";
                    break;
                case "--light":
                    choice = "light";
                    break;
                case "--high-contrast":
                case "--hc":
                    choice = "high-contrast";
                    break;
            }
        }

        choice ??= Environment.GetEnvironmentVariable("BROILER_UI_THEME")?.Trim().ToLowerInvariant();

        return choice switch
        {
            "dark" => StandardThemeTokens.Dark,
            "high-contrast" or "hc" or "high-contrast-dark" => StandardThemeTokens.HighContrastDark,
            "high-contrast-light" => StandardThemeTokens.HighContrastLight,
            _ => StandardThemeTokens.Light,
        };
    }

    private const uint MbOk = 0x00000000;
    private const uint MbIconError = 0x00000010;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hwnd, string text, string caption, uint type);
}
