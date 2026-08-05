using System;
using System.Runtime.Versioning;
using Broiler.Code.Core.Hosting;

namespace Broiler.Code.Windows;

[SupportedOSPlatform("windows7.0")]
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // --services prints what the head provides and exits. It is how the
        // support claim is checked from a script or a test without opening a
        // window, and it prints substitutes and gaps as loudly as it prints
        // what works.
        if (Array.IndexOf(args, "--services") >= 0)
        {
            HostServiceReport report = CodeHost.DescribeServices();
            Console.WriteLine(report.Describe());
            return report.HasNoSubstitutes ? 0 : 1;
        }

        string? workspacePath = Array.Find(args, argument => !argument.StartsWith('-'));

        using var window = new CodeWindow();
        return CodeHost.Run(window, workspacePath);
    }
}
