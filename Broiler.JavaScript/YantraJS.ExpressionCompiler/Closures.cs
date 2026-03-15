using System.Reflection;

namespace YantraJS;

public class Closures(
    IMethodRepository repository,
    Box[] boxes,
    string il,
    string exp)
{
    internal static FieldInfo repositoryField = typeof(Closures).GetField(nameof(Repository));
    internal static FieldInfo boxesField = typeof(Closures).GetField(nameof(Boxes));
    internal static ConstructorInfo constructor = typeof(Closures).GetConstructors()[0];

    public readonly IMethodRepository Repository = repository;
    public readonly Box[] Boxes = boxes;
    public readonly string IL = il;
    public readonly string Exp = exp;
}
