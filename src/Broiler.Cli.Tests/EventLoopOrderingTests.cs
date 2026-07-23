using System.Collections.Generic;
using Broiler.HtmlBridge;
using Xunit;

namespace Broiler.Cli.Tests;

/// <summary>
/// Event-loop ordering EL-3: the render pipeline (<see cref="ScriptEngine"/>) runs the synchronous script
/// phases (regular → deferred → modules) with only microtask checkpoints between them, then fires the window
/// load event, then drains timers to completion. So a timer scheduled by an early script fires <b>after</b>
/// all script execution (in deadline order), not eagerly between scripts as the previous drain-everything-
/// after-each-script model did.
/// </summary>
public sealed class EventLoopOrderingTests
{
    private const string Url = "file:///el3.html";
    private const string Html = "<!DOCTYPE html><html><head></head><body></body></html>";

    [Fact]
    public void Timer_From_Regular_Script_Fires_After_Deferred_Scripts()
    {
        // The regular script schedules a timer that records the value of a marker attribute. A deferred
        // script (which runs after all regular scripts) sets that marker. Under EL-3 the timer fires only
        // after the deferred script, so it observes "deferred-ran"; under the old eager model it fired
        // between the regular and deferred scripts and would have observed "none".
        var engine = new ScriptEngine();
        var regular = new List<string>
        {
            "setTimeout(function(){ " +
            "document.body.setAttribute('data-timer-saw', document.body.getAttribute('data-marker') || 'none'); " +
            "}, 0);"
        };
        var deferred = new List<string>
        {
            "document.body.setAttribute('data-marker', 'deferred-ran');"
        };

        var output = engine.Execute(regular, deferred, Html, Url);

        Assert.NotNull(output);
        Assert.Contains("data-timer-saw=\"deferred-ran\"", output);
    }

    [Fact]
    public void Timers_Still_Fire_And_Settle_By_Capture()
    {
        // A timer's DOM effect is still present in the final render (it fires during the post-load drain).
        var engine = new ScriptEngine();
        var scripts = new List<string>
        {
            "setTimeout(function(){ document.body.setAttribute('data-ran', 'yes'); }, 0);"
        };

        var output = engine.Execute(scripts, new List<string>(), Html, Url);

        Assert.NotNull(output);
        Assert.Contains("data-ran=\"yes\"", output);
    }
}
