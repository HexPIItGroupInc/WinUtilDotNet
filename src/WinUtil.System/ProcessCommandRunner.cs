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
        var file = Environment.ExpandEnvironmentVariables(command.File);

        // A '*' in the path is a version glob (e.g. Edge's Application\<ver>\...).
        // Resolve to the newest match, upstream's "Select-Object -Last 1"; if
        // nothing matches, the target isn't installed — nothing to do.
        if (file.Contains('*', StringComparison.Ordinal))
        {
            file = ResolveGlob(file);
            if (file is null)
            {
                return 0;
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = Environment.ExpandEnvironmentVariables(command.Args),
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (global::System.ComponentModel.Win32Exception) when (command.IgnoreExitCode)
        {
            // Executable not found (e.g. wmic on modern Windows) and the caller
            // tolerates failure — report non-zero, don't crash.
            return 9009;
        }

        if (process is null)
        {
            throw new InvalidOperationException($"Failed to start '{psi.FileName}'.");
        }

        using (process)
        {
            if (command.Background)
            {
                // Shell relaunch etc. — started, not awaited.
                return 0;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
    }

    private static string? ResolveGlob(string pattern)
    {
        // Split on the first path segment containing '*': fixed root + glob tail.
        var star = pattern.IndexOf('*', StringComparison.Ordinal);
        var lastSep = pattern.LastIndexOf('\\', star);
        if (lastSep < 0)
        {
            return null;
        }

        var root = pattern[..lastSep];
        var tail = pattern[(lastSep + 1)..];
        if (!Directory.Exists(root))
        {
            return null;
        }

        // tail may itself contain further path segments after the wildcard dir.
        var slash = tail.IndexOf('\\', StringComparison.Ordinal);
        var dirPattern = slash < 0 ? tail : tail[..slash];
        var rest = slash < 0 ? null : tail[(slash + 1)..];

        foreach (var dir in Directory.GetDirectories(root, dirPattern).OrderByDescending(d => d))
        {
            var candidate = rest is null ? dir : Path.Combine(dir, rest);
            if (rest is null || File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
