using Broiler.JavaScript.BuiltIns.Array;
using Broiler.JavaScript.BuiltIns.Boolean;
using Broiler.JavaScript.BuiltIns.Promise;
using Broiler.JavaScript.Engine;
using Broiler.JavaScript.Runtime;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Broiler.JavaScript.BuiltIns.Tests;

public class BuiltInsTests
{
    private static JSContext CreateContext(JavaScriptFeatureFlags experimentalFeatures = JavaScriptFeatureFlags.AllExperimentalEs2026)
        => new(experimentalFeatures: experimentalFeatures);

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

    [Fact]
    public void JSON_Parse_SourceTextAccess_IsDisabled_WhenFlagIsOff()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.AllExperimentalEs2026 & ~JavaScriptFeatureFlags.JsonParseSourceTextAccess);
        var result = ctx.Eval("""
            var seen = [];
            JSON.parse('{"a":1}', function(key, value, context) {
                seen.push(context === undefined ? 'missing' : 'present');
                return value;
            });
            seen.join(',');
            """);
        Assert.Equal("missing", result.ToString());
    }

    [Fact]
    public void JSON_Parse_SourceTextAccess_IsEnabled_WhenFlagIsOn()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.JsonParseSourceTextAccess);
        var result = ctx.Eval("""
            var seen = [];
            JSON.parse('{"a":1}', function(key, value, context) {
                if (key === 'a') {
                    seen.push(context === undefined ? 'missing' : 'present');
                    seen.push(context.source);
                }
                return value;
            });
            seen.join('|');
            """);
        Assert.Equal("present|1", result.ToString());
    }

    [Fact]
    public void Error_IsError_IsDisabled_WhenFlagIsOff()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.AllExperimentalEs2026 & ~JavaScriptFeatureFlags.ErrorIsError);
        var result = ctx.Eval("typeof Error.isError");
        Assert.Equal("undefined", result.ToString());
    }

    [Fact]
    public void Error_IsError_IsEnabled_WhenFlagIsOn()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.ErrorIsError);
        var result = ctx.Eval("[Error.isError(new Error('x')), Error.isError(new TypeError('y')), Error.isError({})].join('|')");
        Assert.Equal("true|true|false", result.ToString());
    }

    [Fact]
    public void Array_FromAsync_IsDisabled_WhenFlagIsOff()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.AllExperimentalEs2026 & ~JavaScriptFeatureFlags.ArrayFromAsync);
        var result = ctx.Eval("typeof Array.fromAsync");
        Assert.Equal("undefined", result.ToString());
    }

    [Fact]
    public async Task Array_FromAsync_IsEnabled_WhenFlagIsOn()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.ArrayFromAsync);
        var promise = Assert.IsType<JSPromise>(ctx.Eval("Array.fromAsync([1, 2, 3], value => value * 2)"));
        var result = Assert.IsType<JSArray>(await promise.Task);
        ctx["resultArray"] = result;
        Assert.Equal("2,4,6", ctx.Eval("resultArray.join(',')").ToString());
    }

    [Fact]
    public void Object_And_Map_GroupBy_Are_Disabled_WhenFlagIsOff()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.AllExperimentalEs2026 & ~JavaScriptFeatureFlags.ObjectMapGroupBy);
        var result = ctx.Eval("typeof Object.groupBy + '|' + typeof Map.groupBy");
        Assert.Equal("undefined|undefined", result.ToString());
    }

    [Fact]
    public void Object_And_Map_GroupBy_Are_Enabled_WhenFlagIsOn()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.ObjectMapGroupBy);
        var result = ctx.Eval("""
            var objectGroups = Object.groupBy([1, 2, 3, 4], function(value) {
                return value % 2 === 0 ? 'even' : 'odd';
            });
            var mapGroups = Map.groupBy([1, 2, 3, 4], function(value) {
                return value % 2;
            });
            [
                objectGroups.odd.join(','),
                objectGroups.even.join(','),
                mapGroups.get(1).join(','),
                mapGroups.get(0).join(',')
            ].join('|');
            """);
        Assert.Equal("1,3|2,4|1,3|2,4", result.ToString());
    }

    [Fact]
    public void Iterator_Concat_IsDisabled_WhenFlagIsOff()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.AllExperimentalEs2026 & ~JavaScriptFeatureFlags.IteratorConcat);
        var result = ctx.Eval("typeof Iterator.concat + '|' + typeof Iterator.from");
        Assert.Equal("undefined|function", result.ToString());
    }

    [Fact]
    public void Iterator_Concat_IsEnabled_WhenFlagIsOn()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.IteratorConcat);
        var result = ctx.Eval("Iterator.concat([1,2], new Set([3,4]).values()).reduce((sum, value) => sum + value, 0);");
        Assert.Equal(10.0, result.DoubleValue);
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

    // ── M2: ArrayBuffer transfer tests ───────────────────────────────

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

    // ── M2: Hashbang grammar tests ────────────────────────────────────

    [Fact]
    public void Hashbang_At_Start_Of_Source_Is_Ignored()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("#!/usr/bin/env node\n1 + 2;");
        Assert.Equal(3.0, result.DoubleValue);
    }

    [Fact]
    public void Hashbang_Not_At_Start_Of_Source_Is_Rejected()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        Assert.Throws<JSException>(() => ctx.Eval("0;\n#!/usr/bin/env node\n1;"));
    }

    [Fact]
    public void Eval_Var_Can_Coexist_With_Global_Let()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("let value = 1; [eval('var value = 2; value;'), value].join('|');");
        Assert.Equal("2|1", result.ToString());
    }

    [Fact]
    public void Eval_Var_Can_Coexist_With_Global_Const()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("const value = 1; [eval('var value = 2; value;'), value].join('|');");
        Assert.Equal("2|1", result.ToString());
    }

    [Fact]
    public void Eval_Var_Can_Be_Redeclared_Across_Direct_Evals()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("let value = 1; [eval('var value = 2; value;'), eval('var value = 3; value;'), value].join('|');");
        Assert.Equal("2|3|1", result.ToString());
    }

    // ── M2: JSMap tests ──────────────────────────────────────────────

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

    [Fact]
    public void Set_Methods_Accept_SetLike_Objects()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var other = {
                size: 2,
                has(value) { return value === 2 || value === 4; },
                keys: function* () { yield 2; yield 4; }
            };
            var baseSet = new Set([1, 2, 3]);
            var union = baseSet.union(other);
            var intersection = baseSet.intersection(other);
            var difference = baseSet.difference(other);
            var symmetric = baseSet.symmetricDifference(other);
            [
                union.size, union.has(1), union.has(4),
                intersection.size, intersection.has(2),
                difference.size, difference.has(1), difference.has(3),
                symmetric.size, symmetric.has(1), symmetric.has(3), symmetric.has(4),
                baseSet.isSubsetOf(other), baseSet.isSupersetOf(other), baseSet.isDisjointFrom(other)
            ].join('|');
        ");
        Assert.Equal("4|true|true|1|true|2|true|true|3|true|true|true|false|false|false", result.ToString());
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
    public void Promise_Try_Uses_The_Receiver_Constructor()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var captured = null;
            var calls = 0;
            function CustomPromise(executor) {
                captured = executor;
                calls++;
            }

            var instance = Promise.try.call(CustomPromise, () => 42);
            [instance.constructor === CustomPromise, calls, typeof captured].join('|');
        ");
        Assert.Equal("true|1|function", result.ToString());
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
    public void RegExp_Escape_Escapes_Whitespace_And_Rejects_NonStrings()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"RegExp.escape('\uFEFF \u00A0\u202F');");
        Assert.Equal(@"\ufeff\x20\xa0\u202f", result.ToString());
        Assert.Throws<JSException>(() => ctx.Eval("RegExp.escape(123);"));
    }

    [Fact]
    public void String_IsWellFormed_Detects_Paired_And_Lone_Surrogates()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            [
                'plain'.isWellFormed(),
                '\uD83C\uDF89'.isWellFormed(),
                '\uD800'.isWellFormed(),
                '\uDC00'.isWellFormed()
            ].join('|');
        ");
        Assert.Equal("true|true|false|false", result.ToString());
    }

    [Fact]
    public void String_ToWellFormed_Replaces_Lone_Surrogates_With_Replacement_Character()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            [
                '\uD800A\uDC00'.toWellFormed(),
                '\uD83C\uDF89'.toWellFormed()
            ].join('|');
        ");
        Assert.Equal("\uFFFDA\uFFFD|🎉", result.ToString());
    }

    [Fact]
    public void String_ToWellFormed_Always_Produces_A_WellFormed_String()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"'\uD800'.toWellFormed().isWellFormed();");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void Finally_Return_Overrides_Try_And_Catch_Completions()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            [
                (function() { try { return 'try'; } finally { return 'finally'; } })(),
                (function() { try { throw new Error('boom'); } catch (e) { return 'catch'; } finally { return 'finally'; } })()
            ].join('|');
        ");
        Assert.Equal("finally|finally", result.ToString());
    }

    [Fact]
    public void Finally_Return_Skips_Remaining_Finally_Statements()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            (function() {
                var order = [];
                try {
                    order.push('try');
                    return 'try';
                } finally {
                    order.push('finally-before');
                    return order.join(',');
                    order.push('finally-after');
                }
            })();
        ");
        Assert.Equal("try,finally-before", result.ToString());
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
    public void RegExp_V_Flag_Works_For_Literals()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var re = /a/v;
            [re.flags, re.unicodeSets, re.test('a')].join('|');
        ");
        Assert.Equal("v|true|true", result.ToString());
    }

    [Fact]
    public void RegExp_V_Flag_Cannot_Be_Combined_With_U()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        Assert.Throws<JSException>(() => ctx.Eval("new RegExp('a', 'uv');"));
        Assert.Throws<JSException>(() => ctx.Eval("/a/vu;"));
    }

    [Fact]
    public void RegExp_V_Flag_Cannot_Be_Specified_Twice()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        Assert.Throws<JSException>(() => ctx.Eval("new RegExp('a', 'vv');"));
        Assert.Throws<JSException>(() => ctx.Eval("/a/vv;"));
    }

    [Fact]
    public void RegExp_Flags_Are_Normalized_And_Metadata_Is_Exposed()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            var re = new RegExp('a', 'yidg');
            [re.flags, re.hasIndices, re.sticky, re.global, re.ignoreCase].join('|');
        ");
        Assert.Equal("dgiy|true|true|true|true", result.ToString());
    }

    [Fact]
    public void RegExp_D_And_Y_Flags_Cannot_Be_Specified_Twice()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        Assert.Throws<JSException>(() => ctx.Eval("new RegExp('a', 'dd');"));
        Assert.Throws<JSException>(() => ctx.Eval("new RegExp('a', 'yy');"));
    }

    [Fact]
    public void RegExp_Literal_Duplicate_D_And_Y_Flags_Are_Rejected()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        Assert.Throws<JSException>(() => ctx.Eval("/a/dd"));
        Assert.Throws<JSException>(() => ctx.Eval("/a/yy"));
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
        using var ctx = CreateContext(JavaScriptFeatureFlags.IteratorConcat);
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

    [Fact]
    public void Iterator_Helper_Callbacks_Receive_Counters()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval(@"
            function* letters() {
                yield 'a';
                yield 'b';
                yield 'c';
            }

            var map = letters().map((value, count) => value + count).toArray().join(',');
            var filterCounts = [];
            [...letters().filter((value, count) => { filterCounts.push(count); return true; })];
            var flatMapCounts = [];
            [...letters().flatMap((value, count) => { flatMapCounts.push(count); return [value]; })];
            var forEachCounts = [];
            letters().forEach((value, count) => forEachCounts.push(count));
            var reduceCounts = [];
            letters().reduce((memo, value, count) => { reduceCounts.push(count); return value; });
            [map, filterCounts.join(','), flatMapCounts.join(','), forEachCounts.join(','), reduceCounts.join(',')].join('|');
        ");
        Assert.Equal("a0,b1,c2|0,1,2|0,1,2|0,1,2|1,2", result.ToString());
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
        using var ctx = CreateContext(JavaScriptFeatureFlags.StructuredClone);
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
        using var ctx = CreateContext(JavaScriptFeatureFlags.StructuredClone);
        var result = ctx.Eval(@"
            var s = new Set([1, 2, 3]);
            var clone = structuredClone(s);
            clone.size;
        ");
        Assert.Equal(3.0, result.DoubleValue);
    }

    [Fact]
    public void StructuredClone_Transfer_ArrayBuffer_Detaches_Source_And_Returns_Clone()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.StructuredClone);
        var result = ctx.Eval(@"
            var source = new ArrayBuffer(4);
            var sourceView = new Uint8Array(source);
            sourceView[0] = 7;
            sourceView[1] = 11;
            var clone = structuredClone(source, { transfer: [source] });
            var cloneView = new Uint8Array(clone);
            [source.detached, clone.detached, clone.byteLength, cloneView[0], cloneView[1], clone !== source].join('|');
        ");
        Assert.Equal("true|false|4|7|11|true", result.ToString());
    }

    [Fact]
    public void StructuredClone_TypedArray_Preserves_View_And_Copies_Buffer()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.StructuredClone);
        var result = ctx.Eval(@"
            var source = new ArrayBuffer(6);
            var bytes = new Uint8Array(source);
            bytes[2] = 7;
            bytes[3] = 11;
            bytes[4] = 13;
            var sourceView = new Uint8Array(source, 2, 3);
            var clone = structuredClone(sourceView);
            bytes[2] = 99;
            [
              clone instanceof Uint8Array,
              clone.buffer !== source,
              clone.byteOffset,
              clone.length,
              clone[0],
              clone[2]
            ].join('|');
        ");
        Assert.Equal("true|true|2|3|7|13", result.ToString());
    }

    [Fact]
    public void StructuredClone_Transfer_Reuses_Transferred_Buffer_For_TypedArray_And_DataView()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.StructuredClone);
        var result = ctx.Eval(@"
            var source = new ArrayBuffer(6);
            var bytes = new Uint8Array(source);
            bytes.set([1, 2, 3, 4, 5, 6]);
            var payload = {
              buffer: source,
              typed: new Uint8Array(source, 1, 3),
              view: new DataView(source, 2, 2)
            };
            var clone = structuredClone(payload, { transfer: [source] });
            [
              source.detached,
              clone.buffer !== source,
              clone.typed instanceof Uint8Array,
              clone.typed.buffer === clone.buffer,
              clone.typed.byteOffset,
              clone.typed.length,
              clone.typed[0],
              clone.typed[2],
              clone.view instanceof DataView,
              clone.view.buffer === clone.buffer,
              clone.view.byteOffset,
              clone.view.byteLength,
              clone.view.getUint8(0),
              clone.view.getUint8(1)
            ].join('|');
        ");
        Assert.Equal("true|true|true|true|1|3|2|4|true|true|2|2|3|4", result.ToString());
    }

    [Fact]
    public void StructuredClone_Transfer_Rejects_NonArrayBuffer_Entries()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.StructuredClone);
        Assert.Throws<JSException>(() => ctx.Eval(@"
            structuredClone({ ok: true }, { transfer: [{}] });
        "));
    }

    [Fact]
    public void StructuredClone_Transfer_Rejects_Duplicate_ArrayBuffers()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.StructuredClone);
        Assert.Throws<JSException>(() => ctx.Eval(@"
            var source = new ArrayBuffer(4);
            structuredClone(source, { transfer: [source, source] });
        "));
    }

    [Fact]
    public void ExperimentalEs2026Features_CanBeDisabled_PerContext()
    {
        EnsureBuiltInsLoaded();
        using var ctx = new JSContext();
        var result = ctx.Eval("""
            [
              typeof structuredClone,
              typeof Math.sumPrecise,
              typeof Uint8Array.fromBase64,
              typeof Uint8Array.prototype.toBase64,
              typeof Map.prototype.getOrInsert,
              typeof WeakMap.prototype.getOrInsert
            ].join('|');
            """);
        Assert.Equal("undefined|undefined|undefined|undefined|undefined|undefined", result.ToString());
    }

    [Fact]
    public void ExperimentalEs2026Features_CanBeEnabled_Selectively()
    {
        EnsureBuiltInsLoaded();
        using var ctx = CreateContext(JavaScriptFeatureFlags.StructuredClone | JavaScriptFeatureFlags.MapUpsert);
        var result = ctx.Eval("""
            [
              typeof structuredClone,
              typeof Math.sumPrecise,
              typeof Map.prototype.getOrInsert,
              typeof Uint8Array.fromBase64
            ].join('|');
            """);
        Assert.Equal("function|undefined|function|undefined", result.ToString());
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
