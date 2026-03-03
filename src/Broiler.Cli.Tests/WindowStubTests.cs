using YantraJS.Core;

namespace Broiler.Cli.Tests;

/// <summary>
/// Tests for the window/document stubs registered by
/// <see cref="CaptureService.RegisterWindowStub"/> to prevent
/// JSException when scripts access browser globals.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Engine", "Cli")]
public class WindowStubTests
{
    [Fact]
    public void RegisterWindowStub_WindowIsDefined()
    {
        using var context = new JSContext();
        CaptureService.RegisterWindowStub(context);

        var result = context.Eval("typeof window !== 'undefined'");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void RegisterWindowStub_LocalStorageIsDefined()
    {
        using var context = new JSContext();
        CaptureService.RegisterWindowStub(context);

        var result = context.Eval("typeof window.localStorage !== 'undefined'");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void RegisterWindowStub_LocalStorageBracketAccessReturnsUndefined()
    {
        using var context = new JSContext();
        CaptureService.RegisterWindowStub(context);

        var result = context.Eval("window.localStorage['nonexistent'] === undefined");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void RegisterWindowStub_LocalStorageGetItemReturnsNull()
    {
        using var context = new JSContext();
        CaptureService.RegisterWindowStub(context);

        var result = context.Eval("window.localStorage.getItem('missing') === null");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void RegisterWindowStub_MatchMediaReturnsFalseMatches()
    {
        using var context = new JSContext();
        CaptureService.RegisterWindowStub(context);

        var result = context.Eval("window.matchMedia('(prefers-color-scheme: dark)').matches === false");
        Assert.True(result.BooleanValue);
    }

    [Fact]
    public void RegisterWindowStub_DocumentElementClassListAdd()
    {
        using var context = new JSContext();
        CaptureService.RegisterWindowStub(context);

        var result = context.Eval(@"
            document.documentElement.classList.add('dark');
            document.documentElement.classList.contains('dark')");
        Assert.True(result.BooleanValue);
    }

    /// <summary>
    /// Regression test: the exact heise.de script that caused
    /// JSException: 'Cannot get property localStorage of undefined'.
    /// </summary>
    [Fact]
    public void RegisterWindowStub_HeiseColorSchemeScript_DoesNotThrow()
    {
        using var context = new JSContext();
        CaptureService.RegisterWindowStub(context);

        var ex = Record.Exception(() => context.Eval(@"
            var config = JSON.parse(window.localStorage['akwaConfig-v2'] || '{}')
            var scheme = config.colorScheme ? config.colorScheme.scheme : 'auto'
            if (scheme === 'dark' || (scheme === 'auto' && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
              document.documentElement.classList.add('dark')
            }
        "));

        Assert.Null(ex);
    }

    /// <summary>
    /// Regression test for JSException: 'Cannot get property consent of null'.
    /// <c>localStorage.getItem</c> returns null for a missing key, so
    /// <c>JSON.parse(null)</c> yields null and <c>ls.consent</c> throws.
    /// Verifies the exception is raised (correctly) from the JS engine.
    /// </summary>
    [Fact]
    public void RegisterWindowStub_GetItemReturnsNull_JsonParseNullDereferenceThrows()
    {
        using var context = new JSContext();
        CaptureService.RegisterWindowStub(context);

        // getItem for a missing key returns null; JSON.parse(null) → null;
        // accessing a property on null must throw a JSException.
        Assert.ThrowsAny<Exception>(() => context.Eval(@"
            var ls = JSON.parse(window.localStorage.getItem('akwaConfig-v2'))
            if (ls.consent && ls.consent[820]) { }
        "));
    }
}
