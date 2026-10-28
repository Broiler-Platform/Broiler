using System.Text;

namespace Broiler.Launcher.Services;

/// <summary>Portable double-quote/backslash grammar for legacy settings; no shell expansion.</summary>
public static class LaunchArguments
{
    public static IReadOnlyList<string> Parse(string? text)
    {
        var result = new List<string>();
        if (text is null) return result;
        var token = new StringBuilder();
        bool quoted = false, started = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\')
            {
                int count = 1;
                while (i + 1 < text.Length && text[i + 1] == '\\') { count++; i++; }
                if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    token.Append('\\', count / 2);
                    i++;
                    if (count % 2 != 0) token.Append('"'); else quoted = !quoted;
                }
                else token.Append('\\', count);
                started = true;
            }
            else if (c == '"') { quoted = !quoted; started = true; }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (started) { result.Add(token.ToString()); token.Clear(); started = false; }
            }
            else { token.Append(c); started = true; }
        }
        if (quoted) throw new FormatException("Unclosed double quote in launch arguments.");
        if (started) result.Add(token.ToString());
        return result;
    }

    public static string Format(IEnumerable<string> arguments) => string.Join(" ", arguments.Select(Quote));

    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            result.Append(c);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}
