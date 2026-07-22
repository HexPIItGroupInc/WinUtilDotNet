using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using WinUtil.Core.Abstractions;

namespace WinUtil.System;

/// <summary>
/// IRegistry against the real Windows registry via Microsoft.Win32 — the API
/// PowerShell's registry provider itself wraps. Catalog paths use PowerShell
/// hive notation ("HKLM:\..."), converted here at the boundary.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistry : IRegistry
{
    public bool TryGetValue(string path, string name, out string? value)
    {
        var (hive, subKey) = Split(path);
        using var key = hive.OpenSubKey(subKey, writable: false);
        var raw = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

        if (raw is null)
        {
            value = null;
            return false;
        }

        // Normalize to the catalog's string representation so comparisons round-trip:
        // byte[] → hex (matches Binary Value strings), string[] → NUL-joined (MultiString).
        value = raw switch
        {
            byte[] bytes => Convert.ToHexString(bytes),
            string[] parts => string.Join('\0', parts),
            _ => Convert.ToString(raw, CultureInfo.InvariantCulture),
        };
        return true;
    }

    public void SetValue(string path, string name, string value, string type)
    {
        var (hive, subKey) = Split(path);
        using var key = hive.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Cannot open or create registry key '{path}'.");

        var kind = ToKind(type);
        object converted = kind switch
        {
            RegistryValueKind.DWord => unchecked((int)Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            RegistryValueKind.QWord => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            RegistryValueKind.Binary => Convert.FromHexString(value),
            RegistryValueKind.MultiString => value.Split('\0', StringSplitOptions.RemoveEmptyEntries),
            _ => value,
        };

        key.SetValue(name, converted, kind);
    }

    public void DeleteValue(string path, string name)
    {
        var (hive, subKey) = Split(path);
        using var key = hive.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public void DeleteKeyTree(string path)
    {
        var (hive, subKey) = Split(path);
        hive.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
    }

    private static (RegistryKey Hive, string SubKey) Split(string path)
    {
        var separator = path.IndexOf('\\', StringComparison.Ordinal);
        if (separator < 0)
        {
            throw new ArgumentException($"Registry path '{path}' has no subkey.", nameof(path));
        }

        var hive = path[..separator].TrimEnd(':').ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKU" or "HKEY_USERS" => Registry.Users,
            "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            var other => throw new ArgumentException($"Unknown registry hive '{other}' in path '{path}'.", nameof(path)),
        };

        return (hive, path[(separator + 1)..]);
    }

    private static RegistryValueKind ToKind(string type) => type switch
    {
        "DWord" => RegistryValueKind.DWord,
        "QWord" => RegistryValueKind.QWord,
        "String" => RegistryValueKind.String,
        "ExpandString" => RegistryValueKind.ExpandString,
        "Binary" => RegistryValueKind.Binary,
        "MultiString" => RegistryValueKind.MultiString,
        _ => throw new ArgumentException($"Unknown registry value type '{type}'.", nameof(type)),
    };
}
