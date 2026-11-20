#if BROILER_OFFICE_ALL_IN_ONE
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Mail.Core.Accounts;
using Broiler.Mail.Core.Services;
using Broiler.Mail.Infrastructure.Persistence;

namespace Broiler.Launcher.Linux.Office;

/// <summary>
/// AES-GCM encrypted file-backed credential store for Broiler Mail on Linux.
/// </summary>
internal sealed class OfficeLinuxCredentialStore(string directory) : ICredentialStore
{
    private readonly EncryptedCredentialStore _store = new(directory, new FileKeyAesGcmCipher(Path.Combine(directory, ".master.key")));

    public Task<string?> ReadAsync(CredentialKey key, CancellationToken cancellationToken = default) =>
        _store.ReadAsync(key, cancellationToken);

    public Task WriteAsync(CredentialKey key, string secret, CancellationToken cancellationToken = default) =>
        _store.WriteAsync(key, secret, cancellationToken);

    public Task DeleteAsync(CredentialKey key, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(key, cancellationToken);

    private sealed class FileKeyAesGcmCipher(string keyPath) : ICredentialCipher
    {
        private const int NonceBytes = 12;
        private const int TagBytes = 16;
        private const int KeyBytes = 32;
        private readonly object _gate = new();

        public byte[] Encrypt(byte[] plaintext, byte[] associatedData)
        {
            byte[] key = GetOrCreateKey();
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceBytes);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagBytes];
            using (var aes = new AesGcm(key, TagBytes))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
            }

            byte[] result = new byte[NonceBytes + TagBytes + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceBytes);
            Buffer.BlockCopy(tag, 0, result, NonceBytes, TagBytes);
            Buffer.BlockCopy(ciphertext, 0, result, NonceBytes + TagBytes, ciphertext.Length);
            return result;
        }

        public byte[] Decrypt(byte[] payload, byte[] associatedData)
        {
            if (payload.Length < NonceBytes + TagBytes)
                throw new InvalidDataException("The saved password is invalid. Save it again.");

            byte[] key = ReadExistingKey();
            ReadOnlySpan<byte> span = payload;
            ReadOnlySpan<byte> nonce = span[..NonceBytes];
            ReadOnlySpan<byte> tag = span.Slice(NonceBytes, TagBytes);
            ReadOnlySpan<byte> ciphertext = span[(NonceBytes + TagBytes)..];
            byte[] plaintext = new byte[ciphertext.Length];
            try
            {
                using var aes = new AesGcm(key, TagBytes);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
                return plaintext;
            }
            catch (CryptographicException ex)
            {
                throw new InvalidDataException("The saved password could not be unlocked. Save it again.", ex);
            }
        }

        private byte[] GetOrCreateKey()
        {
            lock (_gate)
            {
                if (File.Exists(keyPath))
                    return ReadExistingKey();

                string? parent = Path.GetDirectoryName(keyPath);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                byte[] key = RandomNumberGenerator.GetBytes(KeyBytes);
                string temp = keyPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(temp, key);
                if (OperatingSystem.IsLinux())
                    File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(temp, keyPath, overwrite: true);
                return key;
            }
        }

        private byte[] ReadExistingKey()
        {
            if (!File.Exists(keyPath))
                throw new IOException("The credential encryption key is unavailable.");
            byte[] key = File.ReadAllBytes(keyPath);
            if (key.Length != KeyBytes)
                throw new InvalidDataException("The credential encryption key is invalid.");
            return key;
        }
    }
}
#endif
