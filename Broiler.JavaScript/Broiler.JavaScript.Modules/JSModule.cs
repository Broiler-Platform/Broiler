using Broiler.JavaScript.Engine.Core;
using Broiler.JavaScript.ExpressionCompiler;
using Broiler.JavaScript.Runtime;
using System;
using System.Threading.Tasks;

namespace Broiler.JavaScript.Modules;

/// <summary>
/// Create and load a module
/// </summary>

[JSBaseClass("Object")]
[JSFunctionGenerator("Module", Register = false)]
public partial class JSModule : JSObject
{
    public readonly string filePath;
    internal readonly string dirPath;

    [JSPrototypeMethod]
    [JSExport("code")]
    public string Code { get; set; }

    public JSModule(in Arguments a) => throw new NotSupportedException();

    public JSModule(JSModuleContext context, JSObject exports, string name, bool isMain = false) : this(context.ModulePrototype)
    {
        filePath = name;
        dirPath = "./";
        this.exports = exports;
    }

    internal JSModule(JSModuleContext context, string name, string code = null) : this(context.ModulePrototype)
    {
        filePath = name;
        dirPath = System.IO.Path.GetDirectoryName(dirPath);
        Code = code;
    }

    [JSPrototypeMethod]
    [JSExport("id")]
    public JSValue Id => JSValue.CreateString(filePath);

    JSValue exports;

    [JSPrototypeMethod]
    [JSExport("exports")]
    public JSValue Exports
    {
        get
        {
            return exports;
        }
        set
        {
            if (value == null || value.IsNullOrUndefined)
                throw JSEngine.NewTypeError("Exports cannot be set to null or undefined");

            exports = value;
        }
    }

    [JSPrototypeMethod]
    [JSExport("require")]
    public JSValue Require { get; set; }

    [JSPrototypeMethod]
    [JSExport("import")]
    public JSValue Import { get; set; }

    public Task<JSValue> ImportAsync(string name)
    {
        var result = Import.InvokeFunction(new Arguments(JSUndefined.Value, JSValue.CreateString(name)));
        return (result as IJSPromise).Task;
    }

    [JSPrototypeMethod]
    [JSExport("compile")]
    public JSValue Compile { get; set; }

    internal async Task InitAsync()
    {
        if (exports != null)
            return;

        exports = new JSObject();

        var result = Compile.InvokeFunction(new Arguments(this));
        if (result is IJSPromise promise)
            await promise.Task;
    }
}
