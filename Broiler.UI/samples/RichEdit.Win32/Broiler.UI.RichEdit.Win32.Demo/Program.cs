using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Broiler.UI.RichEdit.Win32.Demo;

[SupportedOSPlatform("windows7.0")]
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        _ = SetProcessDpiAwarenessContext(new IntPtr(-4)); // PER_MONITOR_AWARE_V2, best effort.

        try
        {
            using var window = new RichEditDemoWindow();
            return window.Run();
        }
        catch (Exception ex)
        {
            MessageBox(IntPtr.Zero, ex.ToString(), "Broiler.UI RichEdit Win32 Demo", MbIconError | MbOk);
            return 1;
        }
    }

    private const uint MbOk = 0x00000000;
    private const uint MbIconError = 0x00000010;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hwnd, string text, string caption, uint type);
}
