using Broiler.UI.TabView;

namespace Broiler.UI.TreeView.Tests;

/// <summary>
/// The TabView document behaviour from ADR 0024. Every capability here is
/// additive, so a host that sets none of it gets the previous behaviour — the
/// existing TabView and Writer suites are the gate on that claim.
/// </summary>
public sealed class TabViewDocumentTests
{
    private sealed class TestTabView : UiTabView;

    [Fact(Timeout = 600000)]
    public void Close_Is_A_Request_The_Control_Does_Not_Act_On()
    {
        using var tabs = new TestTabView();
        tabs.AddTab("doc-1", "One");
        tabs.AddTab("doc-2", "Two");

        var requested = new List<string>();
        tabs.CloseRequested += (_, e) => requested.Add(e.Id);

        tabs.RequestClose("doc-1");

        // The host was asked and did nothing, so the tab is still there. This
        // is what makes Save/Discard/Cancel expressible.
        Assert.Equal(["doc-1"], requested);
        Assert.Equal(2, tabs.Tabs.Count);
        Assert.NotNull(tabs.FindTab("doc-1"));
    }

    [Fact(Timeout = 600000)]
    public void Removing_A_Tab_Moves_Focus_Deterministically()
    {
        using var tabs = new TestTabView();
        tabs.AddTab("a", "A");
        tabs.AddTab("b", "B");
        tabs.AddTab("c", "C");

        tabs.SelectTab("b");
        Assert.True(tabs.RemoveTab("b"));

        // Selection stays at the same index, which is now the following tab.
        Assert.Equal("c", tabs.SelectedTab!.Id);

        tabs.SelectTab("c");
        Assert.True(tabs.RemoveTab("c"));

        // The last tab was closed, so focus falls back to the previous one.
        Assert.Equal("a", tabs.SelectedTab!.Id);

        Assert.True(tabs.RemoveTab("a"));
        Assert.Null(tabs.SelectedTab);
        Assert.Equal(-1, tabs.SelectedIndex);
    }

    [Fact(Timeout = 600000)]
    public void A_Dirty_Tab_Says_So_In_Its_Accessible_Description()
    {
        using var tabs = new TestTabView();
        UiTabItem tab = tabs.AddTab("a", "Program.cs");

        UiSemanticNode clean = tabs.GetSemanticNode();
        Assert.Contains("Program.cs, 1 of 1", clean.Children[0].Name);

        tab.IsDirty = true;

        // Spoken, not only drawn: a marker a screen reader cannot see is a
        // marker half the users do not have.
        Assert.Contains("unsaved changes", tabs.GetSemanticNode().Children[0].Name);
    }

    [Fact(Timeout = 600000)]
    public void A_Renamed_Tab_Keeps_Its_Position_And_Identity()
    {
        using var tabs = new TestTabView();
        tabs.AddTab("a", "A");
        UiTabItem tab = tabs.AddTab("doc", "Old.cs");
        tabs.AddTab("c", "C");
        tabs.SelectTab("doc");

        var changed = new List<string>();
        tabs.TabChanged += (_, e) => changed.Add(e.Tab.Id);

        tab.Header = "New.cs";

        Assert.Equal(["doc"], changed);
        Assert.Equal(1, tabs.Tabs.ToList().FindIndex(t => t.Id == "doc"));
        Assert.Equal("doc", tabs.SelectedTab!.Id);
    }

    [Fact(Timeout = 600000)]
    public void Reordering_Keeps_The_Selection_On_The_Same_Tab()
    {
        using var tabs = new TestTabView();
        tabs.AddTab("a", "A");
        tabs.AddTab("b", "B");
        tabs.AddTab("c", "C");
        tabs.SelectTab("a");

        Assert.True(tabs.MoveTab("a", 2));

        Assert.Equal(["b", "c", "a"], tabs.Tabs.Select(t => t.Id));

        // The selection followed the tab, not the slot. Reordering must not
        // silently activate a different document.
        Assert.Equal("a", tabs.SelectedTab!.Id);
    }

    [Fact(Timeout = 600000)]
    public void A_Host_Can_Refuse_A_Reorder()
    {
        using var tabs = new TestTabView();
        tabs.AddTab("a", "A");
        tabs.AddTab("b", "B");
        tabs.Reordering += (_, e) => e.Cancel = true;

        Assert.False(tabs.MoveTab("a", 1));
        Assert.Equal(["a", "b"], tabs.Tabs.Select(t => t.Id));
    }

    [Fact(Timeout = 600000)]
    public void Overflow_Reports_What_Does_Not_Fit_And_Activation_Brings_It_Back()
    {
        using var tabs = new TestTabView { VisibleTabCapacity = 2 };
        tabs.AddTab("a", "A");
        tabs.AddTab("b", "B");
        tabs.AddTab("c", "C");

        Assert.Equal(["c"], tabs.OverflowTabs.Select(t => t.Id));
        Assert.Contains(tabs.GetSemanticNode().Children, node => node.State.HasFlag(UiSemanticState.Offscreen));

        Assert.True(tabs.SelectTab("c"));

        // The active tab is always reachable, so activating from the overflow
        // scrolls it into the visible strip.
        Assert.Equal("c", tabs.SelectedTab!.Id);
        Assert.DoesNotContain(tabs.OverflowTabs, t => t.Id == "c");
    }

    [Fact(Timeout = 600000)]
    public void Selecting_An_Unknown_Tab_Fails_Rather_Than_Guessing()
    {
        using var tabs = new TestTabView();
        tabs.AddTab("a", "A");

        // A host uses the result to tell "already open, now focused" from
        // "not open, go and open it".
        Assert.False(tabs.SelectTab("missing"));
        Assert.True(tabs.SelectTab("a"));
    }
}
