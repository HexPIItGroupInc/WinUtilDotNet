namespace WinUtil.Core.Abstractions;

/// <summary>
/// Port to the Windows registry. The only implementation that touches the real
/// registry lives in WinUtil.System; tests use in-memory fakes. Values cross
/// this boundary as strings in the catalog's own representation — the adapter
/// owns conversion to concrete registry kinds.
/// </summary>
public interface IRegistry
{
    /// <summary>Read a value. Returns false when the key or value does not exist.</summary>
    bool TryGetValue(string path, string name, out string? value);

    void SetValue(string path, string name, string value, string type);

    void DeleteValue(string path, string name);

    /// <summary>Delete a key and its entire subtree. Missing keys are a no-op.</summary>
    void DeleteKeyTree(string path);
}
