using Broiler.JavaScript.Ast;
using Broiler.JavaScript.Ast.Misc;
using Broiler.JavaScript.Core;
using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Core.Primitive;
using Broiler.JavaScript.BuiltIns.Null;

namespace Broiler.JavaScript.Runtime.Tests;

public class RuntimeTests
{
    [Fact]
    public void JSUndefined_IsUndefined()
    {
        var undef = JSUndefined.Value;
        Assert.True(undef.IsUndefined);
        Assert.False(undef.IsNull);
    }

    [Fact]
    public void JSNull_IsNull()
    {
        var n = JSNull.Value;
        Assert.True(n.IsNull);
        Assert.False(n.IsUndefined);
    }

    [Fact]
    public void PropertyKey_FromInt_IsUInt()
    {
        PropertyKey key = 42;
        Assert.True(key.IsUInt);
        Assert.Equal(42u, key.Index);
    }

    [Fact]
    public void PropertyKey_FromString_IsKeyString()
    {
        var ks = KeyStrings.GetOrCreate(new StringSpan("prop"));
        PropertyKey key = ks;
        Assert.False(key.IsUInt);
        Assert.False(key.IsSymbol);
    }
}
