using System;
using System.IO;
using System.Linq;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Xunit;

namespace Broiler.Launcher.Tests;

public class AppLauncherServiceTests
{
    [Fact]
    public void ResolveApp_WhenBinaryNotFound_ReturnsNotAvailable()
    {
        string emptyTempDir = Path.Combine(Path.GetTempPath(), "BroilerLauncherTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyTempDir);

        try
        {
            var service = new AppLauncherService();
            var app = service.Apps.First(a => a.Id == BroilerAppId.Browser);

            var resolution = service.ResolveApp(app, emptyTempDir);

            // Since emptyTempDir has no binaries, but probe may walk up, if we create a subfolder with custom fake app:
            var customApp = new BroilerAppInfo(
                Id: BroilerAppId.Browser,
                Title: "Fake App",
                ShortName: "Fake",
                Tagline: "Fake",
                Description: "Fake",
                Category: "Test",
                BadgeText: "TEST",
                AccentColor: default,
                WindowsExeName: "NonExistentBinary_123456.exe",
                WindowsDllName: "NonExistentBinary_123456.dll",
                LinuxBinaryName: "NonExistentBinary_123456",
                LinuxDllName: "NonExistentBinary_123456.dll",
                RelativeProbePaths: Array.Empty<string>());

            var customResolution = service.ResolveApp(customApp, emptyTempDir);
            Assert.False(customResolution.IsAvailable);
            Assert.Null(customResolution.ResolvedPath);
            Assert.Equal("Not Found", customResolution.StatusText);
        }
        finally
        {
            if (Directory.Exists(emptyTempDir))
                Directory.Delete(emptyTempDir, true);
        }
    }

    [Fact]
    public void ResolveApp_WhenDirectBinaryExists_ReturnsAvailable()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "BroilerLauncherTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string expectedExe = OperatingSystem.IsWindows() ? "FakeApp.exe" : "FakeApp";
            string binaryPath = Path.Combine(tempDir, expectedExe);
            File.WriteAllText(binaryPath, "fake executable");

            var customApp = new BroilerAppInfo(
                Id: BroilerAppId.Writer,
                Title: "Custom Writer",
                ShortName: "Writer",
                Tagline: "Writer",
                Description: "Writer",
                Category: "Test",
                BadgeText: "WRT",
                AccentColor: default,
                WindowsExeName: "FakeApp.exe",
                WindowsDllName: "FakeApp.dll",
                LinuxBinaryName: "FakeApp",
                LinuxDllName: "FakeApp.dll",
                RelativeProbePaths: Array.Empty<string>());

            var service = new AppLauncherService([customApp]);
            var resolution = service.ResolveApp(customApp, tempDir);

            Assert.True(resolution.IsAvailable);
            Assert.Equal(Path.GetFullPath(binaryPath), resolution.ResolvedPath);
            Assert.False(resolution.LaunchWithDotnet);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveApp_WhenDirectDllExists_MarksLaunchWithDotnet()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "BroilerLauncherTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string dllPath = Path.Combine(tempDir, "FakeApp.dll");
            File.WriteAllText(dllPath, "fake assembly");

            var customApp = new BroilerAppInfo(
                Id: BroilerAppId.Cell,
                Title: "Custom Cell",
                ShortName: "Cell",
                Tagline: "Cell",
                Description: "Cell",
                Category: "Test",
                BadgeText: "CEL",
                AccentColor: default,
                WindowsExeName: "FakeApp_NoExe.exe",
                WindowsDllName: "FakeApp.dll",
                LinuxBinaryName: "FakeApp_NoLinux",
                LinuxDllName: "FakeApp.dll",
                RelativeProbePaths: Array.Empty<string>());

            var service = new AppLauncherService([customApp]);
            var resolution = service.ResolveApp(customApp, tempDir);

            Assert.True(resolution.IsAvailable);
            Assert.Equal(Path.GetFullPath(dllPath), resolution.ResolvedPath);
            Assert.True(resolution.LaunchWithDotnet);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
