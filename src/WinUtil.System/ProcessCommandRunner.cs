using System.Diagnostics;
using System.Runtime.Versioning;
using WinUtil.Core.Abstractions;
using WinUtil.Core.Model;

namespace WinUtil.System;

/// <summary>
/// ICommandRunner via direct process start — no shell, no PowerShell.
/// %Var% environment references in file and args are expanded here.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ProcessCommandRunner : ICommandRunner
{
    public int Run(CommandAction command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Environment.ExpandEnvironmentVariables(command.File),
            Arguments = Environment.ExpandEnvironmentVariables(command.Args),
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start '{psi.FileName}'.");

        if (command.Background)
        {
            // Shell relaunch etc. — started, not awaited.
            return 0;
        }

        process.WaitForExit();
        return process.ExitCode;
    }
}
