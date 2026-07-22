namespace WinUtil.Core.Abstractions;

/// <summary>
/// Port for overlay filesystem actions. Deletion is best-effort (locked files
/// are skipped, matching Remove-Item -Force's non-terminating errors).
/// %Var% environment references are expanded by the adapter.
/// </summary>
public interface IFileSystem
{
    /// <summary>Delete the contents matched by a trailing-\* glob (e.g. "%Temp%\*").</summary>
    void DeleteGlob(string pathWithGlob);

    void EnsureDirectory(string path);
}
