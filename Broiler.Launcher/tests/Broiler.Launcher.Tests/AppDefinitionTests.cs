using System.Linq;
using Broiler.Launcher.Models;
using Xunit;

namespace Broiler.Launcher.Tests;

public class AppDefinitionTests
{
    [Fact]
    public void DefaultSuite_ContainsFourTargetApplications()
    {
        var apps = BroilerAppInfo.CreateDefaultSuite();

        Assert.Equal(4, apps.Count);
        Assert.Contains(apps, a => a.Id == BroilerAppId.Browser);
        Assert.Contains(apps, a => a.Id == BroilerAppId.Writer);
        Assert.Contains(apps, a => a.Id == BroilerAppId.Cell);
        Assert.Contains(apps, a => a.Id == BroilerAppId.Plate);
    }

    [Theory]
    [InlineData(BroilerAppId.Browser, "Broiler Browser", "Broiler.Browser.Windows.exe")]
    [InlineData(BroilerAppId.Writer, "Broiler Writer", "Broiler.Writer.Windows.exe")]
    [InlineData(BroilerAppId.Cell, "Broiler Cell", "Broiler.Cells.Windows.exe")]
    [InlineData(BroilerAppId.Plate, "Broiler Plate", "Broiler.Plate.Windows.exe")]
    public void AppMetadata_MatchesExpectedConfigurations(BroilerAppId id, string expectedTitle, string expectedWindowsExe)
    {
        var app = BroilerAppInfo.CreateDefaultSuite().Single(a => a.Id == id);

        Assert.Equal(expectedTitle, app.Title);
        Assert.Equal(expectedWindowsExe, app.WindowsExeName);
        Assert.NotEmpty(app.Description);
        Assert.NotEmpty(app.BadgeText);
        Assert.NotEmpty(app.RelativeProbePaths);
    }
}
