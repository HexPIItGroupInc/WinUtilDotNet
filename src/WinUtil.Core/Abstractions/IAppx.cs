namespace WinUtil.Core.Abstractions;

/// <summary>
/// Port to the Appx/MSIX package system (Windows.Management.Deployment).
/// Patterns are case-insensitive substrings of the package identity name.
/// </summary>
public interface IAppx
{
    /// <summary>Full names of installed packages whose identity name matches the pattern.</summary>
    IReadOnlyList<string> FindInstalled(string pattern);

    void Remove(string packageFullName);
}
