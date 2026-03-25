using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Broiler.JavaScript.BuiltIns.Error;

[JSClassGenerator("Error")]
public partial class JSError : JSObject, IJSError
{
    public string Message { get; private set; }
    public string Stack { get; private set; }

    Exception IJSError.Exception => Exception;

    private string CreateStack()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{ToString(Arguments.Empty)}");

        var top = (JSEngine.Current as IJSExecutionContext)?.Top;
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
        this(JSEngine.NewTargetPrototype)
    {
        Exception = new JSException(this, function: function, filePath: filePath, line: line);
        var message = a[0]?.ToString() ?? "Internal Error";

        Message = message;
        Stack = CreateStack();

        FastAddValue(KeyStrings.message, JSValue.CreateString(message), JSPropertyAttributes.ConfigurableValue);
        FastAddValue(KeyStrings.stack, JSValue.CreateString(Stack), JSPropertyAttributes.ConfigurableValue);
    }

    [JSExport("isError")]
    internal static JSValue IsError(in Arguments a)
    {
        var arg = a.Get1();
        return arg is JSError ? JSValue.BooleanTrue : JSValue.BooleanFalse;
    }

    [JSExport("toString")]
    public new JSValue ToString(in Arguments a)
    {
        var name = prototypeChain.Object[KeyStrings.constructor][KeyStrings.name];
        return JSValue.CreateString($"{name}: {Message}");
    }

    public override string ToString() => ToString(Arguments.Empty).ToString();

    public override string ToDetailString() => ToString(Arguments.Empty).ToString() + "\r\n" + Exception.JSStackTrace.ToString();

    public JSException Exception { get; }

    internal protected JSError(JSException ex, JSObject prototype = null) : base(prototype)
    {
        Exception = ex;
        ex.Error ??= this;

        FastAddValue(KeyStrings.message, JSValue.CreateString(ex.Message), JSPropertyAttributes.ConfigurableValue);
        FastAddValue(KeyStrings.stack, ex.JSStackTrace, JSPropertyAttributes.ConfigurableValue);
    }

    internal JSError(JSException ex, string msg) : this()
    {
        Exception = ex;
        ex.Error ??= this;
        Message = msg;

        FastAddValue(KeyStrings.message, JSValue.CreateString(msg), JSPropertyAttributes.ConfigurableValue);
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
