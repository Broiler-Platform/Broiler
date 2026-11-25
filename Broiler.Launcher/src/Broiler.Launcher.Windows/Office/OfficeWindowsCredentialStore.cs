#if BROILER_OFFICE_ALL_IN_ONE
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Mail.Core.Accounts;
using Broiler.Mail.Core.Services;
using Broiler.Native.Windows.Credentials;

namespace Broiler.Launcher.Windows.Office;

/// <summary>
/// Windows Credential Manager backed credential store for Broiler Mail in the all-in-one executable.
/// </summary>
[SupportedOSPlatform("windows7.0")]
internal sealed class OfficeWindowsCredentialStore : ICredentialStore
{
    public Task<string?> ReadAsync(CredentialKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CredentialNative.CredRead(Target(key), CredentialNative.CredTypeGeneric, 0, out nint pointer))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == CredentialNative.ErrorNotFound) return null;
                throw Failure("read", error);
            }

            CredentialNative.CREDENTIAL credential = default;
            try
            {
                credential = Marshal.PtrToStructure<CredentialNative.CREDENTIAL>(pointer);
                if (credential.UserName != key.Binding) return null;
                if (credential.CredentialBlobSize > CredentialNative.CredMaxCredentialBlobSize || credential.CredentialBlobSize % 2 != 0 || credential.CredentialBlob == 0)
                    throw new InvalidDataException("The saved credential is invalid. Save the password again.");
                return Marshal.PtrToStringUni(credential.CredentialBlob, checked((int)credential.CredentialBlobSize / 2));
            }
            finally
            {
                if (credential.CredentialBlob != 0 && credential.CredentialBlobSize <= CredentialNative.CredMaxCredentialBlobSize)
                    Marshal.Copy(new byte[credential.CredentialBlobSize], 0, credential.CredentialBlob, (int)credential.CredentialBlobSize);
                CredentialNative.CredFree(pointer);
            }
        }, cancellationToken);
    }

    public Task WriteAsync(CredentialKey key, string secret, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length is 0 or > CredentialNative.CredMaxCredentialBlobSize / 2 || secret.Contains('\0'))
            throw new ArgumentException("Enter a password of 1–1280 characters without null characters.");

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            nint blob = Marshal.StringToCoTaskMemUni(secret);
            try
            {
                var credential = new CredentialNative.CREDENTIAL
                {
                    Type = CredentialNative.CredTypeGeneric,
                    TargetName = Target(key),
                    UserName = key.Binding,
                    CredentialBlob = blob,
                    CredentialBlobSize = checked((uint)secret.Length * 2),
                    Persist = CredentialNative.CredPersistLocalMachine,
                };
                if (!CredentialNative.CredWrite(ref credential, 0))
                    throw Failure("save", Marshal.GetLastWin32Error());
            }
            finally
            {
                Marshal.ZeroFreeCoTaskMemUnicode(blob);
            }
        }, cancellationToken);
    }

    public Task DeleteAsync(CredentialKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CredentialNative.CredDelete(Target(key), CredentialNative.CredTypeGeneric, 0))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != CredentialNative.ErrorNotFound)
                    throw Failure("delete", error);
            }
        }, cancellationToken);
    }

    private static string Target(CredentialKey key) => $"Broiler.Mail/{key.AccountId}/{key.Protocol}";

    private static IOException Failure(string operation, int error) =>
        new($"Windows Credential Manager could not {operation} the password (error {error}).");
}
#endif
