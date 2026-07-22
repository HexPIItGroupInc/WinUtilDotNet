using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using WinUtil.Core.Abstractions;

namespace WinUtil.System;

/// <summary>
/// IServices against the Service Control Manager. Reads come from the SCM
/// config; writes use ChangeServiceConfig — the same API Set-Service wraps.
/// Startup type names match the catalog: Automatic, AutomaticDelayedStart,
/// Manual, Disabled.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsServices : IServices
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryConfig = 0x0001;
    private const uint ServiceChangeConfig = 0x0002;
    private const uint ServiceNoChange = 0xFFFFFFFF;

    public bool TryGetStartupType(string serviceName, out string? startupType)
    {
        // SCM start types live in the registry; DelayedAutostart disambiguates
        // Automatic vs AutomaticDelayedStart, which QUERY_SERVICE_CONFIG hides.
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
        var start = key?.GetValue("Start");

        if (start is not int startValue)
        {
            startupType = null;
            return false;
        }

        var delayed = key?.GetValue("DelayedAutostart") is int d && d == 1;
        startupType = startValue switch
        {
            2 => delayed ? "AutomaticDelayedStart" : "Automatic",
            3 => "Manual",
            4 => "Disabled",
            var other => other.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
        };
        return true;
    }

    public void SetStartupType(string serviceName, string startupType)
    {
        var (startValue, delayed) = startupType switch
        {
            "Automatic" => (2u, false),
            "AutomaticDelayedStart" => (2u, true),
            "Manual" => (3u, false),
            "Disabled" => (4u, false),
            _ => throw new ArgumentException($"Unknown service startup type '{startupType}'.", nameof(startupType)),
        };

        var scm = OpenSCManagerW(null, null, ScManagerConnect);
        if (scm == IntPtr.Zero)
        {
            throw new Win32Exception();
        }

        try
        {
            var service = OpenServiceW(scm, serviceName, ServiceChangeConfig | ServiceQueryConfig);
            if (service == IntPtr.Zero)
            {
                const int ErrorServiceDoesNotExist = 1060;
                if (Marshal.GetLastWin32Error() == ErrorServiceDoesNotExist)
                {
                    // Not present on this Windows edition — upstream's Set-Service
                    // errors non-terminating here; we skip, matching that.
                    return;
                }

                throw new Win32Exception();
            }

            try
            {
                if (!ChangeServiceConfigW(service, ServiceNoChange, startValue, ServiceNoChange, null, null, IntPtr.Zero, null, null, null, null))
                {
                    throw new Win32Exception();
                }

                var info = delayed ? 1 : 0;
                if (!ChangeServiceConfig2W(service, 3 /* SERVICE_CONFIG_DELAYED_AUTO_START_INFO */, ref info))
                {
                    throw new Win32Exception();
                }
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    [LibraryImport("advapi32.dll", EntryPoint = "OpenSCManagerW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenSCManagerW(string? machineName, string? databaseName, uint access);

    [LibraryImport("advapi32.dll", EntryPoint = "OpenServiceW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenServiceW(IntPtr scManager, string serviceName, uint access);

    [LibraryImport("advapi32.dll", EntryPoint = "ChangeServiceConfigW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfigW(IntPtr service, uint serviceType, uint startType, uint errorControl, string? binaryPath, string? loadOrderGroup, IntPtr tagId, string? dependencies, string? serviceStartName, string? password, string? displayName);

    [LibraryImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfig2W(IntPtr service, uint infoLevel, ref int info);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseServiceHandle(IntPtr handle);
}
