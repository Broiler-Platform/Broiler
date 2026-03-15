namespace YantraJS.Core.FastParser;

public class AstLabeledStatement(FastToken id, AstStatement statement) : AstStatement(id, FastNodeType.LabeledStatement, statement.End)
{
    public readonly FastToken Label = id;
    public readonly AstStatement Body = statement;

    public override string ToString() => $"{Label}: {Body}";
}