namespace YantraJS.Core.FastParser;

public class AstClassProperty(
    FastToken begin,
    FastToken last,
    AstPropertyKind propertyKind,
    bool isPrivate,
    bool isStatic,
    AstExpression propertyName,
    bool computed,
    AstExpression init) : AstNode(begin,  FastNodeType.ClassProperty, last)
{
    public readonly bool IsStatic = isStatic;
    public readonly bool IsPrivate = isPrivate;
    public readonly AstPropertyKind Kind = propertyKind;
    public readonly AstExpression Key = propertyName;
    public readonly AstExpression Init = init;
    public readonly bool Computed = computed;

    public AstClassProperty Reduce(AstExpression key, AstExpression init) => new(Start, End, Kind, IsPrivate, IsStatic, key, Computed, init);

    public override string ToString()
    {
        if (Kind == AstPropertyKind.Constructor)
            return $"constructor: {Init}";
        if(IsStatic)
        {
            if (Kind == AstPropertyKind.Get)
                return $"static get {Key} {Init}";
            if (Kind == AstPropertyKind.Set)
                return $"static set {Key} {Init}";
            if (Computed)
            {
                if (Kind == AstPropertyKind.Data)
                    return $"static [{Key}]: {Init}";
            }
            if (Kind == AstPropertyKind.Data)
                return $"static {Key}: {Init}";
        }
        if (Kind == AstPropertyKind.Get)
            return $"get {Key} {Init}";
        if (Kind == AstPropertyKind.Set)
            return $"set {Key} {Init}";
        if (Kind == AstPropertyKind.Data)
            return $"{Key}: {Init}";
        if (Computed)
        {
            if (Kind == AstPropertyKind.Data)
                return $"[{Key}]: {Init}";
        }
        return "AstClassProperty";
    }
}
