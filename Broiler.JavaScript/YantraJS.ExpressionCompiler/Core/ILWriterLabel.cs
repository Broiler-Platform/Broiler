using System.Reflection.Emit;
using System.Threading;

namespace YantraJS.Core;

public class ILWriterLabel(Label value, string label, ILTryBlock tryBlock)
{
    public readonly Label Value = value;
    public readonly ILTryBlock TryBlock = tryBlock;
    public readonly string ID = $"{label ?? "LABEL"}_{Interlocked.Increment(ref nextID)}";

    public int Offset;

    private static int nextID = 1;

    public override string ToString() => ID;
}
