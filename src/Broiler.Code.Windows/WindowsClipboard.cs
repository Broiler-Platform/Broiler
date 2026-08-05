using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.UI;

namespace Broiler.Code.Windows;

/// <summary>
/// The Win32 clipboard.
///
/// There is deliberately no in-memory fallback. Writer's host keeps a private
/// string and serves it when no OS accessor is wired, which makes copy and
/// paste appear to work while silently not interoperating with anything else on
/// the machine — a user copies from the editor, pastes into a browser, and gets
/// their previous clipboard contents. This reports failure instead, and the
/// caller shows the command as unavailable.
/// </summary>
[SupportedOSPlatform("windows5.0")]
internal sealed partial class WindowsClipboard(IntPtr ownerWindow) : IUiClipboardHost
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    public bool TryGetText(out string text)
    {
        text = string.Empty;
        if (!IsClipboardFormatAvailable(CfUnicodeText))
            return false;

        // The clipboard is a shared, single-owner resource: another process can
        // hold it, so opening is allowed to fail and the caller is told rather
        // than being handed something stale.
        if (!OpenClipboard(ownerWindow))
            return false;

        try
        {
            IntPtr handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
                return false;

            IntPtr pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
                return false;

            try
            {
                text = Marshal.PtrToStringUni(pointer) ?? string.Empty;
                return text.Length > 0;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!OpenClipboard(ownerWindow))
            return;

        try
        {
            EmptyClipboard();

            // The clipboard takes ownership of the moveable block on success,
            // so it must not be freed here — and must be freed if
            // SetClipboardData fails, or the process leaks it on every copy.
            int bytes = (text.Length + 1) * sizeof(char);
            IntPtr block = GlobalAlloc(GmemMoveable, (UIntPtr)bytes);
            if (block == IntPtr.Zero)
                return;

            IntPtr pointer = GlobalLock(block);
            if (pointer == IntPtr.Zero)
            {
                GlobalFree(block);
                return;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                Marshal.WriteInt16(pointer, text.Length * sizeof(char), 0);
            }
            finally
            {
                GlobalUnlock(block);
            }

            if (SetClipboardData(CfUnicodeText, block) == IntPtr.Zero)
                GlobalFree(block);
        }
        finally
        {
            CloseClipboard();
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(IntPtr owner);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsClipboardFormatAvailable(uint format);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr GetClipboardData(uint format);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr SetClipboardData(uint format, IntPtr data);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalFree(IntPtr handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalLock(IntPtr handle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(IntPtr handle);
}
