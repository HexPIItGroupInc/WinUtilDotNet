using WinUtil.Core.Abstractions;

namespace WinUtil.System;

/// <summary>
/// IFileSystem over the BCL. Deletion is best-effort per entry, mirroring
/// Remove-Item -Force -Recurse's non-terminating errors on locked files.
/// </summary>
public sealed class NativeFileSystem : IFileSystem
{
    public void DeleteGlob(string pathWithGlob)
    {
        var expanded = Environment.ExpandEnvironmentVariables(pathWithGlob);
        if (!expanded.EndsWith("\\*", StringComparison.Ordinal) && !expanded.EndsWith("/*", StringComparison.Ordinal))
        {
            throw new ArgumentException($"deletePaths entries must end in \\*, got '{pathWithGlob}'.", nameof(pathWithGlob));
        }

        var directory = expanded[..^2];
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        foreach (var subdir in Directory.EnumerateDirectories(directory))
        {
            try
            {
                Directory.Delete(subdir, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public void EnsureDirectory(string path) =>
        Directory.CreateDirectory(Environment.ExpandEnvironmentVariables(path));
}
