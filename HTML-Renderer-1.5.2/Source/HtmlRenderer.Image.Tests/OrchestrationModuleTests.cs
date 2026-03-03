using System.Reflection;
using TheArtOfDev.HtmlRenderer.Core;
using TheArtOfDev.HtmlRenderer.Image;

namespace HtmlRenderer.Image.Tests;

/// <summary>
/// Tests validating the Phase 3 modular architecture (ADR-008).
/// Ensures HtmlContainerInt lives in the Orchestration assembly
/// and that the rendering pipeline works through the new assembly structure.
/// </summary>
[Collection("Rendering")]
[Trait("Category", "Unit")]
[Trait("Engine", "HtmlRenderer")]
public class OrchestrationModuleTests
{
    [Fact]
    public void HtmlContainerInt_LivesInOrchestrationAssembly()
    {
        var type = typeof(HtmlContainerInt);
        Assert.Equal("HtmlRenderer.Orchestration", type.Assembly.GetName().Name);
    }

    [Fact]
    public void HtmlRendererUtils_LivesInOrchestrationAssembly()
    {
        var type = typeof(HtmlRendererUtils);
        Assert.Equal("HtmlRenderer.Orchestration", type.Assembly.GetName().Name);
    }

    [Fact]
    public void IAdapter_LivesInCoreAssembly()
    {
        // IAdapter is internal, so access it via reflection
        var coreAssembly = typeof(CssData).Assembly;
        var iAdapterType = coreAssembly.GetType("TheArtOfDev.HtmlRenderer.Core.IAdapter");
        Assert.NotNull(iAdapterType);
        Assert.True(iAdapterType.IsInterface);
    }

    [Fact]
    public void ISelectionHandler_LivesInCoreAssembly()
    {
        var coreAssembly = typeof(CssData).Assembly;
        var type = coreAssembly.GetType("TheArtOfDev.HtmlRenderer.Core.ISelectionHandler");
        Assert.NotNull(type);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IHandlerFactory_LivesInCoreAssembly()
    {
        var coreAssembly = typeof(CssData).Assembly;
        var type = coreAssembly.GetType("TheArtOfDev.HtmlRenderer.Core.IHandlerFactory");
        Assert.NotNull(type);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void HtmlContainer_PipelineThroughOrchestration_Works()
    {
        using var container = new HtmlContainer();
        container.AvoidAsyncImagesLoading = true;
        container.SetHtml("<div>Phase 3 module test</div>");

        Assert.NotNull(container.CssData);
    }
}
