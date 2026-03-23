using Broiler.JavaScript.Core.Core.Boolean;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Extensions;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Storage;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Broiler.JavaScript.Core.Core.Error;


[JSClassGenerator("Error")]
public partial class JSError : JSObject
{
    public string Message { get; private set; }
    public string Stack { get; private set; }

    private string CreateStack()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{ToString(Arguments.Empty)}");

        var top = JSContext.Current.Top;
        while (top != null)
        {
            // ref var top = ref walker.Current;
            var fx = top.Function;
            var file = top.FileName;

            if (fx.IsNullOrWhiteSpace())
                fx = "native";

            if (string.IsNullOrWhiteSpace(file))
                file = "file";

            sb.AppendLine($"    at {fx}:{file}:{top.Line},{top.Column}");
            top = top.Parent;
        }

        return sb.ToString();
    }

    public JSError(in Arguments a, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) :
        this(JSContext.NewTargetPrototype)
    {
        Exception = new JSException(this, function: function, filePath: filePath, line: line);
        var message = a[0]?.ToString() ?? "Internal Error";

        Message = message;
        Stack = CreateStack();

        FastAddValue(KeyStrings.message, message.Marshal(), JSPropertyAttributes.ConfigurableValue);
        FastAddValue(KeyStrings.stack, Stack.Marshal(), JSPropertyAttributes.ConfigurableValue);
    }

    [JSExport("isError")]
    internal static JSValue IsError(in Arguments a)
    {
        var arg = a.Get1();
        return arg is JSError ? JSBoolean.True : JSBoolean.False;
    }

    [JSExport("toString")]
    public new JSValue ToString(in Arguments a)
    {
        var name = prototypeChain.Object[KeyStrings.constructor][KeyStrings.name];
        return new JSString($"{name}: {Message}");
    }

    public override string ToString() => ToString(Arguments.Empty).ToString();

    public override string ToDetailString() => ToString(Arguments.Empty).ToString() + "\r\n" + Exception.JSStackTrace.ToString();

    public const string Cannot_convert_undefined_or_null_to_object = "Cannot convert undefined or null to object";

    public const string Parameter_is_not_an_object = "Parameter is not an object";

    public JSException Exception { get; }

    internal protected JSError(JSException ex, JSObject prototype = null) : base(prototype)
    {
        Exception = ex;
        ex.Error ??= this;

        FastAddValue(KeyStrings.message, ex.Message.Marshal(), JSPropertyAttributes.ConfigurableValue);
        FastAddValue(KeyStrings.stack, ex.JSStackTrace, JSPropertyAttributes.ConfigurableValue);
    }

    internal JSError(JSException ex, string msg) : this()
    {
        Exception = ex;
        ex.Error ??= this;
        Message = msg;

        FastAddValue(KeyStrings.message, msg.Marshal(), JSPropertyAttributes.ConfigurableValue);
        FastAddValue(KeyStrings.stack, ex.JSStackTrace, JSPropertyAttributes.ConfigurableValue);
    }

    public static JSValue From(Exception ex)
    {
        if (ex is JSException jse)
            return jse.Error;

        var je = new JSException(ex.Message + "\r\n" + ex.ToString());
        return je.Error;
    }
}

[JSClassGenerator("TypeError"), JSBaseClass("Error")]
public partial class JSTypeError : JSError
{
    public JSTypeError(in Arguments a, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) :
        base(in a, function: function, filePath: filePath, line: line)
    { }

    public static string NotIterable(object name) => $"{name} is not iterable";
    public static string NotEntry(object name) => $"Iterator value {name} is an entry object";
}

[JSClassGenerator("SyntaxError"), JSBaseClass("Error")]
public partial class JSSyntaxError : JSError
{
    public JSSyntaxError(in Arguments a, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) :
        base(in a, function: function, filePath: filePath, line: line)
    { }
}

[JSClassGenerator("URIError"), JSBaseClass("Error")]
public partial class JSURIError : JSError
{
    public JSURIError(in Arguments a, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) :
        base(in a, function: function, filePath: filePath, line: line)
    { }
}

[JSClassGenerator("RangeError"), JSBaseClass("Error")]
public partial class JSRangeError : JSError
{
    public JSRangeError(in Arguments a, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) :
        base(in a, function: function, filePath: filePath, line: line)
    { }
}

[JSClassGenerator("EvalError"), JSBaseClass("Error")]
public partial class JSEvalError : JSError
{
    public JSEvalError(in Arguments a, [CallerMemberName] string function = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0) :
        base(in a, function: function, filePath: filePath, line: line)
    { }
}
