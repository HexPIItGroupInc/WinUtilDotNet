using System.Management;
using System.Runtime.Versioning;
using WinUtil.Core.Abstractions;

namespace WinUtil.System;

/// <summary>
/// ISystemRestore via the SystemRestore WMI class — the same mechanism
/// Checkpoint-Computer uses, and independent of the removed wmic.exe.
/// Also clears the creation-frequency throttle so the point is not skipped.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSystemRestore : ISystemRestore
{
    private const uint ModifySettings = 12;
    private const uint BeginSystemChange = 100;

    public void CreateRestorePoint(string description)
    {
        try
        {
            using var cls = new ManagementClass(new ManagementScope(@"\\.\root\default"), new ManagementPath("SystemRestore"), null);
            using var args = cls.GetMethodParameters("CreateRestorePoint");
            args["Description"] = description;
            args["RestorePointType"] = ModifySettings;
            args["EventType"] = BeginSystemChange;

            using var result = cls.InvokeMethod("CreateRestorePoint", args, null);
            var code = Convert.ToInt64(result?["ReturnValue"] ?? -1L, global::System.Globalization.CultureInfo.InvariantCulture);
            if (code != 0)
            {
                throw new InvalidOperationException($"CreateRestorePoint returned {code}.");
            }
        }
        catch (global::System.Runtime.InteropServices.COMException e) when ((uint)e.HResult == 0x80070422)
        {
            // ERROR_SERVICE_DISABLED — System Protection is off (Windows 11's default).
            throw new InvalidOperationException(
                "System Restore is turned off on this PC — Windows 11 ships with System Protection disabled by default, " +
                "so there's nowhere to save a restore point. Turn it on in Settings → System → About → System protection " +
                "(select your system drive, Configure, Turn on system protection), then run this again.", e);
        }
    }
}
