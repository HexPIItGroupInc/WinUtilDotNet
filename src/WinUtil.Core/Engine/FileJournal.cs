using System.Text.Json;

namespace WinUtil.Core.Engine;

/// <summary>
/// File-backed journal so undo works across CLI invocations. One JSON file,
/// rewritten on every mutation — journal volumes are tiny (entries per applied
/// tweak) and durability beats cleverness here.
/// </summary>
public sealed class FileJournal : IJournal
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string path;
    private readonly Dictionary<string, List<JournalEntry>> entries;

    public FileJournal(string path)
    {
        this.path = path;
        entries = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, List<JournalEntry>>>(File.ReadAllText(path), Options) ?? []
            : [];
    }

    public void Record(JournalEntry entry)
    {
        if (!entries.TryGetValue(entry.TweakId, out var list))
        {
            entries[entry.TweakId] = list = [];
        }

        list.Add(entry);
        Save();
    }

    public IReadOnlyList<JournalEntry> EntriesFor(string tweakId) =>
        entries.TryGetValue(tweakId, out var list) ? list : [];

    /// <summary>All journaled tweaks and their entries — for provenance display.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<JournalEntry>> Snapshot() =>
        entries.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<JournalEntry>)kv.Value);

    public void Clear(string tweakId)
    {
        if (entries.Remove(tweakId))
        {
            Save();
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(entries, Options));
    }
}
