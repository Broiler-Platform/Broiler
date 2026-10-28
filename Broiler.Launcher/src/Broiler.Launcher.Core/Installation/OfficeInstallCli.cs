using System.Runtime.InteropServices;

namespace Broiler.Launcher.Installation;

public static class OfficeInstallCli
{
    public static bool TryHandle(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 1 && args[0] == "--office-package-info")
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                demo = OfficeBuild.IsDemo,
                version = typeof(OfficeBuild).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                    .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
            }));
            return true;
        }
        if (args.Length == 0 || args[0] is not ("--office-install" or "--office-repair" or "--office-rollback" or "--office-unregister")) return false;
        try
        {
            if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("Office installation currently supports Windows x64.");
            bool needsPackage = args[0] is "--office-install" or "--office-repair";
            if (args.Length != 2 && !(needsPackage && args.Length == 4 && args[2] == "--package-directory" && Path.IsPathFullyQualified(args[3])))
                throw new ArgumentException("Usage: --office-install|--office-repair <product> [--package-directory <absolute-directory>]; --office-rollback|--office-unregister <product>. These commands target user scope.");
            string id = args[1];
            OfficeCatalog.ValidateId(id);
            var installer = new ProductInstaller(OfficeInstallPaths.CreateDefault(), new UserInstallAccess());
            InstallResult result;
            if (needsPackage)
            {
                var source = args.Length == 4 ? new BundledOfficePackageSource(args[3], OfficeReleaseVerifier.CreateDefault()) : BundledOfficePackageSource.CreateDefault();
                var package = ProductInstallationService.PrepareArchiveAsync(source.Read(id), OfficeInstallPaths.CreateDefault(), null, default).GetAwaiter().GetResult();
                result = installer.InstallAsync(package, InstallScope.User, repair: args[0] == "--office-repair").GetAwaiter().GetResult();
            }
            else if (args[0] == "--office-rollback") result = installer.Rollback(id, InstallScope.User);
            else result = installer.Unregister(id, InstallScope.User);
            Console.WriteLine(result.Message);
            exitCode = result.Succeeded ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); exitCode = 1; }
        return true;
    }
}
