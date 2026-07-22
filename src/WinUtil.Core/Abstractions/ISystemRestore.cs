namespace WinUtil.Core.Abstractions;

/// <summary>
/// Port for creating a System Restore checkpoint. The Windows adapter uses the
/// SystemRestore WMI class (what Checkpoint-Computer wraps) — no PowerShell,
/// and no dependency on the now-removed wmic.exe.
/// </summary>
public interface ISystemRestore
{
    void CreateRestorePoint(string description);
}
