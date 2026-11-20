using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Broiler.Documents;
using Broiler.Documents.Pdf;
using Broiler.Documents.Pdf.Fonts;
using Broiler.Documents.Pdf.Images;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Text;
using Broiler.Mail.Application;
using Broiler.Mail.Core.Services;
using Broiler.Mail.Infrastructure.Mail;
using Broiler.Mail.Infrastructure.Persistence;
using Broiler.Media;
using Broiler.Media.Image.Managed;
using Broiler.Plate;
using Broiler.Writer;

namespace Broiler.Launcher.Office;

/// <summary>
/// Shared composition helpers for the all-in-one Broiler Office executable across Windows and Linux.
/// </summary>
internal static class OfficeComposition
{
    private static int _codecsInitialized;

    public static string DefaultMailDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Broiler.Mail");

    public static void EnsureInitialized(bool installLinuxFontResolver)
    {
        if (System.Threading.Interlocked.Exchange(ref _codecsInitialized, 1) == 0)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Broiler.Mail.Windows.Preview.HtmlRuntime.Initialize();
            try
            {
                BImageCodecs.Use(new MediaCodecCatalog(ManagedImageCodecs.CreateCodecs()));
            }
            catch (InvalidOperationException)
            {
                // Codec catalog already registered.
            }

            if (installLinuxFontResolver)
            {
                BSystemFonts.InstallFontFileResolver();
            }
        }
    }

    public static WriterDocumentFormats CreateWriterDocumentFormats() =>
        WriterDocumentFormats.CreateDefault().With(
            new WriterDocumentFormat(
                new PdfDocumentCodec(CreateWriterPdfServices()),
                "PDF",
                WriterFormatCapabilities.Open)
            {
                Passwords = CreateWriterPdfPasswords(),
            });

    private static WriterPasswordSupport CreateWriterPdfPasswords() =>
        new(
            PdfDiagnosticCodes.EncryptionPasswordRequired,
            PdfDiagnosticCodes.EncryptionPasswordIncorrect,
            PdfDiagnosticCodes.EncryptionExtractionNotPermitted,
            static (options, password) =>
                new PdfReadOptions(options.Limits, resourcePolicy: options.ResourcePolicy)
                    .WithCredentials(PdfDecryptionCredentials.FromPassword(password)));

    private static PdfCodecServices CreateWriterPdfServices() =>
        PdfCodecServices.Base
            .WithStreamFilters(new JpegStreamFilter())
            .WithUriPolicy(new PdfUriPolicy(allowHttp: true, allowMailto: true))
            .WithFontProgramReader(new GraphicsFontProgramReader())
            .WithColorProfileReader(new IccColorProfileReader());

    public static PlateFileFormats CreatePlateFileFormats() =>
        PlateFileFormats.CreateDefault().With(
            new PlateDocumentFormat(
                new PdfDocumentCodec(CreatePlatePdfServices()),
                "PDF"));

    private static PdfCodecServices CreatePlatePdfServices() =>
        PdfCodecServices.Base.WithStreamFilters(new JpegStreamFilter());

    public static PlatePrintPermission GetPlatePdfPrintPermission(DocumentReadResult result)
    {
        if (result is not PdfReadResult { Encryption: { } encryption } || encryption.Access == PdfAccessLevel.Owner)
            return PlatePrintPermission.Allowed;

        if (!encryption.Permissions.HasFlag(PdfPermissions.Print))
            return PlatePrintPermission.Refused;

        return encryption.Permissions.HasFlag(PdfPermissions.PrintHighQuality)
            ? PlatePrintPermission.Allowed
            : PlatePrintPermission.LowResolution;
    }

    public static MailApplication CreateMailApplication(
        ICredentialStore credentials,
        string? dataDirectory = null,
        Action<string>? openProfileWindow = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        string resolvedDirectory = dataDirectory ?? DefaultMailDataDirectory;
        var profile = MailProfile.Open(resolvedDirectory);
        try
        {
            var cache = new SqliteMailCache(profile.CachePath, profile.Manifest.CacheMaximumBytes);
            cache.RepairAsync().GetAwaiter().GetResult();
            var accounts = new JsonAccountCollectionStore(profile.DirectoryPath);
            var storage = new ProfileStorageService(profile, cache);
            return new MailApplication(
                accounts,
                new JsonSettingsStore(profile.SettingsPath),
                new ImapMailReceiver(credentials, new MutationIntentJournal(profile.CachePath)),
                new SmtpMailSender(credentials),
                credentials,
                new JsonDraftStore(profile.DraftsPath),
                new ImapSentCopyWriter(credentials),
                new SmtpConnectionTester(credentials),
                cache,
                profile,
                storage,
                openProfileWindow ?? OpenMailProfileWindow,
                new AccountRemovalService(accounts, storage, credentials),
                new JsonRecipientBook(Path.Combine(profile.DirectoryPath, "recipients.json")),
                new JsonOfflineFolderStore(Path.Combine(profile.DirectoryPath, "offline-folders.json")),
                new JsonAccountNavigationStore(Path.Combine(profile.DirectoryPath, "account-navigation.json")),
                new FileMessageSourceExports(profile.DirectoryPath, storage));
        }
        catch
        {
            profile.Dispose();
            throw;
        }
    }

    private static void OpenMailProfileWindow(string directory)
    {
        string executable = Environment.ProcessPath
            ?? throw new IOException("The current Broiler Office executable path could not be resolved.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        start.ArgumentList.Add("--app");
        start.ArgumentList.Add("mail");
        start.ArgumentList.Add("--data-directory");
        start.ArgumentList.Add(directory);
        using var process = Process.Start(start) ?? throw new IOException("A new Mail window could not be started.");
    }
}
