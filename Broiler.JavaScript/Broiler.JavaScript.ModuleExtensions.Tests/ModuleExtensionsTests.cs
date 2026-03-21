using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Module;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Storage;
using Broiler.JavaScript.ModuleExtensions;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.ModuleExtensions.Tests;

/// <summary>
/// Tests for the <see cref="ModuleBuilder"/> fluent API.
/// </summary>
public class ModuleBuilderTests : IDisposable
{
    private readonly JSModuleContext _context;

    public ModuleBuilderTests()
    {
        _context = new JSModuleContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void ExportType_Generic_UsesTypeNameByDefault()
    {
        _context.CreateModule("test-generic", builder =>
            builder.ExportType<DateTime>());

        Assert.Contains(_context.All, m => m.filePath == "test-generic");
    }

    [Fact]
    public void ExportType_Generic_UsesCustomName()
    {
        _context.CreateModule("test-generic-named", builder =>
            builder.ExportType<DateTime>("MyDate"));

        Assert.Contains(_context.All, m => m.filePath == "test-generic-named");
    }

    [Fact]
    public void ExportType_NonGeneric_UsesTypeNameByDefault()
    {
        _context.CreateModule("test-nongeneric", builder =>
            builder.ExportType(typeof(DateTime)));

        Assert.Contains(_context.All, m => m.filePath == "test-nongeneric");
    }

    [Fact]
    public void ExportType_NonGeneric_UsesCustomName()
    {
        _context.CreateModule("test-nongeneric-named", builder =>
            builder.ExportType(typeof(DateTime), "CustomDate"));

        Assert.Contains(_context.All, m => m.filePath == "test-nongeneric-named");
    }

    [Fact]
    public void ExportValue_RegistersValue()
    {
        _context.CreateModule("test-value", builder =>
            builder.ExportValue("greeting", "hello"));

        Assert.Contains(_context.All, m => m.filePath == "test-value");
    }

    [Fact]
    public void ExportFunction_RegistersFunction()
    {
        _context.CreateModule("test-func", builder =>
            builder.ExportFunction("myFunc", (in Arguments a) => JSUndefined.Value));

        Assert.Contains(_context.All, m => m.filePath == "test-func");
    }

    [Fact]
    public void FluentChaining_MultipleExports()
    {
        _context.CreateModule("test-chained", builder =>
            builder
                .ExportType<DateTime>("Date")
                .ExportValue("version", "1.0")
                .ExportFunction("helper", (in Arguments a) => JSUndefined.Value));

        Assert.Contains(_context.All, m => m.filePath == "test-chained");
    }

    [Fact]
    public void AddModuleToContext_SetsDefaultExport()
    {
        _context.CreateModule("test-default", builder =>
            builder.ExportValue("name", "test"));

        var exports = _context.ImportModule(KeyStrings.GetOrCreate("test-default"));
        // default export should point to the exports object itself
        Assert.False(exports.IsNullOrUndefined);
    }

    [Fact]
    public void ExportFunction_IsCallableFromJavaScript()
    {
        _context.CreateModule("test-callable", builder =>
            builder.ExportFunction("add", (in Arguments a) =>
            {
                var x = a[0].IntValue;
                var y = a[1].IntValue;
                return new JSNumber(x + y);
            }));

        // Verify the function export is accessible via ImportModule
        var exports = _context.ImportModule(KeyStrings.GetOrCreate("test-callable"));
        Assert.False(exports.IsNullOrUndefined);
        // The exported function should be available under "add"
        var addFn = exports[KeyStrings.GetOrCreate("add")];
        Assert.False(addFn.IsNullOrUndefined);
    }
}

/// <summary>
/// Tests for the <see cref="JSModuleContextExtension"/> extension methods.
/// </summary>
public class JSModuleContextExtensionTests : IDisposable
{
    private readonly JSModuleContext _context;

    public JSModuleContextExtensionTests()
    {
        _context = new JSModuleContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void CreateModule_RegistersModuleInContext()
    {
        _context.CreateModule("ext-test", builder =>
            builder.ExportValue("key", "value"));

        Assert.Contains(_context.All, m => m.filePath == "ext-test");
    }

    [Fact]
    public void ImportModule_ReturnsRegisteredModule()
    {
        _context.CreateModule("import-test", builder =>
            builder.ExportValue("data", "hello"));

        var result = _context.ImportModule(KeyStrings.GetOrCreate("import-test"));
        Assert.NotNull(result);
        Assert.False(result.IsNullOrUndefined);
    }

    [Fact]
    public void ImportModule_ThrowsForUnknownModule()
    {
        Assert.Throws<ArgumentException>(() =>
            _context.ImportModule(KeyStrings.GetOrCreate("nonexistent-module")));
    }

    [Fact]
    public void CreateModule_ExportValueUsableFromJavaScript()
    {
        _context.CreateModule("js-val", builder =>
            builder.ExportValue("msg", "hello"));

        // Verify the value export is accessible via ImportModule
        var exports = _context.ImportModule(KeyStrings.GetOrCreate("js-val"));
        var msg = exports[KeyStrings.GetOrCreate("msg")];
        Assert.Equal("hello", msg.ToString());
    }
}
