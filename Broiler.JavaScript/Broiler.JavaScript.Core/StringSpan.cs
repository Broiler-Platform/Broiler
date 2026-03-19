#nullable enable
using Broiler.JavaScript.Core.Core;
using System.Diagnostics;

namespace Broiler.JavaScript.Core;

[DebuggerDisplay("{Key}: {Value}")]
public struct KeyValue
{
    public string Key;
    public JSValue Value;
}
