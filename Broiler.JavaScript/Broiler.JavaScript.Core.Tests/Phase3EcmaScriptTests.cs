using System.Threading;

namespace YantraJS.Core.Tests;

/// <summary>
/// ECMAScript conformance tests for Phase 3 features:
/// Import attributes (§2.3), RegExp pattern modifiers (§2.6),
/// Duplicate named capture groups (§2.7), Float16Array / Math.f16round (§2.8).
/// </summary>
public class Phase3EcmaScriptTests : IDisposable
{
    private readonly JSContext _context;
    private readonly SynchronizationContext _previousSyncCtx;

    public Phase3EcmaScriptTests()
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
    // §2.3  Import attributes — parser acceptance
    // ===============================================================

    [Fact]
    public void ImportAttributes_ParserAcceptsWithClause()
    {
        // Verify the parser does not throw on `import ... with { type: "json" }`.
        // We cannot actually resolve a module in a unit test, but we can verify
        // that the parser accepts the syntax by compiling the script.
        var script = @"
            async function loadModule() {
                const data = await import('./data.json', { with: { type: 'json' } });
                return data;
            }
        ";
        // Should not throw — the parser accepts the syntax
        var result = _context.Eval(script);
        Assert.NotNull(result);
    }

    [Fact]
    public void ImportAttributes_ParserAcceptsStaticImportWithClause()
    {
        // Verify the parser can handle the with-clause in a static import.
        // We wrap in try/catch because module resolution will fail, but
        // the parser should not throw a SyntaxError.
        try
        {
            // Static import with attributes — parser should accept this syntax.
            // Module resolution will fail, which is expected.
            _context.Eval("import data from './data.json' with { type: 'json' };");
        }
        catch (Exception ex)
        {
            // Syntax errors are parser failures — these should NOT happen.
            Assert.DoesNotContain("SyntaxError", ex.Message);
            // Other errors (like module not found) are acceptable.
        }
    }

    // ===============================================================
    // §2.6  RegExp pattern modifiers — inline flags
    // ===============================================================

    [Fact]
    public void RegExp_PatternModifiers_InlineCaseInsensitive()
    {
        // (?i:abc) makes 'abc' case-insensitive while 'def' remains case-sensitive
        var result = _context.Eval("new RegExp('(?i:abc)def').test('ABCdef')");
        Assert.Equal("true", result.ToString());
    }

    [Fact]
    public void RegExp_PatternModifiers_InlineCaseSensitiveOuter()
    {
        // Only the inner part is case-insensitive; 'DEF' should NOT match
        var result = _context.Eval("new RegExp('(?i:abc)def').test('ABCDEF')");
        Assert.Equal("false", result.ToString());
    }

    [Fact]
    public void RegExp_PatternModifiers_InlineMultiline()
    {
        // (?m:^abc) makes ^ match at line boundaries within the group
        var result = _context.Eval(@"new RegExp('(?m:^abc)').test('xyz\nabc')");
        Assert.Equal("true", result.ToString());
    }

    [Fact]
    public void RegExp_PatternModifiers_NegateFlag()
    {
        // (?-i:abc) turns OFF case-insensitivity for the sub-expression
        var result = _context.Eval("new RegExp('(?-i:abc)', 'i').test('ABC')");
        Assert.Equal("false", result.ToString());
    }

    [Fact]
    public void RegExp_DotAll_Flag()
    {
        // The 's' flag should make . match newlines
        var result = _context.Eval("new RegExp('a.b', 's').test('a\\nb')");
        Assert.Equal("true", result.ToString());
    }

    [Fact]
    public void RegExp_DotAll_FlagWithoutS()
    {
        // Without 's', dot should NOT match newline
        var result = _context.Eval("new RegExp('a.b').test('a\\nb')");
        Assert.Equal("false", result.ToString());
    }

    // ===============================================================
    // §2.7  Duplicate named capture groups
    // ===============================================================

    [Fact]
    public void RegExp_DuplicateNamedGroups_MatchesFirstAlternative()
    {
        // Same named group in different alternatives — first branch matches
        var result = _context.Eval(@"
            var re = new RegExp('(?<year>\\d{4})-\\d{2}|\\d{2}-(?<year>\\d{2})');
            var m = re.exec('2025-01');
            m.groups.year;
        ");
        Assert.Equal("2025", result.ToString());
    }

    [Fact]
    public void RegExp_DuplicateNamedGroups_MatchesSecondAlternative()
    {
        // Same named group in different alternatives — second branch matches
        var result = _context.Eval(@"
            var re = new RegExp('(?<year>\\d{4})-\\d{2}|\\d{2}-(?<year>\\d{2})');
            var m = re.exec('01-25');
            m.groups.year;
        ");
        Assert.Equal("25", result.ToString());
    }

    [Fact]
    public void RegExp_DuplicateNamedGroups_DoesNotThrow()
    {
        // Creating a regex with duplicate named groups should not throw
        var result = _context.Eval(@"
            var re = new RegExp('(?<val>a)|(?<val>b)');
            typeof re;
        ");
        Assert.Equal("object", result.ToString());
    }

    // ===============================================================
    // §2.8  Float16Array — typed array
    // ===============================================================

    [Fact]
    public void Float16Array_IsFunction()
    {
        var result = _context.Eval("typeof Float16Array");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Float16Array_Constructor_Length()
    {
        var result = _context.Eval("new Float16Array(4).length");
        Assert.Equal("4", result.ToString());
    }

    [Fact]
    public void Float16Array_BYTES_PER_ELEMENT()
    {
        var result = _context.Eval("Float16Array.BYTES_PER_ELEMENT");
        Assert.Equal("2", result.ToString());
    }

    [Fact]
    public void Float16Array_SetAndGet()
    {
        var result = _context.Eval(@"
            var arr = new Float16Array(2);
            arr[0] = 1.5;
            arr[1] = 2.5;
            arr[0] + ',' + arr[1];
        ");
        Assert.Equal("1.5,2.5", result.ToString());
    }

    [Fact]
    public void Float16Array_From()
    {
        var result = _context.Eval("Float16Array.from([1, 2, 3]).length");
        Assert.Equal("3", result.ToString());
    }

    [Fact]
    public void Float16Array_Of()
    {
        var result = _context.Eval(@"
            var arr = Float16Array.of(1.5, 2.5, 3.5);
            arr.length + ',' + arr[0] + ',' + arr[1] + ',' + arr[2];
        ");
        Assert.Equal("3,1.5,2.5,3.5", result.ToString());
    }

    [Fact]
    public void Float16Array_HalfPrecisionRounding()
    {
        // Half-precision has limited precision — values should be rounded
        var result = _context.Eval(@"
            var arr = new Float16Array(1);
            arr[0] = 0.1;
            arr[0] !== 0.1;  // should be true because half-precision rounds 0.1
        ");
        Assert.Equal("true", result.ToString());
    }

    // ===============================================================
    // §2.8  Math.f16round
    // ===============================================================

    [Fact]
    public void Math_f16round_IsFunction()
    {
        var result = _context.Eval("typeof Math.f16round");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void Math_f16round_RoundsToHalfPrecision()
    {
        // Math.f16round should round to the nearest half-precision value
        var result = _context.Eval("Math.f16round(1.5)");
        Assert.Equal("1.5", result.ToString());
    }

    [Fact]
    public void Math_f16round_RoundsSmallValues()
    {
        // 0.1 in half-precision is approximately 0.0999755859375
        var result = _context.Eval("Math.f16round(0.1)");
        var val = double.Parse(result.ToString());
        Assert.NotEqual(0.1, val);
        Assert.InRange(val, 0.099, 0.101);
    }

    [Fact]
    public void Math_f16round_NaN()
    {
        var result = _context.Eval("Math.f16round(NaN)");
        Assert.Equal("NaN", result.ToString());
    }

    [Fact]
    public void Math_f16round_Infinity()
    {
        var result = _context.Eval("Math.f16round(Infinity)");
        Assert.Equal("Infinity", result.ToString());
    }

    [Fact]
    public void Math_f16round_Zero()
    {
        var result = _context.Eval("Math.f16round(0)");
        Assert.Equal("0", result.ToString());
    }

    // ===============================================================
    // §2.8  DataView getFloat16 / setFloat16
    // ===============================================================

    [Fact]
    public void DataView_GetFloat16_IsFunction()
    {
        var result = _context.Eval(@"
            var buf = new ArrayBuffer(4);
            var dv = new DataView(buf);
            typeof dv.getFloat16;
        ");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void DataView_SetFloat16_IsFunction()
    {
        var result = _context.Eval(@"
            var buf = new ArrayBuffer(4);
            var dv = new DataView(buf);
            typeof dv.setFloat16;
        ");
        Assert.Equal("function", result.ToString());
    }

    [Fact]
    public void DataView_SetGetFloat16_RoundTrip()
    {
        var result = _context.Eval(@"
            var buf = new ArrayBuffer(4);
            var dv = new DataView(buf);
            dv.setFloat16(0, 1.5, true);
            dv.getFloat16(0, true);
        ");
        Assert.Equal("1.5", result.ToString());
    }

    [Fact]
    public void DataView_SetGetFloat16_BigEndian()
    {
        var result = _context.Eval(@"
            var buf = new ArrayBuffer(4);
            var dv = new DataView(buf);
            dv.setFloat16(0, 2.5, false);
            dv.getFloat16(0, false);
        ");
        Assert.Equal("2.5", result.ToString());
    }
}
