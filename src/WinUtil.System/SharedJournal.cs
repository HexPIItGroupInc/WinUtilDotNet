using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace WinUtil.System;

/// <summary>
/// The one journal shared by every WinUtilsDotNet surface (CLI, GUI), under
/// %ProgramData% so machine-scoped tweak history is common to all of them.
/// The directory is granted Users:Modify on creation so any admin context —
/// an elevated GUI, a CLI in an admin prompt, a SYSTEM task — can read and
/// write the same file without an access-denied.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SharedJournal
{
    public static string Path { get; } = global::System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "WinUtilsDotNet",
        "journal.json");

    /// <summary>Ensure the journal directory exists and is writable by all users. Best-effort.</summary>
    public static string EnsureWritable()
    {
        var dir = global::System.IO.Path.GetDirectoryName(Path)!;
        var info = Directory.CreateDirectory(dir);

        try
        {
            var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var security = info.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(
                users,
                FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            info.SetAccessControl(security);
        }
        catch (UnauthorizedAccessException)
        {
            // Not elevated enough to change the ACL — the write may still work
            // if the directory is already permissive; surface the real error
            // only when the journal write itself fails.
        }

        return Path;
    }
}
