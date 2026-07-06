using System;

namespace Broiler.Documents;

/// <summary>
/// A single note produced while reading or writing a document — an unsupported
/// construct that was skipped, a limit that clamped, or a recovered error.
/// Diagnostics carry a stable <see cref="Code"/> and a human-readable
/// <see cref="Message"/>, but never the document text or any payload (ADR 0004
/// privacy rule).
/// </summary>
public sealed class DocumentDiagnostic
{
    public DocumentDiagnostic(DocumentDiagnosticSeverity severity, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("A diagnostic needs a stable code.", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A diagnostic needs a message.", nameof(message));

        Severity = severity;
        Code = code;
        Message = message;
    }

    public DocumentDiagnosticSeverity Severity { get; }

    public string Code { get; }

    public string Message { get; }

    public static DocumentDiagnostic Info(string code, string message) =>
        new(DocumentDiagnosticSeverity.Info, code, message);

    public static DocumentDiagnostic Warning(string code, string message) =>
        new(DocumentDiagnosticSeverity.Warning, code, message);

    public static DocumentDiagnostic Error(string code, string message) =>
        new(DocumentDiagnosticSeverity.Error, code, message);

    public override string ToString() => $"{Severity} {Code}: {Message}";
}
