namespace WinUtil.Core.Engine;

/// <summary>
/// What a value actually was on this machine before we changed it. The catalog's
/// OriginalValue is a default; the journal is ground truth for undo.
/// For registry entries Path/Name identify the value; for service entries
/// Path is the service name and Name is "StartupType".
/// </summary>
public sealed record JournalEntry(string TweakId, string Path, string Name, string? PreviousValue, bool Existed, string Kind = JournalEntry.RegistryKind)
{
    public const string RegistryKind = "registry";
    public const string ServiceKind = "service";
}

/// <summary>Store for journal entries. File-backed implementation to follow; tests use in-memory.</summary>
public interface IJournal
{
    void Record(JournalEntry entry);

    IReadOnlyList<JournalEntry> EntriesFor(string tweakId);

    void Clear(string tweakId);
}

public sealed class InMemoryJournal : IJournal
{
    private readonly Dictionary<string, List<JournalEntry>> entries = [];

    public void Record(JournalEntry entry)
    {
        if (!entries.TryGetValue(entry.TweakId, out var list))
        {
            entries[entry.TweakId] = list = [];
        }

        list.Add(entry);
    }

    public IReadOnlyList<JournalEntry> EntriesFor(string tweakId) =>
        entries.TryGetValue(tweakId, out var list) ? list : [];

    public void Clear(string tweakId) => entries.Remove(tweakId);
}
