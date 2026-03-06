using System.Text.RegularExpressions;

namespace Broiler.DevSite.Services;

public record ApiTypeDoc(string TypeName, string? Summary, List<ApiMemberDoc> Members);

public record ApiMemberDoc(string Name, string Signature, string? Summary, string MemberKind);

/// <summary>
/// Extracts API documentation from the <c>DomBridge.cs</c> source file using regex-based
/// parsing of XML doc comments and public member declarations.
/// Register as a singleton via <c>builder.Services.AddSingleton&lt;ApiDocService&gt;()</c>.
/// </summary>
public sealed partial class ApiDocService
{
    private readonly string _sourceFilePath;
    private readonly Lazy<List<ApiTypeDoc>> _docs;

    public ApiDocService(string contentRootPath)
    {
        _sourceFilePath = Path.GetFullPath(
            Path.Combine(contentRootPath, "..", "Broiler.App", "Rendering", "DomBridge.cs"));
        _docs = new Lazy<List<ApiTypeDoc>>(ParseApiDocs);
    }

    public List<ApiTypeDoc> GetApiDocs() => _docs.Value;

    private List<ApiTypeDoc> ParseApiDocs()
    {
        if (!File.Exists(_sourceFilePath))
            return [];

        var lines = File.ReadAllLines(_sourceFilePath);
        var types = new List<ApiTypeDoc>();

        int i = 0;
        while (i < lines.Length)
        {
            // Look for a public class declaration
            var classMatch = ClassRegex().Match(lines[i]);
            if (classMatch.Success)
            {
                string typeName = classMatch.Groups[1].Value;
                string? classSummary = ExtractPrecedingSummary(lines, i);
                var members = ParseMembers(lines, ref i);
                types.Add(new ApiTypeDoc(typeName, classSummary, members));
                continue;
            }

            i++;
        }

        return types;
    }

    private static List<ApiMemberDoc> ParseMembers(string[] lines, ref int index)
    {
        var members = new List<ApiMemberDoc>();

        // Move past the class declaration line(s)
        index++;

        int braceDepth = 0;
        bool enteredBody = false;

        while (index < lines.Length)
        {
            string line = lines[index];

            // Track brace depth to know when we leave the class body
            foreach (char ch in line)
            {
                if (ch == '{') { braceDepth++; enteredBody = true; }
                else if (ch == '}') braceDepth--;
            }

            if (enteredBody && braceDepth <= 0)
            {
                index++;
                break;
            }

            // Only look at top-level members (depth == 1)
            if (braceDepth == 1)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("public ", StringComparison.Ordinal)
                    && !trimmed.Contains(" class ", StringComparison.Ordinal))
                {
                    string? summary = ExtractPrecedingSummary(lines, index);
                    string memberKind = DetermineMemberKind(trimmed);
                    string name = ExtractMemberName(trimmed, memberKind);
                    members.Add(new ApiMemberDoc(name, trimmed, summary, memberKind));
                }
            }

            index++;
        }

        return members;
    }

    private static string? ExtractPrecedingSummary(string[] lines, int declarationIndex)
    {
        // Walk backwards from the declaration to collect /// comment lines
        var commentLines = new List<string>();
        for (int j = declarationIndex - 1; j >= 0; j--)
        {
            string trimmed = lines[j].Trim();
            if (trimmed.StartsWith("///", StringComparison.Ordinal))
            {
                commentLines.Insert(0, trimmed);
            }
            else
            {
                break;
            }
        }

        if (commentLines.Count == 0)
            return null;

        string combined = string.Join(" ", commentLines);
        var match = SummaryRegex().Match(combined);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string DetermineMemberKind(string line)
    {
        if (line.Contains('('))
            return "Method";
        if (PropertyRegex().IsMatch(line))
            return "Property";
        return "Property";
    }

    private static string ExtractMemberName(string line, string memberKind)
    {
        if (memberKind == "Method")
        {
            var match = MethodNameRegex().Match(line);
            return match.Success ? match.Groups[1].Value : "Unknown";
        }

        var propMatch = PropertyNameRegex().Match(line);
        return propMatch.Success ? propMatch.Groups[1].Value : "Unknown";
    }

    [GeneratedRegex(@"<summary>(.*?)</summary>", RegexOptions.Singleline)]
    private static partial Regex SummaryRegex();

    [GeneratedRegex(@"public\s+(?:sealed\s+)?class\s+(\w+)")]
    private static partial Regex ClassRegex();

    [GeneratedRegex(@"\{\s*get\s*[;{]")]
    private static partial Regex PropertyRegex();

    [GeneratedRegex(@"\s(\w+)\s*\(")]
    private static partial Regex MethodNameRegex();

    [GeneratedRegex(@"\s(\w+)\s*\{")]
    private static partial Regex PropertyNameRegex();
}
