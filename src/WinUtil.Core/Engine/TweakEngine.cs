using WinUtil.Core.Abstractions;
using WinUtil.Core.Model;

namespace WinUtil.Core.Engine;

/// <summary>
/// Applies, undoes, and detects tweaks by dispatching their declared changes to
/// ports. Registry and service changes are handled natively; script escape
/// hatches are not executed here (they are the tracked burn-down backlog).
/// </summary>
public sealed class TweakEngine(IRegistry registry, IJournal journal, IServices? services = null, ICommandRunner? commands = null, IAppx? appx = null, IFileSystem? files = null, IHostsBlocker? hosts = null)
{
    /// <summary>
    /// Ground truth from the live system: Applied when every declared change
    /// matches its target, NotApplied when every change matches its original
    /// (or is absent), Drifted otherwise. A tweak with no typed changes is Unknown.
    /// </summary>
    public ActionState Detect(Tweak tweak)
    {
        var applied = 0;
        var notApplied = 0;
        var total = 0;

        foreach (var change in tweak.Registry)
        {
            total++;
            var exists = registry.TryGetValue(change.Path, change.Name, out var current);

            if (exists && current == change.Value)
            {
                applied++;
            }
            else if (!exists || current == change.OriginalValue)
            {
                notApplied++;
            }
        }

        foreach (var change in tweak.Service)
        {
            total++;
            var exists = Services.TryGetStartupType(change.Name, out var current);

            if (exists && current == change.StartupType)
            {
                applied++;
            }
            else if (!exists || current == change.OriginalType)
            {
                notApplied++;
            }
        }

        if (total == 0)
        {
            return ActionState.Unknown;
        }

        if (applied == total)
        {
            return ActionState.Applied;
        }

        if (notApplied == total)
        {
            return ActionState.NotApplied;
        }

        return ActionState.Drifted;
    }

    /// <summary>Apply all typed changes, journaling each value as it actually was first.</summary>
    public void Apply(Tweak tweak)
    {
        foreach (var command in tweak.PreCommands)
        {
            RunCommand(command);
        }

        // Tree deletes come before typed writes: a tweak may flush a key tree
        // and then write fresh values inside it (e.g. Explorer's view Bags).
        foreach (var path in tweak.RegistryDeleteKeys)
        {
            registry.DeleteKeyTree(path);
        }

        foreach (var change in tweak.Registry)
        {
            var existed = registry.TryGetValue(change.Path, change.Name, out var previous);
            journal.Record(new JournalEntry(tweak.Id, change.Path, change.Name, previous, existed));
            registry.SetValue(change.Path, change.Name, change.Value, change.Type);
        }

        foreach (var change in tweak.Service)
        {
            var existed = Services.TryGetStartupType(change.Name, out var previous);
            journal.Record(new JournalEntry(tweak.Id, change.Name, "StartupType", previous, existed, JournalEntry.ServiceKind));
            Services.SetStartupType(change.Name, change.StartupType);
        }

        foreach (var pattern in tweak.AppxRemove)
        {
            var port = appx
                ?? throw new InvalidOperationException("This tweak declares Appx removals but no IAppx adapter was provided.");

            // Removal is inherently one-way; nothing is journaled for these.
            foreach (var fullName in port.FindInstalled(pattern))
            {
                port.Remove(fullName);
            }
        }

        foreach (var dir in tweak.EnsureDirs)
        {
            Files.EnsureDirectory(dir);
        }

        foreach (var glob in tweak.DeletePaths)
        {
            Files.DeleteGlob(glob);
        }

        if (tweak.HostsBlock is { } block)
        {
            Hosts.ApplyBlocklist(block.Url);
        }

        // Post-commands last: effects like flushdns or an explorer restart must
        // observe the typed changes above.
        foreach (var command in tweak.Commands)
        {
            RunCommand(command);
        }
    }

    /// <summary>
    /// Restore from the journal when we applied this tweak on this machine;
    /// fall back to the catalog's declared originals otherwise.
    /// </summary>
    public void Undo(Tweak tweak)
    {
        var journaled = journal.EntriesFor(tweak.Id)
            .ToDictionary(e => (e.Kind, e.Path, e.Name), e => e);

        foreach (var change in tweak.Registry)
        {
            if (journaled.TryGetValue((JournalEntry.RegistryKind, change.Path, change.Name), out var entry))
            {
                if (entry.Existed && entry.PreviousValue is not null)
                {
                    registry.SetValue(change.Path, change.Name, entry.PreviousValue, change.Type);
                }
                else
                {
                    registry.DeleteValue(change.Path, change.Name);
                }
            }
            else if (change.OriginalValue is not null)
            {
                registry.SetValue(change.Path, change.Name, change.OriginalValue, change.Type);
            }
        }

        foreach (var change in tweak.Service)
        {
            if (journaled.TryGetValue((JournalEntry.ServiceKind, change.Name, "StartupType"), out var entry))
            {
                if (entry.Existed && entry.PreviousValue is not null)
                {
                    Services.SetStartupType(change.Name, entry.PreviousValue);
                }
            }
            else if (change.OriginalType is not null)
            {
                Services.SetStartupType(change.Name, change.OriginalType);
            }
        }

        foreach (var path in tweak.UndoRegistryDeleteKeys)
        {
            registry.DeleteKeyTree(path);
        }

        if (tweak.HostsBlock is { } block)
        {
            Hosts.RemoveBlocklist(block.RemoveFromMarker);
        }

        foreach (var command in tweak.UndoCommands)
        {
            RunCommand(command);
        }

        journal.Clear(tweak.Id);
    }

    private void RunCommand(Model.CommandAction command)
    {
        var runner = commands
            ?? throw new InvalidOperationException("This tweak declares overlay commands but no ICommandRunner was provided.");

        var exitCode = runner.Run(command);
        if (exitCode != 0 && !command.IgnoreExitCode)
        {
            throw new InvalidOperationException($"Command '{command.File} {command.Args}' exited with code {exitCode}.");
        }
    }

    private IServices Services => services
        ?? throw new InvalidOperationException("This tweak declares service changes but no IServices adapter was provided.");

    private IFileSystem Files => files
        ?? throw new InvalidOperationException("This tweak declares filesystem actions but no IFileSystem adapter was provided.");

    private IHostsBlocker Hosts => hosts
        ?? throw new InvalidOperationException("This tweak declares a hosts blocklist but no IHostsBlocker adapter was provided.");
}
