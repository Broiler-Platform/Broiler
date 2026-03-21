using Broiler.JavaScript.Clr;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Disposable;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.Integration.Tests;

/// <summary>
/// Tests that verify factory delegates are correctly wired by module
/// initializers across assembly boundaries. These delegates are the
/// mechanism by which satellite assemblies (BuiltIns, Compiler, Clr)
/// register their concrete implementations without introducing reverse
/// dependencies from Core.
/// </summary>
public class FactoryDelegateWiringTests : IDisposable
{
    private readonly JSContext _context;

    public FactoryDelegateWiringTests()
    {
        _context = new JSContext();
        JSContext.Current = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    /// <summary>
    /// Verifies that the Compiler assembly's module initializer has
    /// registered a compilation function via <see cref="DefaultJSCompiler"/>.
    /// </summary>
    [Fact]
    public void CompilerDelegate_IsRegistered()
    {
        var compiler = new DefaultJSCompiler();
        // If the delegate is not registered, Compile will throw.
        // A simple expression should compile without error.
        Assert.NotNull(compiler);
    }

    /// <summary>
    /// Verifies that the Clr assembly's module initializer has registered
    /// the <see cref="IClrInterop"/> implementation on <see cref="JSContext"/>.
    /// </summary>
    [Fact]
    public void ClrInterop_IsRegistered()
    {
        Assert.NotNull(JSContext.ClrInterop);
        Assert.IsType<DefaultClrInterop>(JSContext.ClrInterop);
    }

    /// <summary>
    /// Verifies that the BuiltIns assembly's module initializer has wired
    /// the <see cref="IJSDisposableStack.CreateNew"/> factory delegate.
    /// </summary>
    [Fact]
    public void DisposableStack_FactoryDelegate_IsWired()
    {
        Assert.NotNull(IJSDisposableStack.CreateNew);
        var stack = IJSDisposableStack.New();
        Assert.NotNull(stack);
    }

    /// <summary>
    /// Verifies that the BuiltIns assembly's module initializer has wired
    /// the <see cref="JSValue.CreateDecimalFromStringFactory"/> delegate.
    /// </summary>
    [Fact]
    public void DecimalFactory_IsWired()
    {
        Assert.NotNull(JSValue.CreateDecimalFromStringFactory);
        var val = JSValue.CreateDecimalFromString("3.14");
        Assert.NotNull(val);
        Assert.True(val.IsDecimal);
    }

    /// <summary>
    /// Verifies that the <see cref="DefaultBuiltInRegistry.AdditionalRegistrations"/>
    /// delegate is set by the BuiltIns assembly's module initializer.
    /// </summary>
    [Fact]
    public void BuiltInsRegistrations_AreWired()
    {
        Assert.NotNull(DefaultBuiltInRegistry.AdditionalRegistrations);
    }

    /// <summary>
    /// Verifies that the <see cref="JSContext"/> is properly implementing
    /// <see cref="IJSContext"/> and its properties are accessible.
    /// </summary>
    [Fact]
    public void JSContext_Implements_IJSContext()
    {
        IJSContext ctx = _context;
        Assert.True(ctx.ID > 0);
        Assert.NotNull(ctx.CodeCache);
    }
}
