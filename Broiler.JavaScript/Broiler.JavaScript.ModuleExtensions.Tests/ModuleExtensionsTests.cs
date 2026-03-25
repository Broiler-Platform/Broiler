using Broiler.JavaScript.BuiltIns.String;
using Broiler.JavaScript.ModuleExtensions;
using Broiler.JavaScript.Runtime;

namespace Broiler.JavaScript.ModuleExtensions.Tests;

public class ModuleExtensionsTests
{
    [Fact]
    public void ModuleBuilder_Create_Succeeds()
    {
        var builder = new ModuleBuilder("testmod");
        Assert.NotNull(builder);
    }

    [Fact]
    public void ModuleBuilder_ExportValue_ReturnsSelf()
    {
        var builder = new ModuleBuilder("testmod");
        var result = builder.ExportValue("version", "1.0");
        Assert.Same(builder, result);
    }

    [Fact]
    public void ModuleBuilder_ExportFunction_ReturnsSelf()
    {
        var builder = new ModuleBuilder("testmod");
        var result = builder.ExportFunction("greet", (in Arguments a) => new JSString("hello"));
        Assert.Same(builder, result);
    }
}
