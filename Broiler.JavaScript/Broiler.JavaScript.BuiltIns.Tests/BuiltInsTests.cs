using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Promise;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;
using System.Runtime.CompilerServices;

namespace Broiler.JavaScript.BuiltIns.Tests;

public class BuiltInsTests
{
    [Fact]
    public void WeakRef_Construct_And_Deref()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var obj = { value: 42 }; var wr = new WeakRef(obj); wr.deref().value;");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void EventTarget_Construct_Succeeds()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var t = new EventTarget(); typeof t;");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void FinalizationRegistry_Construct_Succeeds()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var fr = new FinalizationRegistry(function(v) {}); typeof fr;");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void WeakRef_TypeOf_IsObject()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var wr = new WeakRef({}); typeof wr;");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void BuiltIns_ModuleInitializer_Registers()
    {
        EnsureBuiltInsLoaded();
        Assert.NotNull(DefaultBuiltInRegistry.AdditionalRegistrations);
    }

    // ── M2: JSMath tests ─────────────────────────────────────────────

    [Fact]
    public void Math_PI_ReturnsCorrectValue()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.PI");
        Assert.Equal(Math.PI, result.DoubleValue, 10);
    }

    [Fact]
    public void Math_Abs_NegativeNumber()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.abs(-42)");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void Math_Floor_ReturnsFloor()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.floor(4.7)");
        Assert.Equal(4.0, result.DoubleValue);
    }

    [Fact]
    public void Math_Ceil_ReturnsCeiling()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.ceil(4.1)");
        Assert.Equal(5.0, result.DoubleValue);
    }

    [Fact]
    public void Math_Round_RoundsCorrectly()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.round(4.5)");
        Assert.Equal(5.0, result.DoubleValue);
    }

    [Fact]
    public void Math_Max_ReturnsLargest()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.max(1, 5, 3)");
        Assert.Equal(5.0, result.DoubleValue);
    }

    [Fact]
    public void Math_Min_ReturnsSmallest()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.min(1, 5, 3)");
        Assert.Equal(1.0, result.DoubleValue);
    }

    [Fact]
    public void Math_Sqrt_ReturnsSquareRoot()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.sqrt(25)");
        Assert.Equal(5.0, result.DoubleValue);
    }

    [Fact]
    public void Math_Pow_ReturnsPower()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.pow(2, 10)");
        Assert.Equal(1024.0, result.DoubleValue);
    }

    [Fact]
    public void Math_Random_ReturnsBetweenZeroAndOne()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.random()");
        var value = result.DoubleValue;
        Assert.InRange(value, 0.0, 1.0);
    }

    [Fact]
    public void Math_Trunc_Truncates()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.trunc(42.84)");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void Math_Sign_ReturnsSign()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Math.sign(-5)");
        Assert.Equal(-1.0, result.DoubleValue);
    }

    // ── M2: JSReflect tests ──────────────────────────────────────────

    [Fact]
    public void Reflect_TypeOf_IsObject()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("typeof Reflect");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void Reflect_Apply_CallsFunction()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Reflect.apply(Math.floor, undefined, [1.75])");
        Assert.Equal(1.0, result.DoubleValue);
    }

    [Fact]
    public void Reflect_OwnKeys_ReturnsKeys()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var obj = { a: 1, b: 2 }; Reflect.ownKeys(obj).length");
        Assert.Equal(2.0, result.DoubleValue);
    }

    [Fact]
    public void Reflect_Has_ChecksProperty()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Reflect.has({ x: 0 }, 'x')");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Reflect_DefineProperty_Succeeds()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var obj = {};
            Reflect.defineProperty(obj, 'x', { value: 7 });
            obj.x;
        ");
        Assert.Equal(7.0, result.DoubleValue);
    }

    [Fact]
    public void Reflect_PreventExtensions_Works()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var obj = {};
            Reflect.preventExtensions(obj);
            Reflect.isExtensible(obj);
        ");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void Reflect_Get_ReturnsValue()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Reflect.get({ x: 42 }, 'x')");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void Reflect_Set_SetsValue()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var obj = {};
            Reflect.set(obj, 'x', 99);
            obj.x;
        ");
        Assert.Equal(99.0, result.DoubleValue);
    }

    // ── M2: JSProxy tests ────────────────────────────────────────────

    [Fact]
    public void Proxy_TypeOf_IsFunction()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("typeof Proxy");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Proxy_GetTrap_Intercepts()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var handler = {
                get: function(target, name) {
                    return name in target ? target[name] : 37;
                }
            };
            var p = new Proxy({}, handler);
            p.a = 1;
            p.b;
        ");
        Assert.Equal(37.0, result.DoubleValue);
    }

    [Fact]
    public void Proxy_SetTrap_Intercepts()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var log = [];
            var handler = {
                set: function(obj, prop, value) {
                    log.push(prop);
                    obj[prop] = value;
                    return true;
                }
            };
            var p = new Proxy({}, handler);
            p.a = 1;
            log.length;
        ");
        Assert.Equal(1.0, result.DoubleValue);
    }

    [Fact]
    public void Proxy_Construct_WithTargetAndHandler()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var target = { message: 'hello' };
            var handler = {};
            var p = new Proxy(target, handler);
            p.message;
        ");
        Assert.Equal("hello", result.ToString());
    }

    // ── M2: JSConsole tests ──────────────────────────────────────────

    [Fact]
    public void Console_TypeOf_IsObject()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("typeof console");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void Console_Log_IsFunction()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("typeof console.log");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Console_Warn_IsFunction()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("typeof console.warn");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Console_Error_IsFunction()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("typeof console.error");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Console_Log_ReturnsValue()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("console.log('test')");
        Assert.Equal("test", result.ToString());
    }

    [Fact]
    public void ConsoleFactory_WiredByModuleInitializer()
    {
        EnsureBuiltInsLoaded();
        Assert.NotNull(DefaultBuiltInRegistry.ConsoleFactory);
    }

    // ── M3: JSJSON tests ─────────────────────────────────────────────

    [Fact]
    public void JSON_Parse_ReturnsObject()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("JSON.parse('{\"a\":1}').a");
        Assert.Equal(1.0, result.DoubleValue);
    }

    [Fact]
    public void JSON_Stringify_ReturnsString()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("JSON.stringify({ a: 1 })");
        Assert.Equal("{\"a\":1}", result.ToString());
    }

    [Fact]
    public void JSON_Parse_WithReviver()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            JSON.parse('{""a"":1,""b"":2}', function(key, value) {
                return typeof value === 'number' ? value * 2 : value;
            }).a;
        ");
        Assert.Equal(2.0, result.DoubleValue);
    }

    [Fact]
    public void JSON_Stringify_WithIndent()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("JSON.stringify({ a: 1 }, null, 2)");
        Assert.Contains("\"a\"", result.ToString());
    }

    // ── M3: DataView tests ───────────────────────────────────────────

    [Fact]
    public void DataView_Construct_Succeeds()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var buf = new ArrayBuffer(16); var dv = new DataView(buf); dv.byteLength;");
        Assert.Equal(16.0, result.DoubleValue);
    }

    [Fact]
    public void DataView_SetAndGetInt8()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var buf = new ArrayBuffer(4);
            var dv = new DataView(buf);
            dv.setInt8(0, 42);
            dv.getInt8(0);
        ");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void DataView_SetAndGetFloat32()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var buf = new ArrayBuffer(4);
            var dv = new DataView(buf);
            dv.setFloat32(0, 3.14, true);
            Math.round(dv.getFloat32(0, true) * 100) / 100;
        ");
        Assert.Equal(3.14, result.DoubleValue, 2);
    }

    // ── M3: ArrayBuffer transfer tests ───────────────────────────────

    [Fact]
    public void ArrayBuffer_Transfer_Copies_Bytes_And_Detaches_Source()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var source = new ArrayBuffer(4);
            var sourceView = new Uint8Array(source);
            sourceView[0] = 7;
            sourceView[1] = 11;
            var moved = source.transfer(6);
            var movedView = new Uint8Array(moved);
            [source.detached, moved.detached, moved.byteLength, movedView[0], movedView[1], movedView[4]].join('|');
        ");
        Assert.Equal("true|false|6|7|11|0", result.ToString());
    }

    [Fact]
    public void ArrayBuffer_TransferToFixedLength_Returns_Transferred_Buffer()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var source = new ArrayBuffer(4);
            new Uint8Array(source)[0] = 99;
            var moved = source.transferToFixedLength(2);
            [source.detached, moved.detached, moved.byteLength, new Uint8Array(moved)[0]].join('|');
        ");
        Assert.Equal("true|false|2|99", result.ToString());
    }

    [Fact]
    public void ArrayBuffer_Transferred_Source_Throws_On_ByteLength_Access()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        Assert.Throws<JSException>(() => ctx.Eval(@"
            var source = new ArrayBuffer(4);
            source.transfer(2);
            source.byteLength;
        "));
    }

    // ── M3: JSMap tests ──────────────────────────────────────────────

    [Fact]
    public void Map_SetAndGet()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var m = new Map(); m.set('key', 42); m.get('key');");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void Map_Size()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var m = new Map(); m.set('a', 1); m.set('b', 2); m.size;");
        Assert.Equal(2.0, result.DoubleValue);
    }

    [Fact]
    public void Map_Has_ReturnsTrue()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var m = new Map(); m.set('x', 1); m.has('x');");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Map_Delete_RemovesEntry()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        // Verify delete returns true when key exists
        var result = ctx.Eval("var m = new Map(); m.set('x', 1); m['delete']('x');");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Map_ForEach_Iterates()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var m = new Map();
            m.set('a', 1);
            m.set('b', 2);
            var sum = 0;
            m.forEach(function(k, v) { sum += v; });
            sum;
        ");
        Assert.Equal(3.0, result.DoubleValue);
    }

    // ── M3: JSWeakMap tests ──────────────────────────────────────────

    [Fact]
    public void WeakMap_SetAndGet()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var wm = new WeakMap(); var k = {}; wm.set(k, 99); wm.get(k);");
        Assert.Equal(99.0, result.DoubleValue);
    }

    [Fact]
    public void WeakMap_Has_ReturnsTrue()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var wm = new WeakMap(); var k = {}; wm.set(k, 1); wm.has(k);");
        Assert.True(result.BooleanValue);
    }

    // ── M3: JSSet tests ──────────────────────────────────────────────

    [Fact]
    public void Set_Add_And_Has()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var s = new Set(); s.add(42); s.has(42);");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Set_Size()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var s = new Set(); s.add(1); s.add(2); s.add(2); s.size;");
        Assert.Equal(2.0, result.DoubleValue);
    }

    [Fact]
    public void Set_Delete()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        // Verify delete returns true when element exists
        var result = ctx.Eval("var s = new Set(); s.add(1); s['delete'](1);");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Set_Union()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var a = new Set([1,2]); var b = new Set([2,3]); a.union(b).size;");
        Assert.Equal(3.0, result.DoubleValue);
    }

    [Fact]
    public void Set_Intersection()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var a = new Set([1,2,3]); var b = new Set([2,3,4]); a.intersection(b).size;");
        Assert.Equal(2.0, result.DoubleValue);
    }

    [Fact]
    public void Set_Difference()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var result = new Set([1,2,3]).difference(new Set([2,4]));
            [result.has(1), result.has(2), result.has(3), result.size].join('|');
        ");
        Assert.Equal("true|false|true|2", result.ToString());
    }

    [Fact]
    public void Set_SymmetricDifference()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var result = new Set([1,2,3]).symmetricDifference(new Set([2,4]));
            [result.has(1), result.has(2), result.has(3), result.has(4), result.size].join('|');
        ");
        Assert.Equal("true|false|true|true|3", result.ToString());
    }

    [Fact]
    public void Set_Subset_Superset_And_Disjoint_Methods()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var a = new Set([1, 2]);
            var b = new Set([1, 2, 3]);
            var c = new Set([4, 5]);
            [a.isSubsetOf(b), b.isSupersetOf(a), a.isDisjointFrom(c)].join('|');
        ");
        Assert.Equal("true|true|true", result.ToString());
    }

    // ── M3: ES2025 built-in coverage ──────────────────────────────────

    [Fact]
    public async Task Promise_Try_Resolves_Return_Value()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Promise.try((a, b) => a + b, 19, 23);");
        var promise = Assert.IsType<JSPromise>(result);
        var resolved = await promise.Task;
        Assert.Equal(42.0, resolved.DoubleValue);
    }

    [Fact]
    public async Task Promise_Try_Rejects_Synchronous_Exception()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Promise.try(() => { throw new Error('boom'); });");
        var promise = Assert.IsType<JSPromise>(result);
        await Assert.ThrowsAsync<JSException>(async () => await promise.Task);
    }

    [Fact]
    public void RegExp_Escape_Escapes_Syntax_Characters()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"RegExp.escape('hello.world?+[test]{x}(y)|/\\^$');");
        Assert.Equal(@"hello\.world\?\+\[test\]\{x\}\(y\)\|\/\\\^\$", result.ToString());
    }

    [Fact]
    public void RegExp_V_Flag_Exposes_UnicodeSets_Metadata()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var re = new RegExp('a', 'v');
            [re.flags, re.unicodeSets, re.unicode].join('|');
        ");
        Assert.Equal("v|true|false", result.ToString());
    }

    [Fact]
    public void RegExp_V_Flag_Cannot_Be_Combined_With_U()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        Assert.Throws<JSException>(() => ctx.Eval("new RegExp('a', 'uv');"));
    }

    [Fact]
    public void RegExp_V_Flag_Cannot_Be_Specified_Twice()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        Assert.Throws<JSException>(() => ctx.Eval("new RegExp('a', 'vv');"));
    }

    [Fact]
    public void Iterator_From_Map_Filter_Take_ToArray()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Iterator.from([1,2,3,4]).map(v => v * 2).filter(v => v > 4).take(2).toArray().join(',');");
        Assert.Equal("6,8", result.ToString());
    }

    [Fact]
    public void Iterator_Concat_Reduce_Across_Iterables()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("Iterator.concat([1,2], new Set([3,4]).values()).reduce((sum, value) => sum + value, 0);");
        Assert.Equal(10.0, result.DoubleValue);
    }

    [Fact]
    public void Generator_Instances_Inherit_Iterator_Helpers()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            function* values() { yield 1; yield 2; yield 3; }
            values().drop(1).find(v => v === 2);
        ");
        Assert.Equal(2.0, result.DoubleValue);
    }

    // ── M3: JSWeakSet tests ──────────────────────────────────────────

    [Fact]
    public void WeakSet_Add_And_Has()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        // WeakSet.add returns the WeakSet itself per spec
        var result = ctx.Eval("var ws = new WeakSet(); var o = {}; typeof ws.add(o);");
        Assert.Equal("object", result.ToString());
    }

    [Fact]
    public void WeakSet_Delete()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("var ws = new WeakSet(); var o = {}; ws.add(o); ws['delete'](o);");
        Assert.True(result.BooleanValue);
    }

    // ── M3: StructuredClone with Map/Set ─────────────────────────────

    [Fact]
    public void StructuredClone_Map()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var m = new Map();
            m.set('a', 1);
            var clone = structuredClone(m);
            clone.get('a');
        ");
        Assert.Equal(1.0, result.DoubleValue);
    }

    [Fact]
    public void StructuredClone_Set()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var s = new Set([1, 2, 3]);
            var clone = structuredClone(s);
            clone.size;
        ");
        Assert.Equal(3.0, result.DoubleValue);
    }

    /// <summary>
    /// Forces the BuiltIns and Clr assemblies to load by referencing types from them,
    /// which triggers their ModuleInitializers.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Fact]
    public void JSBoolean_TrueAndFalse()
    {
        Assert.True(JSBoolean.True.BooleanValue);
        Assert.False(JSBoolean.False.BooleanValue);
    }

    private static void EnsureBuiltInsLoaded()
    {
        // Load CLR assembly so JSEngine.ClrInterop is properly configured
        // (required for JSConsole marshalling via ClrProxy).
        RuntimeHelpers.RunClassConstructor(
            typeof(Broiler.JavaScript.Clr.DefaultClrInterop).TypeHandle);
        RuntimeHelpers.RunClassConstructor(
            typeof(Broiler.JavaScript.BuiltIns.Weak.JSWeakRef).TypeHandle);
    }
}
