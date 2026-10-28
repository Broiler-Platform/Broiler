using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Broiler.Launcher.Installation;

[SupportedOSPlatform("windows")]
public static class WindowsInstallSecurity
{
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier SystemAccount = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);
    private const FileSystemRights Writable = FileSystemRights.WriteData | FileSystemRights.AppendData |
        FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes | FileSystemRights.Delete |
        FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void CreatePrivateStaging(string path)
    {
        OfficeInstallPaths.RejectLinks(path);
        var acl = CreateAcl(includeUsers: false);
        // The stage's parent is protected already. No ordinary user can read a source snapshot
        // while its hash is being checked, even if the caller names an administrator-readable file.
        if (Directory.Exists(path)) throw new IOException("Staging directory already exists.");
        acl.CreateDirectory(path);
    }

    public static void MakePayloadReadable(string path)
    {
        OfficeInstallPaths.RejectLinks(path);
        new DirectoryInfo(path).SetAccessControl(CreateAcl(includeUsers: true));
        foreach (string child in Directory.EnumerateFileSystemEntries(path))
        {
            OfficeInstallPaths.RejectLinks(child);
            if (Directory.Exists(child)) MakePayloadReadable(child);
            else
            {
                var acl = new FileSecurity();
                acl.SetOwner(Administrators);
                acl.SetAccessRuleProtection(true, false);
                acl.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, AccessControlType.Allow));
                acl.AddAccessRule(new FileSystemAccessRule(SystemAccount, FileSystemRights.FullControl, AccessControlType.Allow));
                acl.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
                new FileInfo(child).SetAccessControl(acl);
            }
        }
    }

    private static DirectorySecurity CreateAcl(bool includeUsers)
    {
        var acl = new DirectorySecurity();
        acl.SetOwner(Administrators);
        acl.SetAccessRuleProtection(true, false);
        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        acl.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(SystemAccount, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
        if (includeUsers) acl.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, inheritance, PropagationFlags.None, AccessControlType.Allow));
        return acl;
    }

    public static bool IsProtected(string path)
    {
        try
        {
            OfficeInstallPaths.RejectLinks(path);
            string boundary = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Broiler");
            string full = Path.GetFullPath(path);
            if (!full.Equals(boundary, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
            for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
            {
                FileSystemSecurity security = Directory.Exists(current)
                    ? new DirectoryInfo(current).GetAccessControl()
                    : new FileInfo(current).GetAccessControl();
                var owner = security.GetOwner(typeof(SecurityIdentifier));
                if (!Administrators.Equals(owner) && !SystemAccount.Equals(owner)) return false;
                foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                    if (rule.AccessControlType == AccessControlType.Allow && (rule.FileSystemRights & Writable) != 0 &&
                        !Administrators.Equals(rule.IdentityReference) && !SystemAccount.Equals(rule.IdentityReference)) return false;
                if (current.Equals(boundary, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return false;
    }

    public static void PrepareMachineRoot(OfficeInstallPaths paths)
    {
        if (!IsAdministrator()) throw new UnauthorizedAccessException("Administrator approval is required.");
        using (var identity = WindowsIdentity.GetCurrent())
            if (identity.Owner is not { } owner || (!Administrators.Equals(owner) && !SystemAccount.Equals(owner)))
                throw new UnauthorizedAccessException("The elevated token must create administrator-owned files; this Windows token owner policy is unsupported.");
        var defaults = OfficeInstallPaths.CreateDefault();
        if (!paths.MachineRoot.Equals(defaults.MachineRoot, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("The helper only writes to the fixed Office machine root.");
        foreach (string directory in new[] { Path.GetDirectoryName(paths.MachineRoot)!, paths.MachineRoot })
        {
            OfficeInstallPaths.RejectLinks(directory);
            if (Directory.Exists(directory))
            {
                if (!IsProtected(directory)) throw new UnauthorizedAccessException("Existing Office machine directories are not administrator-owned/read-only to users; repair their permissions before installation.");
                continue;
            }
            var acl = new DirectorySecurity();
            acl.SetOwner(Administrators);
            acl.SetAccessRuleProtection(true, false);
            var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            acl.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(SystemAccount, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            acl.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, inheritance, PropagationFlags.None, AccessControlType.Allow));
            acl.CreateDirectory(directory);
            if (!IsProtected(directory)) throw new UnauthorizedAccessException("Could not establish protected machine installation permissions.");
        }
        foreach (string name in new[] { "staging", "inventory", "locks", "products" })
        {
            string existing = Path.Combine(paths.MachineRoot, name);
            if (Directory.Exists(existing) && !IsProtected(existing))
                throw new UnauthorizedAccessException("Existing Office machine subdirectories have unsafe permissions.");
        }
    }
}

public sealed class WindowsMachineInstallAccess : IInstallAccess
{
    public void CreateStaging(string path)
    {
        ValidateDestination(path);
        if (OperatingSystem.IsWindows()) WindowsInstallSecurity.CreatePrivateStaging(path);
        else throw new PlatformNotSupportedException();
    }
    public void PrepareActivation(string payload)
    {
        ValidateDestination(payload);
        if (OperatingSystem.IsWindows()) WindowsInstallSecurity.MakePayloadReadable(payload);
        else throw new PlatformNotSupportedException();
    }
    public void ValidateDestination(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new UnauthorizedAccessException();
        string root = OfficeInstallPaths.CreateDefault().MachineRoot;
        string full = Path.GetFullPath(path);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException();
        while (!Directory.Exists(full) && !File.Exists(full)) full = Path.GetDirectoryName(full)!;
        if (!WindowsInstallSecurity.IsProtected(full)) throw new UnauthorizedAccessException("Unsafe permissions in the machine destination path.");
    }
    public void Prepare(InstallScope scope, OfficeInstallPaths paths)
    {
        if (scope != InstallScope.Machine || !OperatingSystem.IsWindows()) throw new UnauthorizedAccessException("This helper only supports Windows machine operations.");
        WindowsInstallSecurity.PrepareMachineRoot(paths);
    }
}
