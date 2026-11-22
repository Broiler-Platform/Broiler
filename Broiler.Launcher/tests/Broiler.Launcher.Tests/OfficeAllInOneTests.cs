using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Broiler.Graphics.Geometry;
using Broiler.Launcher.Cli;
using Broiler.Launcher.Installation;
using Broiler.Launcher.Models;
using Broiler.Launcher.Services;
using Broiler.Launcher.UI;
using Xunit;

namespace Broiler.Launcher.Tests;

public sealed class OfficeAllInOneTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "broiler-office-aio-tests-" + Guid.NewGuid().ToString("N"));

    public OfficeAllInOneTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        BuiltInAppRegistry.Clear();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private sealed class MemorySettingsStore : ILauncherSettingsStore
    {
        private LauncherSettings _settings = new();
        public string SettingsFilePath => "in-memory";
        public string? LastLoadError => null;
        public LauncherSettings Load() => _settings.Clone();
        public void Save(LauncherSettings settings) => _settings = settings.Clone();
    }

    [Fact]
    public void CreateOfficeSuite_WithoutBrowser_ContainsMailWriterCellPlate_OnWindowsAndLinux()
    {
        var apps = BroilerAppInfo.CreateOfficeSuite(includeBrowser: false);

        Assert.Equal(4, apps.Count);
        Assert.DoesNotContain(apps, a => a.Id == BroilerAppId.Browser);
        Assert.Equal(
            [BroilerAppId.Mail, BroilerAppId.Writer, BroilerAppId.Cell, BroilerAppId.Plate],
            apps.Select(a => a.Id).ToArray());
        Assert.All(apps, a => Assert.True(a.SupportsLinux));
    }

    [Fact]
    public void CreateOfficeSuite_WithBrowser_ContainsAllFiveApps()
    {
        var apps = BroilerAppInfo.CreateOfficeSuite(includeBrowser: true);

        Assert.Equal(5, apps.Count);
        Assert.Equal(
            [BroilerAppId.Browser, BroilerAppId.Mail, BroilerAppId.Writer, BroilerAppId.Cell, BroilerAppId.Plate],
            apps.Select(a => a.Id).ToArray());
    }

    [Fact]
    public void AppLauncherService_OpensBuiltInAppsInProcessWithoutSpawningProcess()
    {
        var apps = BroilerAppInfo.CreateOfficeSuite(includeBrowser: false);
        var opened = new List<(BroilerAppId Id, string[] Args)>();
        var builtIns = apps.ToDictionary(a => a.Id, _ => (Func<string[], int>)(_ => 0));
        var windowOpeners = apps.ToDictionary(
            a => a.Id,
            a => (Action<string[]>)(args => opened.Add((a.Id, args))));
        ProcessStartInfo? capturedStart = null;
        var service = new AppLauncherService(
            apps: apps,
            settingsStore: new MemorySettingsStore(),
            isWindows: true,
            environment: _ => null,
            currentDirectory: _root,
            startProcess: info => { capturedStart = info; return 42; },
            inventory: new EmptyInstallationInventory(),
            builtInApps: builtIns,
            builtInWindowOpeners: windowOpeners);

        foreach (var app in apps)
        {
            AppResolution resolution = service.ResolveApp(app);
            Assert.True(resolution.IsAvailable);
            Assert.True(resolution.IsBuiltIn);
            Assert.False(resolution.LaunchWithDotnet);
            Assert.Equal("Ready (Built-in)", resolution.StatusText);

            LaunchResult launch = service.LaunchApp(app, "--sample");
            Assert.True(launch.Succeeded);
            Assert.Equal(Environment.ProcessId, launch.ProcessId);
            Assert.Null(capturedStart);
            Assert.Equal((app.Id, new[] { "--sample" }).Item1, opened[^1].Id);
            Assert.Equal(["--sample"], opened[^1].Args);
        }

        Assert.Equal(apps.Count, opened.Count);
    }

    [Fact]
    public void LauncherCliHandler_DispatchesBuiltInAppInProcess_IncludingCellsAlias()
    {
        string[]? capturedArgs = null;
        BroilerAppId? invokedApp = null;
        var openerInvoked = false;
        BuiltInAppRegistry.Register(BroilerAppId.Mail, a => { invokedApp = BroilerAppId.Mail; capturedArgs = a; return 11; }, _ => openerInvoked = true);
        BuiltInAppRegistry.Register(BroilerAppId.Cell, a => { invokedApp = BroilerAppId.Cell; capturedArgs = a; return 22; }, _ => openerInvoked = true);

        var apps = BroilerAppInfo.CreateOfficeSuite(includeBrowser: false);
        var service = new AppLauncherService(
            apps: apps,
            settingsStore: new MemorySettingsStore(),
            isWindows: true,
            environment: _ => null,
            currentDirectory: _root,
            inventory: new EmptyInstallationInventory(),
            builtInApps: BuiltInAppRegistry.Snapshot(),
            builtInWindowOpeners: BuiltInAppRegistry.SnapshotWindowOpeners());

        bool handledMail = LauncherCliHandler.TryHandleCli(["--app", "mail", "--data-directory", "/tmp/mail"], out int mailExit, service);
        Assert.True(handledMail);
        Assert.Equal(11, mailExit);
        Assert.Equal(BroilerAppId.Mail, invokedApp);
        Assert.NotNull(capturedArgs);
        Assert.Equal(["--data-directory", "/tmp/mail"], capturedArgs!);
        Assert.False(openerInvoked);
        Assert.True(BuiltInAppRegistry.TryGetWindowOpener(BroilerAppId.Mail, out _));

        bool handledCellsAlias = LauncherCliHandler.TryHandleCli(["--app", "cells", "--offscreen"], out int cellExit, service);
        Assert.True(handledCellsAlias);
        Assert.Equal(22, cellExit);
        Assert.Equal(BroilerAppId.Cell, invokedApp);
        Assert.NotNull(capturedArgs);
        Assert.Equal(["--offscreen"], capturedArgs!);
    }

    [Fact]
    public void LauncherView_SupportsOfficeSuiteLayoutAndFiveAppSuite()
    {
        var appsFive = BroilerAppInfo.CreateOfficeSuite(includeBrowser: true);
        var builtIns = appsFive.ToDictionary(a => a.Id, _ => (Func<string[], int>)(_ => 0));
        var service = new AppLauncherService(
            apps: appsFive,
            settingsStore: new MemorySettingsStore(),
            isWindows: true,
            environment: _ => null,
            currentDirectory: _root,
            inventory: new EmptyInstallationInventory(),
            builtInApps: builtIns);

        using var view = new LauncherView(service);
        view.Measure(new BSize(840, 600));
        view.Arrange(new BRect(0, 0, 840, 600));
    }

    [Fact]
    public void DefaultServiceApps_MatchBuildProfile()
    {
        var service = new AppLauncherService(
            settingsStore: new MemorySettingsStore(),
            inventory: new EmptyInstallationInventory());

        if (OfficeBuild.IsAllInOne)
        {
            Assert.Equal(OfficeBuild.IncludesBrowser ? 5 : 4, service.Apps.Count);
            Assert.Contains(service.Apps, a => a.Id == BroilerAppId.Mail);
            Assert.Equal(OfficeBuild.IncludesBrowser, service.Apps.Any(a => a.Id == BroilerAppId.Browser));
        }
        else
        {
            Assert.Equal(4, service.Apps.Count);
            Assert.Contains(service.Apps, a => a.Id == BroilerAppId.Browser);
        }
    }
}
