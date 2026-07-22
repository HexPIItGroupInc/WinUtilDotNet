using System.Runtime.Versioning;
using Windows.Management.Deployment;
using WinUtil.Core.Abstractions;

namespace WinUtil.System;

/// <summary>
/// IAppx via Windows.Management.Deployment.PackageManager — the same WinRT API
/// Get-AppxPackage/Remove-AppxPackage wrap.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public sealed class WindowsAppx : IAppx
{
    private readonly PackageManager manager = new();

    public IReadOnlyList<string> FindInstalled(string pattern) =>
        [.. manager.FindPackagesForUser("")
            .Where(p => p.Id.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Id.FullName)];

    public void Remove(string packageFullName)
    {
        var result = manager.RemovePackageAsync(packageFullName).AsTask().GetAwaiter().GetResult();
        if (result.ExtendedErrorCode is not null)
        {
            throw new InvalidOperationException($"Removing '{packageFullName}' failed: {result.ErrorText}");
        }
    }
}
