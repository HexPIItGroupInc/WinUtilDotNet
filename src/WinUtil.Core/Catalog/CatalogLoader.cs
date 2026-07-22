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
    public static IReadOnlyList<Tweak> LoadTweaks(string json)
    {
        using var doc = JsonDocument.Parse(JsonSanitizer.Sanitize(json));
        var tweaks = new List<Tweak>();

        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            var e = entry.Value;
            tweaks.Add(new Tweak
            {
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

        return tweaks;
    }

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
