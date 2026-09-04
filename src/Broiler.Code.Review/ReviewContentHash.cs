using System;
using System.Security.Cryptography;
using System.Text;

namespace Broiler.Code.Review;

/// <summary>
/// The content identity a review is recorded against.
///
/// Staleness is decided here rather than from git, and that choice is what makes
/// the record survive ordinary repository work. A commit-based rule calls a
/// review stale after a rebase, a cherry-pick, a squash, or a branch switch, none
/// of which changed a line the reviewer read; and it calls a review current after
/// a revert-and-reapply, which did. Comparing content answers the question the
/// reviewer was actually asked — "is this the code I read?" — in every one of
/// those cases.
///
/// Two normalizations are applied before hashing, and both exist because the
/// alternative produces false staleness on files nobody edited:
///
/// * <b>Line endings.</b> This repository holds CRLF files, LF files, and files
///   that are mixed within themselves, and an editor that rewrites one whole
///   file normalizes them all. That rewrite changes every byte and not one
///   token. Hashing after normalizing to LF means a review survives it.
/// * <b>The byte-order mark.</b> Adding or dropping a BOM is a save-time
///   decision of whichever tool wrote the file last, and is not a change to the
///   code.
///
/// What is deliberately <i>not</i> normalized is whitespace and case. Trailing
/// whitespace can end a raw string literal, and indentation is meaning in more
/// than one language this store is expected to cover; a reviewer who reads
/// re-indented code is reading different code.
/// </summary>
public static class ReviewContentHash
{
    /// <summary>
    /// The prefix the algorithm is recorded under. Written into every record so
    /// that a later change of algorithm is a visible mismatch rather than a
    /// silent one: a record whose prefix is not understood is reported as
    /// <see cref="ReviewFreshness.Unknown"/>, never as current.
    /// </summary>
    public const string Algorithm = "sha256-nlf";

    /// <summary>
    /// U+FEFF, written as a code point rather than as a character literal.
    ///
    /// A literal byte-order mark here would be an invisible character in the
    /// source of the one class whose job is byte-level fidelity, and any tool
    /// that strips byte-order marks from source files would silently turn the
    /// comparison below into one against a character that is no longer there.
    /// </summary>
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>
    /// Hashes text the way a review records it. The result is
    /// <c>sha256-nlf:</c> followed by lowercase hex.
    /// </summary>
    public static string Compute(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        byte[] bytes = Encoding.UTF8.GetBytes(Normalize(text));
        return string.Concat(Algorithm, ":", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    /// <summary>
    /// True when <paramref name="text"/> is the content <paramref name="recorded"/>
    /// stands for.
    ///
    /// A recorded hash in an algorithm this build does not know returns false,
    /// which the evaluator turns into "unknown" rather than "stale". The
    /// difference matters: telling a reviewer their approval expired because the
    /// tool was upgraded would train them to ignore the warning.
    /// </summary>
    public static bool Matches(string? recorded, string text)
    {
        if (string.IsNullOrEmpty(recorded) || !IsKnownAlgorithm(recorded))
            return false;

        return string.Equals(recorded, Compute(text), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether a recorded hash was produced by an algorithm this build can verify.</summary>
    public static bool IsKnownAlgorithm(string? recorded) =>
        recorded is not null && recorded.StartsWith(Algorithm + ":", StringComparison.Ordinal);

    /// <summary>
    /// Line endings to LF and no leading byte-order mark. Exposed because note
    /// anchoring compares source lines and has to agree with the hash about what
    /// the content is; two normalizations that drift apart would re-anchor notes
    /// onto lines the hash considers unchanged.
    /// </summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > 0 && text[0] == ByteOrderMark)
            text = text[1..];

        // Nothing to rewrite is the common case on Linux and in git-normalized
        // trees, so it is checked before allocating a builder.
        if (text.IndexOf('\r') < 0)
            return text;

        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c != '\r')
            {
                builder.Append(c);
                continue;
            }

            // A lone CR is a line ending too — old Mac files and, more often
            // here, a file that was half-converted by a tool that stopped early.
            builder.Append('\n');
            if (i + 1 < text.Length && text[i + 1] == '\n')
                i++;
        }

        return builder.ToString();
    }
}
