using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.RegularExpressions;
using WinUtil.Core.Abstractions;

namespace WinUtil.System;

/// <summary>
/// Computes machine-specific overlay tokens. Values are resolved lazily and
/// cached for the process lifetime.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsTokenProvider : ITokenProvider
{
    private readonly Dictionary<string, Func<string>> tokens;
    private readonly Dictionary<string, string> cache = [];

    public WindowsTokenProvider()
    {
        tokens = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["RAM_KB"] = PhysicalMemoryKb,
            ["USER_SID"] = CurrentUserSid,
        };
    }

    public string Resolve(string text) =>
        TokenPattern().Replace(text, match =>
        {
            var name = match.Groups[1].Value;
            if (!tokens.TryGetValue(name, out var compute))
            {
                throw new ArgumentException($"Unknown overlay token '{{{{{name}}}}}'.", nameof(text));
            }

            if (!cache.TryGetValue(name, out var value))
            {
                cache[name] = value = compute();
            }

            return value;
        });

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

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex TokenPattern();
}
