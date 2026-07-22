namespace WinUtil.Core.Abstractions;

/// <summary>
/// The migration escape hatch (ADR-0001): executes a catalog InvokeScript/UndoScript
/// entry via PowerShell. Every use of this port is a tracked liability — the
/// "PowerShell-free %" metric exists to burn these down to zero.
/// </summary>
public interface IScriptRunner
{
    /// <summary>Run one script block; returns the process exit code.</summary>
    int Run(string script);
}
