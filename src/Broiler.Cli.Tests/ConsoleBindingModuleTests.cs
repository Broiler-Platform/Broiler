using System.Reflection;
using Broiler.HtmlBridge.Dom.Features;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 feature-module extraction of the
/// <c>console</c> API (<see cref="ConsoleBinding"/>). The object and its four sinks
/// (<c>log</c>/<c>warn</c>/<c>error</c>/<c>info</c>) — previously split between the bridge's
/// <c>BuildConsoleObject</c> and four numbered callbacks in the shared
/// JsFunctionCallbacks/Registration.cs grab-bag — are now co-located in one module. It touches no
/// bridge instance state (it only formats arguments and routes them to RenderLogger), so it has no
/// host contract. The behavior characterization exercises the extracted API end-to-end through the
/// bridge with no layout dependency.
/// </summary>
public sealed class ConsoleBindingModuleTests
{
    [Fact]
    public void Console_Feature_Module_Is_Co_Located_And_Internal()
    {
        var moduleType = typeof(ConsoleBinding);
        Assert.Equal("Broiler.HtmlBridge.Dom.Features", moduleType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", moduleType.Assembly.GetName().Name);
        Assert.False(moduleType.IsPublic);

        // The public seam is a single static Build() factory — no host contract, no instance state.
        Assert.NotNull(moduleType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void Console_Object_And_Callbacks_Moved_Off_The_Bridge()
    {
        var bridge = typeof(Broiler.HtmlBridge.DomBridge);
        var moved = new[]
        {
            "BuildConsoleObject",
            "JsRegistrationLog156Core",
            "JsRegistrationWarn157Core",
            "JsRegistrationError158Core",
            "JsRegistrationInfo159Core",
        };

        foreach (var name in moved)
        {
            Assert.Null(bridge.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        }
    }

    [Fact]
    public void Console_Methods_Are_Callable_Through_The_Bridge_And_Return_Undefined()
    {
        var html = @"<!DOCTYPE html>
<html><body>
<script>
var out =
  'log=' + (console.log('a', 1, true) === undefined) +
  '|warn=' + (console.warn('b') === undefined) +
  '|error=' + (console.error('c') === undefined) +
  '|info=' + (console.info('d') === undefined) +
  '|type=' + (typeof console.log);
var el = document.createElement('div');
el.id = 'result';
el.textContent = out;
document.body.appendChild(el);
</script>
</body></html>";

        var result = CaptureService.ExecuteScriptsWithDom(html, "file:///test.html");

        Assert.Contains("log=true|warn=true|error=true|info=true|type=function", result);
    }
}
