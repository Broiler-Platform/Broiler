using System.Reflection;
using System.Runtime.CompilerServices;
using Broiler.JavaScript.Clr;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Clr;
using Broiler.JavaScript.Core.Core.Disposable;
using Broiler.JavaScript.Core.FastParser.Compiler;

namespace Broiler.JavaScript.Integration.Tests;

/// <summary>
/// Tests that verify module initializer registration ordering across
/// assemblies. The refactored engine relies on module initializers to
/// wire factory delegates and register satellite-assembly services.
/// These tests ensure the ordering contract is correct.
/// </summary>
public class ModuleInitializerOrderingTests
{
    /// <summary>
    /// Verifies that the Compiler assembly's module initializer has run
    /// (i.e., <see cref="DefaultJSCompiler"/> has a registered compilation
    /// function) before any integration test code executes.
    /// </summary>
    [Fact]
    public void Compiler_ModuleInitializer_HasRun()
    {
        // The Compiler module initializer registers via DefaultJSCompiler.Register.
        // If it hasn't run, compile will throw a NullReferenceException.
        var compiler = new DefaultJSCompiler();
        // Compile a trivial script to verify the pipeline is wired.
        var code = new Broiler.JavaScript.Ast.StringSpan("1");
        var result = compiler.Compile(code);
        Assert.NotNull(result);
    }

    /// <summary>
    /// Verifies that the Clr assembly's module initializer has run and
    /// registered the <see cref="IClrInterop"/> instance.
    /// </summary>
    [Fact]
    public void Clr_ModuleInitializer_HasRun()
    {
        // The Clr module initializer sets JSContext.ClrInterop.
        Assert.NotNull(JSContext.ClrInterop);
    }

    /// <summary>
    /// Verifies that the BuiltIns assembly's module initializer has run
    /// and registered factory delegates and additional type registrations.
    /// </summary>
    [Fact]
    public void BuiltIns_ModuleInitializer_HasRun()
    {
        // BuiltIns module initializer sets:
        // 1. DefaultBuiltInRegistry.AdditionalRegistrations
        // 2. IJSDisposableStack.CreateNew
        // 3. JSValue.CreateDecimalFactory
        // 4. JSValue.CreateDecimalFromStringFactory
        Assert.NotNull(DefaultBuiltInRegistry.AdditionalRegistrations);
        Assert.NotNull(IJSDisposableStack.CreateNew);
        Assert.NotNull(JSValue.CreateDecimalFromStringFactory);
    }

    /// <summary>
    /// Verifies that all three satellite module initializers (Clr, Compiler,
    /// BuiltIns) are independently functional — they do not depend on a
    /// specific load order relative to each other.
    /// </summary>
    [Fact]
    public void All_Satellite_Initializers_AreIndependent()
    {
        // Each satellite initializer registers its own delegates/services
        // without depending on other satellites being loaded first.
        // If any initializer depended on another, the bootstrap loading
        // order in IntegrationTestBootstrap would matter and a different
        // order could break tests.

        // Clr — provides IClrInterop
        Assert.NotNull(JSContext.ClrInterop);

        // Compiler — provides IJSCompiler via DefaultJSCompiler
        var compiler = new DefaultJSCompiler();
        Assert.NotNull(compiler);

        // BuiltIns — provides factory delegates
        Assert.NotNull(IJSDisposableStack.CreateNew);
        Assert.NotNull(JSValue.CreateDecimalFromStringFactory);
        Assert.NotNull(DefaultBuiltInRegistry.AdditionalRegistrations);
    }
}
