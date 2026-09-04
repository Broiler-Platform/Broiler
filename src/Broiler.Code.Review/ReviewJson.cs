using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Broiler.Code.Review;

/// <summary>
/// Reads and writes the on-disk form of a <see cref="FileReview"/>.
///
/// These files are committed, so the format is treated as a wire format rather
/// than as whatever a serializer happens to emit for the current shape of the
/// model. Three properties are required of it, and none of them come for free
/// from reflection-based serialization:
///
/// * <b>A rename of a C# property must not change the file.</b> The names below
///   are written out by hand for that reason.
/// * <b>Writing an unchanged record must produce byte-identical output.</b>
///   Otherwise opening a file in the editor dirties its review record, and a
///   reviewer's commits fill with diffs they did not make. Ordering is fixed and
///   nothing derived from a clock is written except the timestamps a reviewer
///   actually caused.
/// * <b>A record must be readable in a pull-request diff.</b> That is, after
///   all, the entire point of committing it.
/// </summary>
public static class ReviewJson
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,

        // The default encoder escapes non-ASCII, which would turn a German or
        // Japanese review note into a wall of \uXXXX in the diff a reviewer is
        // supposed to read.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The timestamp shape: UTC, seconds, no offset, so records sort as text.</summary>
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>Serializes a record. The result ends with a newline, as a text file should.</summary>
    public static string Write(FileReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();

            // The record's own version, not this build's. A record written by a
            // newer build carries a higher number, and stamping it down to
            // CurrentVersion here would claim this build understood a format it
            // does not — the one lie the version field exists to prevent.
            writer.WriteNumber("version", Math.Max(review.Version, FileReview.CurrentVersion));
            writer.WriteString("file", review.Path);
            writer.WriteString("status", ToWire(review.Status));

            if (review.Reviewer.Length > 0)
                writer.WriteString("reviewer", review.Reviewer);
            if (review.ReviewedAt is { } at)
                writer.WriteString("reviewedAt", Stamp(at));
            if (review.ReviewedContentHash is { Length: > 0 } hash)
                writer.WriteString("reviewedContentHash", hash);
            if (review.ReviewedRevision is { Length: > 0 } revision)
                writer.WriteString("reviewedRevision", revision);

            if (review.Notes.Count > 0)
            {
                writer.WriteStartArray("notes");
                foreach (ReviewNote note in review.Notes)
                    WriteNote(writer, note);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    /// <summary>
    /// Parses a record. Returns null when the text is not a review record at all;
    /// a record that parses but carries values this build does not recognize is
    /// returned with those values defaulted, because dropping the file would
    /// destroy a reviewer's work over a spelling.
    /// </summary>
    public static FileReview? Read(string json, string path)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(path);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (root is not JsonObject document)
            return null;

        var notes = new List<ReviewNote>();
        if (document["notes"] is JsonArray array)
        {
            // The ids already spelled out in the file are collected first, so an
            // id minted for a note that has none cannot land on one of them.
            // Hand-authored records are a supported input, and a record mixing
            // explicit and missing ids would otherwise produce two notes sharing
            // an id — which RemoveNote resolves by deleting both.
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonNode? element in array)
            {
                if (element is JsonObject note && GetString(note, "id") is { Length: > 0 } id)
                    taken.Add(id);
            }

            int next = 1;
            foreach (JsonNode? element in array)
            {
                if (element is not JsonObject note)
                    continue;

                string? id = GetString(note, "id");
                if (id is not { Length: > 0 })
                {
                    while (!taken.Add(id = string.Create(CultureInfo.InvariantCulture, $"n{next}")))
                        next++;
                }

                if (ReadNote(note, id) is { } parsed)
                    notes.Add(parsed);
            }
        }

        return new FileReview
        {
            // The path inside the file is informational; the caller knows where
            // it read the record from and that is authoritative. A record copied
            // to a new path would otherwise claim to describe the old one.
            Path = path,
            Version = GetInt(document, "version") ?? FileReview.CurrentVersion,
            Status = FromWire(GetString(document, "status")),
            Reviewer = GetString(document, "reviewer") ?? string.Empty,
            ReviewedAt = GetTimestamp(document, "reviewedAt"),
            ReviewedContentHash = GetString(document, "reviewedContentHash"),
            ReviewedRevision = GetString(document, "reviewedRevision"),
            Notes = notes,
        };
    }

    private static string Stamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static void WriteNote(Utf8JsonWriter writer, ReviewNote note)
    {
        writer.WriteStartObject();
        writer.WriteString("id", note.Id);
        writer.WriteString("kind", ToWire(note.Kind));
        writer.WriteString("author", note.Author);
        writer.WriteString("createdAt", Stamp(note.CreatedAt));
        writer.WriteString("text", note.Text);

        if (!note.Anchor.IsFileLevel)
        {
            writer.WriteStartObject("anchor");

            // One-based on disk and zero-based in the model. The file is read by
            // people next to an editor's gutter; the model is indexed against a
            // text snapshot. The boundary is the only place the two conventions
            // meet, and it is converted in exactly these two methods.
            writer.WriteNumber("startLine", note.Anchor.StartLine + 1);
            writer.WriteNumber("endLine", note.Anchor.EndLine + 1);
            if (note.Anchor.Symbol is { Length: > 0 } symbol)
                writer.WriteString("symbol", symbol);
            writer.WriteString("text", note.Anchor.AnchorText);
            writer.WriteEndObject();
        }

        if (note.Resolution is { } resolution)
        {
            writer.WriteStartObject("resolution");
            writer.WriteString("resolvedAt", Stamp(resolution.ResolvedAt));
            writer.WriteString("resolvedBy", resolution.ResolvedBy);
            writer.WriteString("text", resolution.Text);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static ReviewNote? ReadNote(JsonObject note, string id)
    {
        string? text = GetString(note, "text");
        if (text is null)
            return null;

        ReviewAnchor anchor = ReviewAnchor.File;
        if (note["anchor"] is JsonObject anchorNode)
        {
            int start = Math.Max(0, (GetInt(anchorNode, "startLine") ?? 1) - 1);
            int end = Math.Max(start, (GetInt(anchorNode, "endLine") ?? (start + 1)) - 1);
            anchor = new ReviewAnchor(
                start, end, GetString(anchorNode, "text") ?? string.Empty, GetString(anchorNode, "symbol"));
        }

        ReviewResolution? resolution = null;
        if (note["resolution"] is JsonObject resolved)
        {
            resolution = new ReviewResolution(
                GetTimestamp(resolved, "resolvedAt") ?? default,
                GetString(resolved, "resolvedBy") ?? string.Empty,
                GetString(resolved, "text") ?? string.Empty);
        }

        return new ReviewNote
        {
            // Supplied by the caller, which has already made sure a note written
            // without one gets an id no other note in this record uses.
            Id = id,
            Kind = FromWireKind(GetString(note, "kind")),
            Author = GetString(note, "author") ?? string.Empty,
            CreatedAt = GetTimestamp(note, "createdAt") ?? default,
            Text = text,
            Anchor = anchor,
            Resolution = resolution,
        };
    }

    private static string ToWire(ReviewStatus status) => status switch
    {
        ReviewStatus.InReview => "in-review",
        ReviewStatus.Reviewed => "reviewed",
        ReviewStatus.Question => "question",
        ReviewStatus.NeedsChange => "needs-change",
        _ => "unreviewed",
    };

    private static ReviewStatus FromWire(string? wire) => wire switch
    {
        "in-review" => ReviewStatus.InReview,
        "reviewed" => ReviewStatus.Reviewed,
        "question" => ReviewStatus.Question,
        "needs-change" => ReviewStatus.NeedsChange,
        _ => ReviewStatus.Unreviewed,
    };

    private static string ToWire(ReviewNoteKind kind) => kind switch
    {
        ReviewNoteKind.Concern => "concern",
        ReviewNoteKind.Todo => "todo",
        ReviewNoteKind.Observation => "observation",
        _ => "question",
    };

    private static ReviewNoteKind FromWireKind(string? wire) => wire switch
    {
        "concern" => ReviewNoteKind.Concern,
        "todo" => ReviewNoteKind.Todo,
        "observation" => ReviewNoteKind.Observation,
        _ => ReviewNoteKind.Question,
    };

    private static string? GetString(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static int? GetInt(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue(out int number) ? number : null;

    private static DateTimeOffset? GetTimestamp(JsonObject node, string name) =>
        GetString(node, name) is { } text &&
        DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset value)
            ? value
            : null;
}
