using Broiler.JavaScript.Core.Core;
using Broiler.JavaScript.Core.Debugger;

namespace Broiler.JavaScript.Core.Tests;

/// <summary>
/// Tests for the <see cref="IDebugger"/> interface and the debugger
/// integration with <see cref="JSContext"/>.
/// Verifies that the debugger hook is optional, that custom implementations
/// receive notifications, and that the V8 inspector base class satisfies
/// the interface contract.
/// </summary>
[Collection("DebuggerTests")]
public class DebuggerTests : IDisposable
{
    private readonly JSContext _context;

    public DebuggerTests()
    {
        _context = new JSContext();
        JSContext.CurrentContext = _context;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ---------------------------------------------------------------
    // IDebugger contract
    // ---------------------------------------------------------------

    [Fact]
    public void IDebugger_IsImplementedByJSDebugger()
    {
        // JSDebugger (abstract) implements IDebugger
        Assert.True(typeof(IDebugger).IsAssignableFrom(typeof(JSDebugger)));
    }

    [Fact]
    public void JSContext_Debugger_DefaultsToNull()
    {
        // By default no debugger is attached
        Assert.Null(_context.Debugger);
    }

    [Fact]
    public void JSContext_Eval_WorksWithoutDebugger()
    {
        // Eval should succeed when no debugger is attached
        var result = _context.Eval("1 + 2");
        Assert.Equal(3d, result.DoubleValue);
    }

    // ---------------------------------------------------------------
    // Custom debugger receives notifications
    // ---------------------------------------------------------------

    [Fact]
    public void CustomDebugger_ReceivesScriptParsed()
    {
        var recorder = new RecordingDebugger();
        _context.Debugger = recorder;

        _context.Eval("var x = 42;");

        Assert.Single(recorder.ParsedScripts);
        Assert.Contains("var x = 42;", recorder.ParsedScripts[0].Code);
    }

    [Fact]
    public void CustomDebugger_ReceivesContextId()
    {
        var recorder = new RecordingDebugger();
        _context.Debugger = recorder;

        _context.Eval("1");

        Assert.Single(recorder.ParsedScripts);
        Assert.Equal(_context.ID, recorder.ParsedScripts[0].ContextId);
    }

    [Fact]
    public void CustomDebugger_ReceivesCodeFilePath()
    {
        var recorder = new RecordingDebugger();
        _context.Debugger = recorder;

        _context.Eval("1", "test.js");

        Assert.Single(recorder.ParsedScripts);
        Assert.Equal("test.js", recorder.ParsedScripts[0].FilePath);
    }

    [Fact]
    public void CustomDebugger_ReceivesMultipleScripts()
    {
        var recorder = new RecordingDebugger();
        _context.Debugger = recorder;

        _context.Eval("1");
        _context.Eval("2");
        _context.Eval("3");

        Assert.Equal(3, recorder.ParsedScripts.Count);
    }

    [Fact]
    public void CustomDebugger_CanBeSwapped()
    {
        var recorder1 = new RecordingDebugger();
        var recorder2 = new RecordingDebugger();

        _context.Debugger = recorder1;
        _context.Eval("1");

        _context.Debugger = recorder2;
        _context.Eval("2");

        Assert.Single(recorder1.ParsedScripts);
        Assert.Single(recorder2.ParsedScripts);
    }

    [Fact]
    public void CustomDebugger_CanBeDetached()
    {
        var recorder = new RecordingDebugger();
        _context.Debugger = recorder;
        _context.Eval("1");

        _context.Debugger = null;
        _context.Eval("2");

        // Only the first script should have been recorded
        Assert.Single(recorder.ParsedScripts);
    }

    // ---------------------------------------------------------------
    // JSDebugger.Break static event
    // ---------------------------------------------------------------

    [Fact]
    public void JSDebugger_RaiseBreak_FiresEvent()
    {
        var fired = false;
        EventHandler handler = (s, e) => fired = true;
        JSDebugger.Break += handler;
        try
        {
            JSDebugger.RaiseBreak();
            Assert.True(fired);
        }
        finally
        {
            JSDebugger.Break -= handler;
        }
    }

    [Fact]
    public void JSDebugger_RaiseBreak_ReturnsNull()
    {
        Assert.Null(JSDebugger.RaiseBreak());
    }

    // ---------------------------------------------------------------
    // Helper: recording debugger
    // ---------------------------------------------------------------

    private sealed class RecordingDebugger : IDebugger
    {
        public record ParsedScript(long ContextId, string Code, string FilePath);

        public List<ParsedScript> ParsedScripts { get; } = new();
        public List<JSValue> ReportedExceptions { get; } = new();

        public void ReportException(JSValue error) => ReportedExceptions.Add(error);

        public void ScriptParsed(long id, string code, string codeFilePath)
            => ParsedScripts.Add(new ParsedScript(id, code, codeFilePath));
    }
}
