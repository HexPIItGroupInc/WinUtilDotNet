using WinUtil.Core.Abstractions;
using WinUtil.Core.Model;

namespace WinUtil.Core.Engine;

/// <summary>
/// Applies, undoes, and detects tweaks by dispatching their declared changes to
/// ports. Registry changes are handled natively; script escape hatches are not
/// executed here yet (Phase 1 wires IScriptRunner behind an explicit opt-in).
/// </summary>
public sealed class TweakEngine(IRegistry registry, IJournal journal)
{
    /// <summary>
    /// Ground truth from the live system: Applied when every declared change
    /// matches its target, NotApplied when every change matches its original
    /// (or is absent), Drifted otherwise. A tweak with no typed changes is Unknown.
    /// </summary>
    public ActionState Detect(Tweak tweak)
    {
        if (tweak.Registry.Count == 0)
        {
            return ActionState.Unknown;
        }

        var applied = 0;
        var notApplied = 0;

        foreach (var change in tweak.Registry)
        {
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

        if (applied == tweak.Registry.Count)
        {
            return ActionState.Applied;
        }

        if (notApplied == tweak.Registry.Count)
        {
            return ActionState.NotApplied;
        }

        return ActionState.Drifted;
    }

    /// <summary>Apply all typed changes, journaling each value as it actually was first.</summary>
    public void Apply(Tweak tweak)
    {
        foreach (var change in tweak.Registry)
        {
            var existed = registry.TryGetValue(change.Path, change.Name, out var previous);
            journal.Record(new JournalEntry(tweak.Id, change.Path, change.Name, previous, existed));
            registry.SetValue(change.Path, change.Name, change.Value, change.Type);
        }
    }

    /// <summary>
    /// Restore from the journal when we applied this tweak on this machine;
    /// fall back to the catalog's OriginalValue otherwise.
    /// </summary>
    public void Undo(Tweak tweak)
    {
        var journaled = journal.EntriesFor(tweak.Id)
            .ToDictionary(e => (e.Path, e.Name), e => e);

        foreach (var change in tweak.Registry)
        {
            if (journaled.TryGetValue((change.Path, change.Name), out var entry))
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

        journal.Clear(tweak.Id);
    }
}
