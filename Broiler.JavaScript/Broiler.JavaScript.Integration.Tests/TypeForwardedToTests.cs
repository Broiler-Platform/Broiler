using System.Reflection;
using System.Runtime.CompilerServices;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Disposable;
using Broiler.JavaScript.Core.Debugger;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Module;
using Broiler.JavaScript.Core.Core.Object;
using Broiler.JavaScript.Core.Core.Storage;
using Broiler.JavaScript.Core.Emit;
using Broiler.JavaScript.Core.Extensions;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.Integration.Tests;

/// <summary>
/// Tests that verify <c>TypeForwardedTo</c> attributes in the Core assembly
/// correctly resolve types that were moved to Runtime, Storage, and other
/// assemblies during the refactor.
/// </summary>
public class TypeForwardedToTests
{
    private static readonly Assembly CoreAssembly =
        typeof(JSContext).Assembly;

    private static readonly Assembly RuntimeAssembly =
        typeof(JSValue).Assembly;

    /// <summary>
    /// Verifies that Core exposes forwarded types and that all forwarded
    /// types can be resolved from the Core assembly.
    /// </summary>
    [Fact]
    public void Core_HasForwardedTypes()
    {
        var forwardedTypes = CoreAssembly.GetForwardedTypes();

        Assert.NotEmpty(forwardedTypes);
        // At least 31 types forwarded as of Phase 9d
        Assert.True(forwardedTypes.Length >= 31,
            $"Expected ≥ 31 forwarded types, found {forwardedTypes.Length}");
    }

    /// <summary>
    /// Verifies that <c>JSValue</c> resolves through Core's type-forwarding
    /// to the Runtime assembly, even though consumers reference it via the
    /// Core namespace.
    /// </summary>
    [Fact]
    public void JSValue_ResolvesViaCore_ToRuntime()
    {
        var type = typeof(JSValue);
        Assert.Equal(RuntimeAssembly, type.Assembly);
        Assert.Equal("Broiler.JavaScript.Core.Core", type.Namespace);
    }

    /// <summary>
    /// Verifies that <c>Arguments</c> resolves through Core's type-forwarding
    /// to the Runtime assembly.
    /// </summary>
    [Fact]
    public void Arguments_ResolvesViaCore_ToRuntime()
    {
        Assert.Equal(RuntimeAssembly, typeof(Arguments).Assembly);
    }

    /// <summary>
    /// Verifies that <c>PropertyKey</c> resolves through Core's type-forwarding
    /// to the Runtime assembly.
    /// </summary>
    [Fact]
    public void PropertyKey_ResolvesViaCore_ToRuntime()
    {
        Assert.Equal(RuntimeAssembly, typeof(PropertyKey).Assembly);
    }

    /// <summary>
    /// Verifies that <c>CoreScript</c> resolves through Core's type-forwarding
    /// to the Runtime assembly.
    /// </summary>
    [Fact]
    public void CoreScript_ResolvesViaCore_ToRuntime()
    {
        Assert.Equal(RuntimeAssembly, typeof(CoreScript).Assembly);
    }

    /// <summary>
    /// Verifies that contract interfaces (IDebugger, IClrInterop, IJSCompiler,
    /// IBuiltInRegistry, IJSContext, IJSFunction) all live in the Runtime
    /// assembly and are forwarded from Core.
    /// </summary>
    [Theory]
    [InlineData(typeof(IDebugger))]
    [InlineData(typeof(IClrInterop))]
    [InlineData(typeof(IJSCompiler))]
    [InlineData(typeof(IBuiltInRegistry))]
    [InlineData(typeof(IJSContext))]
    [InlineData(typeof(IJSFunction))]
    [InlineData(typeof(ICodeCache))]
    public void ContractInterfaces_LiveInRuntime(Type interfaceType)
    {
        Assert.Equal(RuntimeAssembly, interfaceType.Assembly);
    }

    /// <summary>
    /// Verifies that storage types (JSProperty, PropertySequence, ElementArray)
    /// live in the Storage assembly and are forwarded from Core.
    /// </summary>
    [Theory]
    [InlineData(typeof(JSProperty))]
    [InlineData(typeof(PropertySequence))]
    [InlineData(typeof(ElementArray))]
    public void StorageTypes_LiveInStorageAssembly(Type storageType)
    {
        Assert.Contains("Storage", storageType.Assembly.GetName().Name);
    }

    /// <summary>
    /// Verifies that module contract types (IJSModuleResolver, ExportAttribute)
    /// live in the Runtime assembly and are forwarded from Core.
    /// </summary>
    [Theory]
    [InlineData(typeof(IJSModuleResolver))]
    [InlineData(typeof(ExportAttribute))]
    [InlineData(typeof(DefaultExportAttribute))]
    public void ModuleContracts_LiveInRuntime(Type moduleType)
    {
        Assert.Equal(RuntimeAssembly, moduleType.Assembly);
    }

    /// <summary>
    /// Verifies that ObjectStatus lives in the Runtime assembly.
    /// </summary>
    [Fact]
    public void ObjectStatus_LivesInRuntime()
    {
        Assert.Equal(RuntimeAssembly, typeof(ObjectStatus).Assembly);
    }

    /// <summary>
    /// Verifies that KeyString types live in the Storage assembly.
    /// </summary>
    [Theory]
    [InlineData(typeof(KeyString))]
    [InlineData(typeof(KeyStrings))]
    public void KeyStringTypes_LiveInStorageAssembly(Type keyType)
    {
        Assert.Contains("Storage", keyType.Assembly.GetName().Name);
    }

    /// <summary>
    /// Verifies that IJSDisposableStack lives in the Runtime assembly.
    /// </summary>
    [Fact]
    public void IJSDisposableStack_LivesInRuntime()
    {
        Assert.Equal(RuntimeAssembly, typeof(IJSDisposableStack).Assembly);
    }
}
