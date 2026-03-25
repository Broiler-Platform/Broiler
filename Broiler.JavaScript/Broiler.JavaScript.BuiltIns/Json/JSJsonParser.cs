using System;
using System.Text.Json;
using System.Text;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Null;
using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Number;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.BuiltIns.Json;

internal class JSJsonParser(JsonParserReceiver r) : System.Text.Json.Serialization.JsonConverter<JSValue>
{
    public static JSValue Parse(string str, JsonParserReceiver r) => JsonSerializer.Deserialize<JSValue>(str, new JsonSerializerOptions
    {
        Converters =
            {
                new JSJsonParser(r)
            }
    });

    /// <summary>
    /// Parse with source text tracking for the reviver context (ES2026 §4.7).
    /// </summary>
    public static JSValue ParseWithSource(string str, JsonParserReceiverWithSource r)
    {
        var parser = new JSJsonParser(null) { reviverWithSource = r };
        return JsonSerializer.Deserialize<JSValue>(str, new JsonSerializerOptions
        {
            Converters = { parser }
        });
    }

    private JsonParserReceiverWithSource reviverWithSource;

    public override JSValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.None => throw new NotSupportedException($"Unable to read JSON Data"),
            JsonTokenType.StartObject => ReadObject(ref reader, options),
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            JsonTokenType.String => new JSString(reader.GetString()),
            JsonTokenType.Number => new JSNumber(reader.GetDouble()),
            JsonTokenType.True => JSBoolean.True,
            JsonTokenType.False => JSBoolean.False,
            JsonTokenType.Null => JSNull.Value,
            _ => throw new NotSupportedException($"Unexpected JSON {reader.TokenType} at {reader.TokenStartIndex}"),
        };
    }

    /// <summary>
    /// Extracts the raw source text for a primitive token from the reader.
    /// </summary>
    private static string GetSourceText(ref Utf8JsonReader reader)
    {
        if (reader.HasValueSequence)
        {
            var sequence = reader.ValueSequence;
            return Encoding.UTF8.GetString(sequence.FirstSpan);
        }
        
        return Encoding.UTF8.GetString(reader.ValueSpan);
    }

    /// <summary>
    /// Reads a value and returns both the value and its source text for
    /// the reviver context.
    /// </summary>
    private (JSValue value, string source) ReadWithSource(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                return (ReadObject(ref reader, options), null);

            case JsonTokenType.StartArray:
                return (ReadArray(ref reader, options), null);

            case JsonTokenType.String:
                // Source includes the surrounding quotes
                var strSource = "\"" + GetSourceText(ref reader) + "\"";
                return (new JSString(reader.GetString()), strSource);

            case JsonTokenType.Number:
                var numSource = GetSourceText(ref reader);
                return (new JSNumber(reader.GetDouble()), numSource);

            case JsonTokenType.True:
                return (JSBoolean.True, "true");

            case JsonTokenType.False:
                return (JSBoolean.False, "false");

            case JsonTokenType.Null:
                return (JSNull.Value, "null");

            default:
                throw new NotSupportedException($"Unexpected JSON {reader.TokenType} at {reader.TokenStartIndex}");
        }
    }

    private JSValue ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var j = new JSArray();
        // read properties...
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reviverWithSource != null)
            {
                var (value, source) = ReadWithSource(ref reader, options);
                value = reviverWithSource((j.Length.ToString(), value, source));
                if (!value.IsUndefined)
                    j.Add(value);
            }
            else
            {
                j.Add(Read(ref reader, typeof(JSValue), options));
            }
        }

        return j;
    }

    private JSValue ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var j = new JSObject();

        // read properties...
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.EndObject:
                    return j;

                case JsonTokenType.PropertyName:
                    var name = reader.GetString();

                    if (!reader.Read())
                        throw new InvalidOperationException($"Unable to read JSON");

                    if (reviverWithSource != null)
                    {
                        var (value, source) = ReadWithSource(ref reader, options);
                        value = reviverWithSource((name, value, source));

                        if (value.IsUndefined)
                            continue;

                        j[name] = value;
                    }
                    else
                    {
                        var value = Read(ref reader, typeof(JSValue), options);
                        value = r?.Invoke((name, value)) ?? value;
                        if (value.IsUndefined)
                            continue;

                        j[name] = value;
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Invalid token {reader.TokenType} at {reader.TokenStartIndex}");
            }
        }

        return j;
    }

    public override void Write(Utf8JsonWriter writer, JSValue value, JsonSerializerOptions options) => throw new NotImplementedException();
}
