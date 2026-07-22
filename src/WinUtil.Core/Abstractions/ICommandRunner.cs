using WinUtil.Core.Model;

namespace WinUtil.Core.Abstractions;

/// <summary>
/// Port for overlay command actions. The implementation must execute the file
/// directly (no shell, no PowerShell) and return the exit code.
/// </summary>
public interface ICommandRunner
{
    int Run(CommandAction command);
}
