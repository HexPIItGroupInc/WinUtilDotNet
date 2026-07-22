using System.Text.Json;
using WinUtil.Core.Model;

namespace WinUtil.Core.Catalog;

/// <summary>
/// Parses winutil's tweaks.json into the typed domain model. The upstream file
/// is the contract: unknown fields are ignored (they are mostly UI metadata),
/// known fields must round-trip losslessly.
/// </summary>
public static class CatalogLoader
{
    /// <param name="json">Upstream tweaks.json content, byte-for-byte.</param>
    /// <param name="overlayJson">Optional native overlay (ADR-0004): per-tweak command
    /// replacements for script escape hatches. Overlay ids must exist in the catalog —
    /// a stale overlay is a build-breaking contract violation, not a shrug.</param>
    public static IReadOnlyList<Tweak> LoadTweaks(string json, string? overlayJson = null)
    {
        var overlay = overlayJson is null ? [] : LoadOverlay(overlayJson);
        using var doc = JsonDocument.Parse(JsonSanitizer.Sanitize(json));
        var tweaks = new List<Tweak>();

        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var e = entry.Value;
            var over = overlay.TryGetValue(entry.Name, out var o) ? o : null;
            tweaks.Add(new Tweak
            {
                PreCommands = over?.PreApply ?? [],
                Commands = over?.Apply ?? [],
                UndoCommands = over?.Undo ?? [],
                AppxRemove = over?.AppxRemove ?? [],
                DeletePaths = over?.DeletePaths ?? [],
                EnsureDirs = over?.EnsureDirs ?? [],
                HostsBlock = over?.HostsBlock,
                ScriptsCovered = over is not null,
                Id = entry.Name,
                Content = GetString(e, "Content") ?? entry.Name,
                Description = GetString(e, "Description"),
                Category = GetString(e, "category"),
                Panel = GetString(e, "panel"),
                Link = GetString(e, "link"),
                Registry = ReadArray(e, "registry", r => new RegistryChange
                {
                    Path = GetString(r, "Path") ?? throw MissingField(entry.Name, "registry.Path"),
                    Name = GetString(r, "Name") ?? throw MissingField(entry.Name, "registry.Name"),
                    Value = GetString(r, "Value") ?? throw MissingField(entry.Name, "registry.Value"),
                    Type = GetString(r, "Type") ?? throw MissingField(entry.Name, "registry.Type"),
                    OriginalValue = GetString(r, "OriginalValue"),
                }),
                Service = ReadArray(e, "service", s => new ServiceChange
                {
                    Name = GetString(s, "Name") ?? throw MissingField(entry.Name, "service.Name"),
                    StartupType = GetString(s, "StartupType") ?? throw MissingField(entry.Name, "service.StartupType"),
                    OriginalType = GetString(s, "OriginalType"),
                }),
                InvokeScript = ReadArray(e, "InvokeScript", s => s.GetString() ?? ""),
                UndoScript = ReadArray(e, "UndoScript", s => s.GetString() ?? ""),
            });
        }

        var unknown = overlay.Keys.Except(tweaks.Select(t => t.Id)).ToList();
        if (unknown.Count > 0)
        {
            throw new FormatException($"Native overlay refers to tweak ids missing from the catalog: {string.Join(", ", unknown)}. The upstream contract changed — update the overlay.");
        }

        return tweaks;
    }

    /// <summary>Parse winutil's appx.json debloat catalog.</summary>
    public static IReadOnlyList<AppxPackage> LoadAppx(string json)
    {
        using var doc = JsonDocument.Parse(JsonSanitizer.Sanitize(json));
        var packages = new List<AppxPackage>();

        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            packages.Add(new AppxPackage
            {
                Id = entry.Name,
                Content = GetString(entry.Value, "Content") ?? entry.Name,
                Category = GetString(entry.Value, "Category"),
                PackageId = GetString(entry.Value, "PackageId") ?? throw MissingField(entry.Name, "PackageId"),
                StoreId = GetString(entry.Value, "StoreId"),
            });
        }

        return packages;
    }

    private sealed record OverlayEntry(
        IReadOnlyList<CommandAction> PreApply,
        IReadOnlyList<CommandAction> Apply,
        IReadOnlyList<CommandAction> Undo,
        IReadOnlyList<string> AppxRemove,
        IReadOnlyList<string> DeletePaths,
        IReadOnlyList<string> EnsureDirs,
        HostsBlock? HostsBlock);

    private static Dictionary<string, OverlayEntry> LoadOverlay(string overlayJson)
    {
        using var doc = JsonDocument.Parse(overlayJson);
        var overlay = new Dictionary<string, OverlayEntry>();

        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            HostsBlock? hosts = null;
            if (entry.Value.TryGetProperty("hostsBlock", out var h) && h.ValueKind == JsonValueKind.Object)
            {
                hosts = new HostsBlock
                {
                    Url = GetString(h, "url") ?? throw new FormatException("hostsBlock is missing 'url'."),
                    RemoveFromMarker = GetString(h, "removeFromMarker") ?? throw new FormatException("hostsBlock is missing 'removeFromMarker'."),
                };
            }

            overlay[entry.Name] = new OverlayEntry(
                ReadArray(entry.Value, "preApply", ReadCommand),
                ReadArray(entry.Value, "apply", ReadCommand),
                ReadArray(entry.Value, "undo", ReadCommand),
                ReadArray(entry.Value, "appxRemove", e => e.GetString() ?? ""),
                ReadArray(entry.Value, "deletePaths", e => e.GetString() ?? ""),
                ReadArray(entry.Value, "ensureDirs", e => e.GetString() ?? ""),
                hosts);
        }

        return overlay;
    }

    private static CommandAction ReadCommand(JsonElement e) => new()
    {
        File = GetString(e, "file") ?? throw new FormatException("Overlay command is missing 'file'."),
        Args = GetString(e, "args") ?? "",
        IgnoreExitCode = e.TryGetProperty("ignoreExitCode", out var i) && i.ValueKind == JsonValueKind.True,
    };

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<T> ReadArray<T>(JsonElement element, string name, Func<JsonElement, T> map) =>
        element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? [.. array.EnumerateArray().Select(map)]
            : [];

    private static FormatException MissingField(string tweakId, string field) =>
        new($"Catalog entry '{tweakId}' is missing required field '{field}'.");
}
