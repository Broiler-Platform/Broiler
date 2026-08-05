using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Graphics;
using Broiler.UI;

namespace Broiler.Code.Windows;

/// <summary>
/// Native IME composition, over IMM32.
///
/// The editor cannot compose text correctly without this. An IME sends
/// composition state through window messages, and a host that only handles
/// WM_CHAR sees the committed result with no composition, no candidate
/// underline, and a candidate window parked at the top-left of the screen
/// instead of at the caret.
///
/// The caret rectangle matters more than it looks: <c>ImmSetCompositionWindow</c>
/// is what puts the candidate list under the text being typed. Without it a
/// Japanese or Chinese user reads their candidates in one corner while typing in
/// another.
/// </summary>
[SupportedOSPlatform("windows5.0")]
internal sealed partial class WindowsTextInputService(IntPtr window) : IDisposable
{
    private const int WmImeStartComposition = 0x010D;
    private const int WmImeComposition = 0x010F;
    private const int WmImeEndComposition = 0x010E;

    private const int GcsCompStr = 0x0008;
    private const int GcsResultStr = 0x0800;

    private const int CfsPoint = 0x0002;

    private bool _disposed;

    /// <summary>Composition text changed. Null text means the composition ended.</summary>
    public event Action<string?, bool>? CompositionChanged;

    /// <summary>
    /// Handles an IME window message. Returns true when the message was
    /// consumed, so the caller does not also let the default handler turn it
    /// into a duplicate WM_CHAR.
    /// </summary>
    public bool TryHandleMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WmImeStartComposition:
                CompositionChanged?.Invoke(string.Empty, false);
                return true;

            case WmImeComposition:
            {
                long flags = lParam.ToInt64();

                // The committed result arrives on the same message as the
                // in-progress composition, and must be handled first: the
                // composition string is empty by then, so reading it first
                // would report the commit as a cancellation.
                if ((flags & GcsResultStr) != 0 && TryRead(GcsResultStr, out string committed))
                {
                    CompositionChanged?.Invoke(committed, true);
                    return true;
                }

                if ((flags & GcsCompStr) != 0 && TryRead(GcsCompStr, out string composing))
                {
                    CompositionChanged?.Invoke(composing, false);
                    return true;
                }

                return false;
            }

            case WmImeEndComposition:
                CompositionChanged?.Invoke(null, false);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Places the candidate window at the caret, in client pixels.</summary>
    public void SetCaretRectangle(BRect caret)
    {
        IntPtr context = ImmGetContext(window);
        if (context == IntPtr.Zero)
            return;

        try
        {
            var form = new CompositionForm
            {
                Style = CfsPoint,
                CurrentPosition = new Point((int)caret.Left, (int)caret.Top),
                Area = new Rect((int)caret.Left, (int)caret.Top, (int)caret.Right, (int)caret.Bottom),
            };
            ImmSetCompositionWindow(context, ref form);
        }
        finally
        {
            ImmReleaseContext(window, context);
        }
    }

    public void Dispose() => _disposed = true;

    private bool TryRead(int index, out string text)
    {
        text = string.Empty;
        if (_disposed)
            return false;

        IntPtr context = ImmGetContext(window);
        if (context == IntPtr.Zero)
            return false;

        try
        {
            // Called twice by design: once for the byte length, once for the
            // data. The length is in bytes and the buffer is UTF-16.
            int bytes = ImmGetCompositionStringW(context, index, IntPtr.Zero, 0);
            if (bytes <= 0)
                return false;

            IntPtr buffer = Marshal.AllocHGlobal(bytes);
            try
            {
                if (ImmGetCompositionStringW(context, index, buffer, bytes) <= 0)
                    return false;
                text = Marshal.PtrToStringUni(buffer, bytes / sizeof(char));
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            ImmReleaseContext(window, context);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect(int left, int top, int right, int bottom)
    {
        public int Left = left;
        public int Top = top;
        public int Right = right;
        public int Bottom = bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionForm
    {
        public int Style;
        public Point CurrentPosition;
        public Rect Area;
    }

    [LibraryImport("imm32.dll")]
    private static partial IntPtr ImmGetContext(IntPtr window);

    [LibraryImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ImmReleaseContext(IntPtr window, IntPtr context);

    [LibraryImport("imm32.dll", EntryPoint = "ImmGetCompositionStringW")]
    private static partial int ImmGetCompositionStringW(
        IntPtr context, int index, IntPtr buffer, int bufferLength);

    [LibraryImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ImmSetCompositionWindow(IntPtr context, ref CompositionForm form);
}
