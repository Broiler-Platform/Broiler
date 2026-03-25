using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Modules;
using Broiler.JavaScript.Runtime;
using Broiler.JavaScript.Storage;

namespace Broiler.JavaScript.Modules.Tests;

public class ModulesTests
{
    [Fact]
    public void JSModuleContext_Create_Succeeds()
    {
        var ctx = new JSModuleContext();
        Assert.NotNull(ctx);
    }

    [Fact]
    public void JSModuleContext_RegisterModule_Succeeds()
    {
        var ctx = new JSModuleContext();
        var exports = new JSObject();
        ctx.RegisterModule(KeyStrings.GetOrCreate(new StringSpan("testmod")), exports);
    }

    [Fact]
    public void JSModule_Create_WithExports()
    {
        var ctx = new JSModuleContext();
        var exports = new JSObject();
        var module = new JSModule(ctx, exports, "mymod");
        Assert.NotNull(module);
    }
}
