using WinUtil.Core.Abstractions;

namespace WinUtil.Core.Tests;

/// <summary>In-memory IRegistry for engine tests.</summary>
public sealed class FakeRegistry : IRegistry
{
    private readonly Dictionary<(string Path, string Name), string> values = [];

    public bool TryGetValue(string path, string name, out string? value)
    {
        if (values.TryGetValue((path, name), out var found))
        {
            value = found;
            return true;
        }

        value = null;
        return false;
    }

    public void SetValue(string path, string name, string value, string type) =>
        values[(path, name)] = value;

    public void DeleteValue(string path, string name) =>
        values.Remove((path, name));

    public void DeleteKeyTree(string path)
    {
        foreach (var key in values.Keys.Where(k =>
                     k.Path.Equals(path, StringComparison.OrdinalIgnoreCase)
                     || k.Path.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            values.Remove(key);
        }
    }
}
