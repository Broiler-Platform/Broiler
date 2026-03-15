#nullable enable
using System.IO;

namespace YantraJS.Core;

public class StringSpanReader(StringSpan span) : TextReader
{
    private int index = 0;

    public override int Peek()
    {
        if (index >= span.Length)
            return -1;
        return span[index];
    }

    public override int Read()
    {
        if (index >= span.Length)
            return -1;
        return span[index++];
    }

    public override string ReadToEnd() => span.Substring(index).Value ?? string.Empty;
}
