using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Module;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.Core.Core.Storage;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Modules.Tests;

/// <summary>
/// Tests for the <see cref="JSModuleContext"/> class extracted into
/// the <c>Broiler.JavaScript.Modules</c> assembly.
/// </summary>
public class JSModuleContextTests : IDisposable
{
    private readonly JSModuleContext _context;

    public JSModuleContextTests()
    {
        _context = new JSModuleContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void Constructor_CreatesValidContext()
    {
        Assert.NotNull(_context);
        Assert.IsAssignableFrom<JSContext>(_context);
    }

    [Fact]
    public void RegisterModule_MakesModuleAvailable()
    {
        var exports = new JSObject();
        exports.FastAddValue(KeyStrings.GetOrCreate("hello"),
            new JSString("world"), JSPropertyAttributes.EnumerableConfigurableValue);

        _context.RegisterModule(KeyStrings.GetOrCreate("test-mod"), exports);

        // The module should be accessible via All
        Assert.Contains(_context.All, m => m.filePath == "test-mod");
    }

    [Fact]
    public void ClrModuleProvider_IsAccessibleViaJSContext()
    {
        // ClrModuleProvider was moved to JSContext for cross-assembly access.
        // The Clr assembly's module initializer sets it.
        Assert.NotNull(JSContext.ClrModuleProvider);
    }

    [Fact]
    public void JSModuleContext_InheritsFromJSContext()
    {
        // Verify the upward dependency pattern: Modules → Core
        Assert.IsAssignableFrom<JSContext>(_context);
    }
}

/// <summary>
/// Tests for the <see cref="ModuleCache"/> struct.
/// </summary>
public class ModuleCacheTests
{
    [Fact]
    public void ModuleCache_Create_ReturnsInstance()
    {
        var cache = ModuleCache.Create();
        // The static module/clr fields should be initialized
        Assert.NotEqual(0u, ModuleCache.module.Key);
        Assert.NotEqual(0u, ModuleCache.clr.Key);
    }

    [Fact]
    public void ModuleCache_ModuleAndClr_HaveDifferentKeys()
    {
        Assert.NotEqual(ModuleCache.module.Key, ModuleCache.clr.Key);
    }
}

/// <summary>
/// Tests for the <see cref="JSModule"/> class.
/// </summary>
public class JSModuleTests : IDisposable
{
    private readonly JSModuleContext _context;

    public JSModuleTests()
    {
        _context = new JSModuleContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void JSModule_CreateClass_ReturnsConstructor()
    {
        var ctor = JSModule.CreateClass(_context, false);
        Assert.NotNull(ctor);
        Assert.IsAssignableFrom<JSValue>(ctor);
    }

    [Fact]
    public void JSModule_Constructor_SetsFilePath()
    {
        var exports = new JSObject();
        var module = new JSModule(_context, exports, "my-module");
        Assert.Equal("my-module", module.filePath);
    }

    [Fact]
    public void JSModule_Exports_AreAccessible()
    {
        var exports = new JSObject();
        var module = new JSModule(_context, exports, "test");
        Assert.Same(exports, (JSObject)module.Exports);
    }
}
