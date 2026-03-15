using System.Threading;

namespace YantraJS.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for Phase 4 features:
/// Math.sumPrecise (§4.2), Uint8Array Base64/Hex (§4.3),
/// JSON.parse source text access (§4.7), Map.getOrInsert (§4.9),
/// structuredClone (§4.11).
/// </summary>
public class Phase4EcmaScriptTests : IDisposable
{
    private readonly JSContext _context;
    private readonly SynchronizationContext _previousSyncCtx;

    public Phase4EcmaScriptTests()
    {
        _previousSyncCtx = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        _context = new JSContext(SynchronizationContext.Current);
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
        SynchronizationContext.SetSynchronizationContext(_previousSyncCtx);
    }

    // ===============================================================
    // §4.2  Math.sumPrecise
    // ===============================================================

    [Fact]
    public void MathSumPrecise_IsFunction()
    {
        var result = _context.Eval("typeof Math.sumPrecise");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void MathSumPrecise_BasicSum()
    {
        var result = _context.Eval("Math.sumPrecise([1, 2, 3])");
        Assert.Equal(6.0, result.DoubleValue);
    }

    [Fact]
    public void MathSumPrecise_EmptyArray()
    {
        var result = _context.Eval("Math.sumPrecise([])");
        Assert.Equal(0.0, result.DoubleValue);
    }

    [Fact]
    public void MathSumPrecise_NaN()
    {
        var result = _context.Eval("Math.sumPrecise([1, NaN, 3])");
        Assert.True(double.IsNaN(result.DoubleValue));
    }

    [Fact]
    public void MathSumPrecise_PositiveInfinity()
    {
        var result = _context.Eval("Math.sumPrecise([1, Infinity, 3])");
        Assert.True(double.IsPositiveInfinity(result.DoubleValue));
    }

    [Fact]
    public void MathSumPrecise_NegativeInfinity()
    {
        var result = _context.Eval("Math.sumPrecise([1, -Infinity, 3])");
        Assert.True(double.IsNegativeInfinity(result.DoubleValue));
    }

    [Fact]
    public void MathSumPrecise_MixedInfinities()
    {
        // +Infinity and -Infinity should produce NaN
        var result = _context.Eval("Math.sumPrecise([Infinity, -Infinity])");
        Assert.True(double.IsNaN(result.DoubleValue));
    }

    [Fact]
    public void MathSumPrecise_CompensatedPrecision()
    {
        // Classic floating-point precision test: 0.1 + 0.2 + 0.3
        // should be closer to 0.6 with compensated summation
        var result = _context.Eval("Math.sumPrecise([0.1, 0.2, 0.3])");
        Assert.Equal(0.6, result.DoubleValue, 10);
    }

    [Fact]
    public void MathSumPrecise_LargeSmallNumbers()
    {
        // Test precision: 1e20 + 1 + -1e20 should be exactly 1
        var result = _context.Eval("Math.sumPrecise([1e20, 1, -1e20])");
        Assert.Equal(1.0, result.DoubleValue);
    }

    // ===============================================================
    // §4.3  Uint8Array Base64/Hex
    // ===============================================================

    [Fact]
    public void Uint8Array_FromBase64_IsFunction()
    {
        var result = _context.Eval("typeof Uint8Array.fromBase64");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Uint8Array_FromBase64_Basic()
    {
        var result = _context.Eval(@"
            var arr = Uint8Array.fromBase64('AQID');
            arr[0] + ',' + arr[1] + ',' + arr[2]
        ");
        Assert.Equal("1,2,3", result.ToString());
    }

    [Fact]
    public void Uint8Array_ToBase64_Basic()
    {
        var result = _context.Eval(@"
            var arr = new Uint8Array([1, 2, 3]);
            arr.toBase64()
        ");
        Assert.Equal("AQID", result.ToString());
    }

    [Fact]
    public void Uint8Array_FromBase64_ToBase64_Roundtrip()
    {
        var result = _context.Eval(@"
            var original = 'SGVsbG8gV29ybGQ=';
            var arr = Uint8Array.fromBase64(original);
            arr.toBase64()
        ");
        Assert.Equal("SGVsbG8gV29ybGQ=", result.ToString());
    }

    [Fact]
    public void Uint8Array_FromHex_IsFunction()
    {
        var result = _context.Eval("typeof Uint8Array.fromHex");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Uint8Array_FromHex_Basic()
    {
        var result = _context.Eval(@"
            var arr = Uint8Array.fromHex('010203');
            arr[0] + ',' + arr[1] + ',' + arr[2]
        ");
        Assert.Equal("1,2,3", result.ToString());
    }

    [Fact]
    public void Uint8Array_ToHex_Basic()
    {
        var result = _context.Eval(@"
            var arr = new Uint8Array([255, 0, 171]);
            arr.toHex()
        ");
        Assert.Equal("ff00ab", result.ToString());
    }

    [Fact]
    public void Uint8Array_FromHex_ToHex_Roundtrip()
    {
        var result = _context.Eval(@"
            var original = 'deadbeef';
            var arr = Uint8Array.fromHex(original);
            arr.toHex()
        ");
        Assert.Equal("deadbeef", result.ToString());
    }

    [Fact]
    public void Uint8Array_SetFromBase64_Basic()
    {
        var result = _context.Eval(@"
            var arr = new Uint8Array(5);
            var r = arr.setFromBase64('AQID');
            r.written
        ");
        Assert.Equal(3.0, result.DoubleValue);
    }

    [Fact]
    public void Uint8Array_SetFromBase64_WritesCorrectData()
    {
        var result = _context.Eval(@"
            var arr = new Uint8Array(5);
            arr.setFromBase64('AQID');
            arr[0] + ',' + arr[1] + ',' + arr[2]
        ");
        Assert.Equal("1,2,3", result.ToString());
    }

    [Fact]
    public void Uint8Array_SetFromHex_Basic()
    {
        var result = _context.Eval(@"
            var arr = new Uint8Array(5);
            var r = arr.setFromHex('0102');
            r.written
        ");
        Assert.Equal(2.0, result.DoubleValue);
    }

    [Fact]
    public void Uint8Array_SetFromHex_WritesCorrectData()
    {
        var result = _context.Eval(@"
            var arr = new Uint8Array(5);
            arr.setFromHex('ff0a');
            arr[0] + ',' + arr[1]
        ");
        Assert.Equal("255,10", result.ToString());
    }

    // ===============================================================
    // §4.7  JSON.parse source text access
    // ===============================================================

    [Fact]
    public void JSONParse_Reviver_ReceivesContext()
    {
        var result = _context.Eval(@"
            var sources = [];
            JSON.parse('{""a"": 1, ""b"": ""hello""}', function(key, value, context) {
                if (key !== '' && context && context.source !== undefined) {
                    sources.push(key + ':' + context.source);
                }
                return value;
            });
            sources.join(',')
        ");
        Assert.Equal("a:1,b:\"hello\"", result.ToString());
    }

    [Fact]
    public void JSONParse_Reviver_SourceForNumber()
    {
        var result = _context.Eval(@"
            var src;
            JSON.parse('42', function(key, value, context) {
                if (context && context.source !== undefined) {
                    src = context.source;
                }
                return value;
            });
            src
        ");
        Assert.Equal("42", result.ToString());
    }

    [Fact]
    public void JSONParse_Reviver_SourceForString()
    {
        var result = _context.Eval(@"
            var src;
            JSON.parse('""test""', function(key, value, context) {
                if (context && context.source !== undefined) {
                    src = context.source;
                }
                return value;
            });
            src
        ");
        Assert.Equal("\"test\"", result.ToString());
    }

    [Fact]
    public void JSONParse_Reviver_SourceForBoolean()
    {
        var result = _context.Eval(@"
            var src;
            JSON.parse('{""flag"": true}', function(key, value, context) {
                if (key === 'flag' && context) src = context.source;
                return value;
            });
            src
        ");
        Assert.Equal("true", result.ToString());
    }

    [Fact]
    public void JSONParse_Reviver_NoSourceForObject()
    {
        // Objects and arrays should not have a source property on context
        var result = _context.Eval(@"
            var hasSource = false;
            JSON.parse('{""a"": {""b"": 1}}', function(key, value, context) {
                if (key === 'a' && context && context.source !== undefined) {
                    hasSource = true;
                }
                return value;
            });
            hasSource
        ");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void JSONParse_WithoutReviver_StillWorks()
    {
        var result = _context.Eval(@"JSON.parse('{""x"": 42}').x");
        Assert.Equal(42.0, result.DoubleValue);
    }

    // ===============================================================
    // §4.9  Map.getOrInsert / getOrInsertComputed
    // ===============================================================

    [Fact]
    public void Map_GetOrInsert_IsFunction()
    {
        var result = _context.Eval("typeof Map.prototype.getOrInsert");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Map_GetOrInsert_InsertsWhenMissing()
    {
        var result = _context.Eval(@"
            var m = new Map();
            m.getOrInsert('key', 42)
        ");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void Map_GetOrInsert_ReturnsExisting()
    {
        var result = _context.Eval(@"
            var m = new Map();
            m.set('key', 10);
            m.getOrInsert('key', 42)
        ");
        Assert.Equal(10.0, result.DoubleValue);
    }

    [Fact]
    public void Map_GetOrInsert_ValueIsStored()
    {
        var result = _context.Eval(@"
            var m = new Map();
            m.getOrInsert('key', 42);
            m.get('key')
        ");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void Map_GetOrInsertComputed_IsFunction()
    {
        var result = _context.Eval("typeof Map.prototype.getOrInsertComputed");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Map_GetOrInsertComputed_InsertsWhenMissing()
    {
        var result = _context.Eval(@"
            var m = new Map();
            m.getOrInsertComputed('key', function(k) { return k + '_value'; })
        ");
        Assert.Equal("key_value", result.ToString());
    }

    [Fact]
    public void Map_GetOrInsertComputed_ReturnsExisting()
    {
        var result = _context.Eval(@"
            var m = new Map();
            m.set('key', 'existing');
            m.getOrInsertComputed('key', function(k) { return 'new'; })
        ");
        Assert.Equal("existing", result.ToString());
    }

    [Fact]
    public void Map_GetOrInsertComputed_DoesNotCallCallbackIfExists()
    {
        var result = _context.Eval(@"
            var m = new Map();
            m.set('key', 'existing');
            var called = false;
            m.getOrInsertComputed('key', function(k) { called = true; return 'new'; });
            called
        ");
        Assert.False(result.BooleanValue);
    }

    [Fact]
    public void WeakMap_GetOrInsert_IsFunction()
    {
        var result = _context.Eval("typeof WeakMap.prototype.getOrInsert");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void WeakMap_GetOrInsert_InsertsWhenMissing()
    {
        var result = _context.Eval(@"
            var wm = new WeakMap();
            var key = {};
            wm.getOrInsert(key, 42)
        ");
        Assert.Equal(42.0, result.DoubleValue);
    }

    [Fact]
    public void WeakMap_GetOrInsert_ReturnsExisting()
    {
        var result = _context.Eval(@"
            var wm = new WeakMap();
            var key = {};
            wm.set(key, 10);
            wm.getOrInsert(key, 42)
        ");
        Assert.Equal(10.0, result.DoubleValue);
    }

    [Fact]
    public void WeakMap_GetOrInsertComputed_IsFunction()
    {
        var result = _context.Eval("typeof WeakMap.prototype.getOrInsertComputed");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void WeakMap_GetOrInsertComputed_InsertsWhenMissing()
    {
        var result = _context.Eval(@"
            var wm = new WeakMap();
            var key = {};
            wm.getOrInsertComputed(key, function(k) { return 99; })
        ");
        Assert.Equal(99.0, result.DoubleValue);
    }

    // ===============================================================
    // §4.11  structuredClone
    // ===============================================================

    [Fact]
    public void StructuredClone_IsFunction()
    {
        var result = _context.Eval("typeof structuredClone");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void StructuredClone_Primitives()
    {
        Assert.Equal(42.0, _context.Eval("structuredClone(42)").DoubleValue);
        Assert.Equal("hello", _context.Eval("structuredClone('hello')").ToString());
        Assert.True(_context.Eval("structuredClone(true)").BooleanValue);
        Assert.True(_context.Eval("structuredClone(null) === null").BooleanValue);
        Assert.True(_context.Eval("structuredClone(undefined) === undefined").BooleanValue);
    }

    [Fact]
    public void StructuredClone_PlainObject()
    {
        var result = _context.Eval(@"
            var obj = { a: 1, b: 'hello', c: true };
            var clone = structuredClone(obj);
            clone.a === 1 && clone.b === 'hello' && clone.c === true && clone !== obj
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void StructuredClone_DeepClone()
    {
        var result = _context.Eval(@"
            var obj = { nested: { x: 10 } };
            var clone = structuredClone(obj);
            clone.nested.x === 10 && clone.nested !== obj.nested
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void StructuredClone_Array()
    {
        var result = _context.Eval(@"
            var arr = [1, 2, { a: 3 }];
            var clone = structuredClone(arr);
            clone[0] === 1 && clone[1] === 2 && clone[2].a === 3 && clone !== arr && clone[2] !== arr[2]
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void StructuredClone_Date()
    {
        var result = _context.Eval(@"
            var d = new Date(2025, 0, 1);
            var clone = structuredClone(d);
            clone.getTime() === d.getTime() && clone !== d
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void StructuredClone_RegExp()
    {
        var result = _context.Eval(@"
            var re = /abc/gi;
            var clone = structuredClone(re);
            clone.source === re.source && clone.flags === re.flags && clone !== re
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void StructuredClone_Map()
    {
        var result = _context.Eval(@"
            var m = new Map([['a', 1], ['b', 2]]);
            var clone = structuredClone(m);
            clone.get('a') === 1 && clone.get('b') === 2 && clone !== m
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void StructuredClone_Set()
    {
        var result = _context.Eval(@"
            var s = new Set([1, 2, 3]);
            var clone = structuredClone(s);
            clone.has(1) && clone.has(2) && clone.has(3) && clone.size === 3 && clone !== s
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void StructuredClone_ArrayBuffer()
    {
        var result = _context.Eval(@"
            var ab = new ArrayBuffer(4);
            var view = new Uint8Array(ab);
            view[0] = 1; view[1] = 2;
            var clone = structuredClone(ab);
            var cloneView = new Uint8Array(clone);
            cloneView[0] === 1 && cloneView[1] === 2 && clone !== ab
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void StructuredClone_ThrowsForFunction()
    {
        Assert.ThrowsAny<Exception>(() =>
        {
            _context.Eval("structuredClone(function() {})");
        });
    }

    [Fact]
    public void StructuredClone_CircularReference()
    {
        var result = _context.Eval(@"
            var obj = { a: 1 };
            obj.self = obj;
            var clone = structuredClone(obj);
            clone.a === 1 && clone.self === clone && clone !== obj
        ");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void StructuredClone_Error()
    {
        var result = _context.Eval(@"
            var e = new Error('test error');
            var clone = structuredClone(e);
            clone.message === 'test error' && clone !== e
        ");
        Assert.True(result.BooleanValue);
    }
}
