using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Module;

namespace Broiler.JavaScript.Runtime.Tests;

/// <summary>
/// Tests for the <see cref="IJSModuleResolver"/> interface contract defined
/// in the Runtime assembly.
/// </summary>
public class IJSModuleResolverTests
{
    /// <summary>
    /// A minimal implementation for testing the interface contract.
    /// </summary>
    private sealed class StubModuleResolver : IJSModuleResolver
    {
        private readonly Dictionary<string, string> _modules = new();

        public void Register(string path, string source) =>
            _modules[path] = source;

        public string? Resolve(string currentPath, string moduleName)
        {
            var resolved = Path.Combine(currentPath, moduleName);
            return _modules.ContainsKey(resolved) ? resolved : null;
        }

        public Task<string> LoadSourceAsync(string resolvedPath)
        {
            if (_modules.TryGetValue(resolvedPath, out var source))
                return Task.FromResult(source);
            throw new FileNotFoundException($"Module not found: {resolvedPath}");
        }
    }

    [Fact]
    public void InterfaceCanBeImplemented()
    {
        IJSModuleResolver resolver = new StubModuleResolver();
        Assert.NotNull(resolver);
    }

    [Fact]
    public void Resolve_ReturnsPath_WhenModuleExists()
    {
        var resolver = new StubModuleResolver();
        resolver.Register("/app/math.js", "export const pi = 3.14;");

        var result = resolver.Resolve("/app", "math.js");
        Assert.Equal("/app/math.js", result);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenModuleDoesNotExist()
    {
        var resolver = new StubModuleResolver();

        var result = resolver.Resolve("/app", "nonexistent.js");
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadSourceAsync_ReturnsSource_WhenModuleExists()
    {
        var resolver = new StubModuleResolver();
        var expectedSource = "export default function() { return 42; }";
        resolver.Register("/app/answer.js", expectedSource);

        var source = await resolver.LoadSourceAsync("/app/answer.js");
        Assert.Equal(expectedSource, source);
    }

    [Fact]
    public async Task LoadSourceAsync_Throws_WhenModuleDoesNotExist()
    {
        var resolver = new StubModuleResolver();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => resolver.LoadSourceAsync("/app/missing.js"));
    }

    [Fact]
    public void Resolve_HandlesNestedPaths()
    {
        var resolver = new StubModuleResolver();
        resolver.Register("/app/lib/utils.js", "export const trim = s => s.trim();");

        var result = resolver.Resolve("/app/lib", "utils.js");
        Assert.Equal("/app/lib/utils.js", result);
    }

    [Fact]
    public async Task LoadSourceAsync_ReturnsCompletedTask_ForSyncSource()
    {
        var resolver = new StubModuleResolver();
        resolver.Register("/mod.js", "const x = 1;");

        var task = resolver.LoadSourceAsync("/mod.js");
        Assert.True(task.IsCompleted);
        Assert.Equal("const x = 1;", await task);
    }

    [Fact]
    public void MultipleImplementations_AreIndependent()
    {
        IJSModuleResolver resolver1 = new StubModuleResolver();
        IJSModuleResolver resolver2 = new StubModuleResolver();

        Assert.NotSame(resolver1, resolver2);
    }
}

/// <summary>
/// Tests for the <see cref="ExportAttribute"/> and <see cref="DefaultExportAttribute"/>
/// types moved to the Runtime assembly.
/// </summary>
public class ExportAttributeTests
{
    [Fact]
    public void ExportAttribute_DefaultName_IsNull()
    {
        var attr = new ExportAttribute();
        Assert.Null(attr.Name);
    }

    [Fact]
    public void ExportAttribute_CustomName_IsPreserved()
    {
        var attr = new ExportAttribute("myExport");
        Assert.Equal("myExport", attr.Name);
    }

    [Fact]
    public void ExportAttribute_IsAttribute()
    {
        var attr = new ExportAttribute();
        Assert.IsAssignableFrom<Attribute>(attr);
    }

    [Fact]
    public void DefaultExportAttribute_Name_IsDefault()
    {
        var attr = new DefaultExportAttribute();
        Assert.Equal("default", attr.Name);
    }

    [Fact]
    public void DefaultExportAttribute_ExtendsExportAttribute()
    {
        var attr = new DefaultExportAttribute();
        Assert.IsAssignableFrom<ExportAttribute>(attr);
    }

    [Fact]
    public void ExportAttribute_LivesInRuntimeAssembly()
    {
        var asm = typeof(ExportAttribute).Assembly;
        Assert.Equal("Broiler.JavaScript.Runtime", asm.GetName().Name);
    }

    [Fact]
    public void DefaultExportAttribute_LivesInRuntimeAssembly()
    {
        var asm = typeof(DefaultExportAttribute).Assembly;
        Assert.Equal("Broiler.JavaScript.Runtime", asm.GetName().Name);
    }
}

/// <summary>
/// Tests for <see cref="CancellableDisposableAction"/> moved to Runtime.
/// </summary>
public class CancellableDisposableActionTests
{
    [Fact]
    public void Dispose_InvokesAction()
    {
        var invoked = false;
        using (var cda = new CancellableDisposableAction(() => invoked = true))
        {
            Assert.False(invoked);
        }
        Assert.True(invoked);
    }

    [Fact]
    public void Cancel_PreventsActionOnDispose()
    {
        var invoked = false;
        using (var cda = new CancellableDisposableAction(() => invoked = true))
        {
            cda.Cancel();
        }
        Assert.False(invoked);
    }

    [Fact]
    public void CommitBool_PreventsActionAndReturnsTrue()
    {
        var invoked = false;
        bool result;
        using (var cda = new CancellableDisposableAction(() => invoked = true))
        {
            result = cda.Commit();
        }
        Assert.True(result);
        Assert.False(invoked);
    }

    [Fact]
    public void CommitGeneric_PreventsActionAndReturnsValue()
    {
        var invoked = false;
        int result;
        using (var cda = new CancellableDisposableAction(() => invoked = true))
        {
            result = cda.Commit(42);
        }
        Assert.Equal(42, result);
        Assert.False(invoked);
    }

    [Fact]
    public void LivesInRuntimeAssembly()
    {
        var asm = typeof(CancellableDisposableAction).Assembly;
        Assert.Equal("Broiler.JavaScript.Runtime", asm.GetName().Name);
    }
}
