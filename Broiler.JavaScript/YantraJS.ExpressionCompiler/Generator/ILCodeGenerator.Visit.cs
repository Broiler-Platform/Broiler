using YantraJS.Expressions;

namespace YantraJS.Generator;

public struct CodeInfo(bool success)
{
    public readonly bool Success = success;

    public static implicit operator CodeInfo(bool success) => new(success);

    public static implicit operator bool (CodeInfo ci) => ci.Success;

}

public partial class ILCodeGenerator: YExpressionVisitor<CodeInfo>
{

}
