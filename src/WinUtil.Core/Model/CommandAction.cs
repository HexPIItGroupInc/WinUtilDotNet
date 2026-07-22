namespace WinUtil.Core.Model;

/// <summary>
/// A direct executable invocation (powercfg, netsh, DISM, …) declared by the
/// native overlay (ADR-0004). Runs the file directly — never through a shell,
/// never through PowerShell. Environment variables in %Name% form are expanded
/// by the runner.
/// </summary>
public sealed record CommandAction
{
    public required string File { get; init; }

    public string Args { get; init; } = "";

    /// <summary>Tolerate non-zero exit (e.g. taskkill when the process wasn't running).</summary>
    public bool IgnoreExitCode { get; init; }

    /// <summary>
    /// Fire-and-forget: start the process and do not wait for it. Required for
    /// shell relaunches (explorer.exe) that become long-lived and would
    /// otherwise block the caller indefinitely.
    /// </summary>
    public bool Background { get; init; }
}
