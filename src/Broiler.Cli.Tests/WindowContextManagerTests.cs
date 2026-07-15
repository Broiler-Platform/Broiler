using System.Reflection;
using Broiler.HtmlBridge;
using Broiler.HtmlBridge.Dom.Runtime;

namespace Broiler.Cli.Tests;

/// <summary>
/// Guards the HtmlBridge complexity-reduction roadmap Phase 3 slice P3.18: the browsing-context
/// window-resolution behaviour (canonicalise/resolve a window and the <c>RunWithWindowContext</c> global
/// switch) is now the single <see cref="WindowContextManager"/> owner, reached by the bridge through the
/// explicit <see cref="IWindowContextHost"/> contract while <c>DomBridge.WindowContext.cs</c> keeps thin
/// delegators. The behaviour itself (cross-window <c>postMessage</c> owner-window resolution and
/// sub-document scripts running under <c>RunWithWindowContext</c>) is covered end-to-end by the existing
/// WebMessaging and SubDocument suites; these guards pin the ownership move.
/// </summary>
public sealed class WindowContextManagerTests
{
    [Fact]
    public void WindowContext_Owner_Is_Internal_And_In_Runtime()
    {
        var ownerType = typeof(WindowContextManager);
        Assert.Equal("Broiler.HtmlBridge.Dom.Runtime", ownerType.Namespace);
        Assert.Equal("Broiler.HtmlBridge.Dom", ownerType.Assembly.GetName().Name);
        Assert.False(ownerType.IsPublic);
        Assert.False(typeof(IWindowContextHost).IsPublic);
    }

    [Fact]
    public void DomBridge_Owns_WindowContext_Through_The_Host_Contract()
    {
        Assert.True(typeof(IWindowContextHost).IsAssignableFrom(typeof(DomBridge)));
        Assert.Contains(
            typeof(DomBridge).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            static field => field.FieldType == typeof(WindowContextManager));

        foreach (var op in new[]
                 {
                     "ResolveCurrentWindow", "ResolveOwnerWindow", "GetCanonicalWindow",
                     "RunWithWindowContext", "GetWindowDocument", "GetWindowParent",
                 })
            Assert.NotNull(typeof(WindowContextManager).GetMethod(op, BindingFlags.Public | BindingFlags.Instance));
    }
}
