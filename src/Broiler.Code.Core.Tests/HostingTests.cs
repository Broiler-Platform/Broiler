using System.Xml.Linq;
using Broiler.Code.Core.Hosting;

namespace Broiler.Code.Core.Tests;

public sealed class HostingTests
{
    [Fact(Timeout = 600000)]
    public void The_Dispatcher_Runs_Posted_Work_On_The_Thread_That_Created_It()
    {
        var dispatcher = new UiThreadDispatcher();
        int uiThread = Environment.CurrentManagedThreadId;
        int ranOn = -1;

        // Posted from a worker, as a classification or a build result would be.
        Task.Run(() => dispatcher.Post(() => ranOn = Environment.CurrentManagedThreadId)).Wait();

        // Nothing ran yet: the whole point is that it waits for the UI thread
        // rather than executing inline on the poster's thread.
        Assert.Equal(-1, ranOn);
        Assert.Equal(1, dispatcher.PendingCount);

        Assert.Equal(1, dispatcher.Drain());
        Assert.Equal(uiThread, ranOn);
    }

    [Fact(Timeout = 600000)]
    public void Check_Access_Is_False_Off_The_Ui_Thread()
    {
        var dispatcher = new UiThreadDispatcher();

        Assert.True(dispatcher.CheckAccess());
        Assert.False(Task.Run(dispatcher.CheckAccess).Result);
    }

    [Fact(Timeout = 600000)]
    public void Draining_Off_The_Ui_Thread_Throws_Rather_Than_Running_Ui_Work_There()
    {
        var dispatcher = new UiThreadDispatcher();
        dispatcher.Post(() => { });

        AggregateException thrown = Assert.Throws<AggregateException>(
            () => Task.Run(() => dispatcher.Drain()).Wait());

        Assert.IsType<InvalidOperationException>(thrown.InnerException);
        Assert.Equal(1, dispatcher.PendingCount);
    }

    [Fact(Timeout = 600000)]
    public void A_Callback_That_Posts_More_Work_Cannot_Starve_The_Frame()
    {
        var dispatcher = new UiThreadDispatcher();
        int ran = 0;

        // A callback that re-posts itself. Draining until empty would spin
        // forever and never return to the frame.
        void Republish()
        {
            ran++;
            dispatcher.Post(Republish);
        }

        dispatcher.Post(Republish);

        Assert.Equal(1, dispatcher.Drain());
        Assert.Equal(1, ran);
        Assert.Equal(1, dispatcher.PendingCount);
    }

    [Fact(Timeout = 600000)]
    public void The_Wake_Callback_Fires_So_A_Host_Knows_To_Drain()
    {
        int woken = 0;
        var dispatcher = new UiThreadDispatcher(() => woken++);

        dispatcher.Post(() => { });

        Assert.Equal(1, woken);
    }

    [Fact(Timeout = 600000)]
    public void Input_Routing_Gives_Every_Event_A_Distinct_Ordered_Header()
    {
        var router = new DesktopInputRouter("test-host");

        Broiler.UI.UiInputEvent first = router.FromText("a");
        Broiler.UI.UiInputEvent second = router.FromText("b");

        // The legacy adapter reused a constant sequence, so two events were
        // indistinguishable in order. These are not.
        Assert.True(second.Header.SequenceNumber > first.Header.SequenceNumber);
        Assert.Equal(first.Header.DeviceId, second.Header.DeviceId);
    }

    [Fact(Timeout = 600000)]
    public void Pointer_And_Keyboard_Events_Come_From_Different_Devices()
    {
        var router = new DesktopInputRouter("test-host");

        Broiler.UI.UiInputEvent key = router.FromText("a");
        Broiler.UI.UiInputEvent pointer = router.FromPointerMove(
            new Broiler.Graphics.BPointerEventArgs(
                new Broiler.Graphics.BPoint(1, 2), Broiler.Graphics.BMouseButtons.None));

        Assert.NotEqual(key.Header.DeviceId, pointer.Header.DeviceId);
    }

    [Theory]
    [InlineData(0x25, "ArrowLeft")]
    [InlineData(0x27, "ArrowRight")]
    [InlineData(0x24, "Home")]
    [InlineData(0x2E, "Delete")]
    [InlineData(0x08, "Backspace")]
    [InlineData(0x0D, "Enter")]
    [InlineData(0x09, "Tab")]
    [InlineData(0x41, "A")]
    [InlineData(0x5A, "Z")]
    [InlineData(0x70, "F1")]
    public void Virtual_Keys_Map_To_The_Names_The_Controls_Switch_On(int virtualKey, string expected) =>
        Assert.Equal(expected, DesktopInputRouter.MapVirtualKey(virtualKey));

    [Fact(Timeout = 600000)]
    public void A_Cancelled_Composition_Is_Not_An_Empty_Commit()
    {
        var router = new DesktopInputRouter("test-host");

        Broiler.UI.UiInputEvent cancelled = router.FromComposition(null, committed: false);
        Broiler.UI.UiInputEvent committed = router.FromComposition("", committed: true);

        // Both carry empty text; only the state tells them apart, and the
        // editor treats them very differently.
        Assert.Equal(
            Broiler.Input.Text.TextCompositionState.Cancelled, cancelled.CompositionState);
        Assert.Equal(
            Broiler.Input.Text.TextCompositionState.Committed, committed.CompositionState);
    }

    /// <summary>
    /// The Phase 2 rule that the heads stay thin. Asserted against the project
    /// files, because a comment saying "no shell logic here" does not stop a
    /// file being added.
    /// </summary>
    [Theory]
    [InlineData("Broiler.Code.Windows")]
    [InlineData("Broiler.Code.Linux")]
    public void A_Desktop_Head_References_Core_And_Carries_No_Shell_Or_Workspace_Logic(string head)
    {
        string directory = RepositoryPath("src", head);
        XDocument project = XDocument.Load(Path.Combine(directory, $"{head}.csproj"));

        string[] references = [.. project.Descendants("ProjectReference")
            .Select(r => ((string?)r.Attribute("Include"))?.Replace('\\', '/'))
            .Where(r => r is not null)
            .Cast<string>()];

        Assert.Contains(references, r => r.EndsWith("Broiler.Code.Core.csproj", StringComparison.Ordinal));

        // A head that referenced the workspace directly would be free to grow
        // its own document handling, which is what "thin" is meant to prevent.
        Assert.DoesNotContain(references, r => r.EndsWith("Broiler.Code.Workspaces.csproj", StringComparison.Ordinal));

        // And no source file may reimplement what Core owns.
        string[] forbidden =
        [
            "class CodeWorkspace", "class SourceBuffer", "class DocumentCoordinator",
            "class SolutionExplorerSource", "class CodeTemplateService", "class DeclaredProjectFile",
        ];
        foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            foreach (string marker in forbidden)
            {
                Assert.False(
                    text.Contains(marker, StringComparison.Ordinal),
                    $"{head} reimplements '{marker}' in {Path.GetFileName(file)}; it belongs to Broiler.Code.Core.");
            }
        }
    }

    /// <summary>
    /// Writer's dispatcher, clipboard fallback, and legacy input adapter must
    /// not reach the Code heads. Each is checked by name because each was a
    /// deliberate decision the roadmap records.
    /// </summary>
    [Theory]
    [InlineData("Broiler.Code.Windows")]
    [InlineData("Broiler.Code.Linux")]
    public void A_Desktop_Head_Carries_None_Of_Writers_Substitutes(string head)
    {
        string directory = RepositoryPath("src", head);
        string[] forbidden = ["ImmediateUiDispatcher", "StandardLegacyGraphicsInputAdapter", "Broiler.Input.Legacy"];

        foreach (string file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly)))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            foreach (string marker in forbidden)
            {
                // The word may appear in a comment explaining why it is absent;
                // what is forbidden is using it.
                bool used = text.Contains($"new {marker}", StringComparison.Ordinal) ||
                    text.Contains($"{marker}.csproj", StringComparison.Ordinal);
                Assert.False(used, $"{head} uses '{marker}' in {Path.GetFileName(file)}.");
            }
        }
    }

    [Fact(Timeout = 600000)]
    public void A_Report_Distinguishes_An_Honest_Gap_From_A_Substitute()
    {
        var withGap = new HostServiceReport("gap",
        [
            new HostService(HostServiceReport.Clipboard, HostServiceQuality.Unavailable, "not implemented"),
        ]);
        var withSubstitute = new HostServiceReport("substitute",
        [
            new HostService(HostServiceReport.Clipboard, HostServiceQuality.Substitute, "in-memory"),
        ]);

        // An unavailable service is a stated gap and is allowed; a substitute
        // reported as support is what the check exists to catch.
        Assert.True(withGap.HasNoSubstitutes);
        Assert.False(withSubstitute.HasNoSubstitutes);
        Assert.Empty(withGap.Supported);
        Assert.Contains("SUBSTITUTE", withSubstitute.Describe(), StringComparison.Ordinal);
    }

    private static string RepositoryPath(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".gitmodules")) &&
                File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return Path.Combine([directory.FullName, .. parts]);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
