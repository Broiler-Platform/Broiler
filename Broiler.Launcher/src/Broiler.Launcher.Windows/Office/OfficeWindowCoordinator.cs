#if BROILER_OFFICE_ALL_IN_ONE
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Graphics.Windows;

namespace Broiler.Launcher.Windows.Office;

internal interface IOfficeUiThreadTarget
{
    bool PostUiAction(Action callback);
}

/// <summary>
/// Manages non-modal top-level Direct2D windows inside the all-in-one Broiler Office Windows process
/// without spawning child processes of <c>Broiler.Office.exe</c>.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal static class OfficeWindowCoordinator
{
    private sealed record TrackedWindow(Direct2DWindow Window, IOfficeUiThreadTarget? Target, IDisposable? Companion);

    private static readonly List<TrackedWindow> ActiveWindows = [];
    private static int _uiThreadId;
    private static bool _loopActive;

    public static int Run(Func<Direct2DWindow> createMainWindow, IDisposable? companion = null)
    {
        ArgumentNullException.ThrowIfNull(createMainWindow);
        _uiThreadId = Environment.CurrentManagedThreadId;
        _loopActive = true;
        try
        {
            Direct2DWindow mainWindow = createMainWindow();
            TrackAndShow(mainWindow, companion);

            while (true)
            {
                int result = GetMessage(out MSG message, 0, 0, 0);
                if (result == -1)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "GetMessage failed.");
                if (result == 0)
                    return unchecked((int)message.WParam);

                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }
        finally
        {
            _loopActive = false;
            DisposeTrackedWindows();
        }
    }

    public static void OpenNonModal(Func<(Direct2DWindow Window, IDisposable? Companion)> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!_loopActive)
            throw new InvalidOperationException("Broiler Office window coordinator is not active.");

        if (Environment.CurrentManagedThreadId == _uiThreadId)
        {
            OpenOnUiThread(factory);
            return;
        }

        foreach (TrackedWindow entry in ActiveWindows.ToArray())
        {
            if (entry.Target is { } target && target.PostUiAction(() => OpenOnUiThread(factory)))
                return;
        }

        throw new InvalidOperationException("No active Broiler Office window is available to open a window on the UI thread.");
    }

    private static void OpenOnUiThread(Func<(Direct2DWindow Window, IDisposable? Companion)> factory)
    {
        (Direct2DWindow window, IDisposable? companion) = factory();
        try
        {
            TrackAndShow(window, companion);
        }
        catch
        {
            window.Dispose();
            companion?.Dispose();
            throw;
        }
    }

    private static void TrackAndShow(Direct2DWindow window, IDisposable? companion)
    {
        var entry = new TrackedWindow(window, window as IOfficeUiThreadTarget, companion);
        ActiveWindows.Add(entry);
        window.Closed += (_, _) =>
        {
            ActiveWindows.Remove(entry);
            try { companion?.Dispose(); } catch { }
            try { window.Dispose(); } catch { }

            if (ActiveWindows.Count == 0 && _loopActive)
                PostQuitMessage(0);
        };
        window.Show();
    }

    private static void DisposeTrackedWindows()
    {
        foreach (TrackedWindow entry in ActiveWindows.ToArray())
        {
            ActiveWindows.Remove(entry);
            try { entry.Companion?.Dispose(); } catch { }
            try { entry.Window.Dispose(); } catch { }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int PtX;
        public int PtY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);
}
#endif
