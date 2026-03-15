#nullable enable
namespace YantraJS.Core.FastParser;

public class AstExportStatement : AstStatement
{
    public readonly AstNode? Declaration;
    public readonly bool IsDefault;
    public readonly bool ExportAll;
    public readonly AstNode? Source;

    public AstExportStatement(FastToken token, AstNode argument, bool IsDefault = false)
        : base(token, FastNodeType.ExportStatement, argument.End)
    {
        Declaration = argument;
        this.IsDefault = IsDefault;
        Source = null;
    }

    public AstExportStatement(FastToken token, AstNode? argument, AstNode source)
        : base(token, FastNodeType.ExportStatement, source.End)
    {
        Declaration = argument;
        IsDefault = false;
        ExportAll = argument == null;
        Source = source;
    }

}