using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.RegularExpressions;
using WinUtil.Core.Abstractions;

namespace WinUtil.System;

/// <summary>
/// Computes machine-specific overlay tokens:
///   {{RAM_KB}}                   physical memory in KB
///   {{USER_SID}}                 current user's SID
///   {{APPX_FULLNAME:Package.Id}} full package name of an installed appx (empty if absent)
/// Values are resolved lazily and cached for the process lifetime.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsTokenProvider(IAppx? appx = null) : ITokenProvider
{
    private readonly Dictionary<string, Func<string>> tokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RAM_KB"] = PhysicalMemoryKb,
        ["USER_SID"] = CurrentUserSid,
    };

    private readonly Dictionary<string, string> cache = [];

    public string Resolve(string text) =>
        TokenPattern().Replace(text, match =>
        {
            var name = match.Groups[1].Value;
            var arg = match.Groups[2].Success ? match.Groups[2].Value : null;
            var key = arg is null ? name : $"{name}:{arg}";

            if (cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var value = Compute(name, arg, text);
            cache[key] = value;
            return value;
        });

    private string Compute(string name, string? arg, string text)
    {
        if (arg is not null)
        {
            if (!name.Equals("APPX_FULLNAME", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Unknown parameterized overlay token '{{{{{name}:{arg}}}}}'.", nameof(text));
            }

            var matches = appx?.FindInstalled(arg) ?? [];
            return matches.Count > 0 ? matches[0] : "";
        }

        return tokens.TryGetValue(name, out var compute)
            ? compute()
            : throw new ArgumentException($"Unknown overlay token '{{{{{name}}}}}'.", nameof(text));
    }

    private static string PhysicalMemoryKb()
    {
        // Upstream sums Win32_PhysicalMemory Capacity / 1KB; the installed-memory
        // API returns the same total in KB directly, without WMI.
        if (!GetPhysicallyInstalledSystemMemory(out var totalKb))
        {
            throw new InvalidOperationException("GetPhysicallyInstalledSystemMemory failed.");
        }

        return totalKb.ToString(CultureInfo.InvariantCulture);
    }

    private static string CurrentUserSid() =>
        WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("Could not resolve the current user SID.");

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);

    // {{NAME}} or {{NAME:argument}}
    [GeneratedRegex(@"\{\{(\w+)(?::([^}]+))?\}\}")]
    private static partial Regex TokenPattern();
}
