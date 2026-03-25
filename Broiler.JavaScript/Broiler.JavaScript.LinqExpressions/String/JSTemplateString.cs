using Broiler.JavaScript.Runtime;
using System.Text;

namespace Broiler.JavaScript.LinqExpressions.String;

public class JSTemplateString(int size)
{
    readonly StringBuilder sb = new(size);

    public void Add(string t) => sb.Append(t);

    public void Add(JSValue value) => sb.Append(value.ToString());

    public JSTemplateString AddQuasi(string text)
    {
        sb.Append(text);
        return this;
    }

    public JSTemplateString AddExpression(JSValue value)
    {
        sb.Append(value.ToString());
        return this;
    }

    public override string ToString() => sb.ToString();

    public JSValue ToJSString() => JSValue.CreateString(sb.ToString());

}
