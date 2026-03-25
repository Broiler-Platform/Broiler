using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Runtime;

public class ScriptInfo
{
    public string FileName;
    public string Code;
    public KeyString[] Indices;
    public object[] Functions;
}
