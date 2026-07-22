using WinUtil.Core.Catalog;
using WinUtil.Core.Engine;
using WinUtil.Core.Model;

namespace WinUtil.Core.Tests;

public class RegistryDeleteKeysTests
{
    private const string Catalog = """{ "WPFTweaksBags": { "Content": "Bags", "InvokeScript": ["x"], "UndoScript": ["y"] } }""";

    private const string Overlay = """
        {
          "WPFTweaksBags": {
            "registryDeleteKeys": ["HKCU:\\Shell\\Bags", "HKCU:\\Shell\\BagMRU"],
            "registry": [
              { "Path": "HKCU:\\Shell\\Bags\\AllFolders\\Shell", "Name": "FolderType", "Value": "NotSpecified", "Type": "String" }
            ],
            "undoRegistryDeleteKeys": ["HKCU:\\Shell\\Bags", "HKCU:\\Shell\\BagMRU"]
          }
        }
        """;

    [Fact]
    public void Overlay_registry_changes_merge_into_the_tweak()
    {
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single();

        Assert.True(tweak.IsNativelyExecutable);
        var change = tweak.Registry.Single();
        Assert.Equal("FolderType", change.Name);
        Assert.Equal(2, tweak.RegistryDeleteKeys.Count);
    }

    [Fact]
    public void Apply_flushes_trees_then_writes_and_becomes_detectable()
    {
        var registry = new FakeRegistry();
        registry.SetValue(@"HKCU:\Shell\Bags\SomeFolder", "Stale", "1", "DWord");
        registry.SetValue(@"HKCU:\Shell\BagMRU", "Stale", "1", "DWord");

        var engine = new TweakEngine(registry, new InMemoryJournal());
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single();

        engine.Apply(tweak);

        Assert.False(registry.TryGetValue(@"HKCU:\Shell\Bags\SomeFolder", "Stale", out _));
        Assert.False(registry.TryGetValue(@"HKCU:\Shell\BagMRU", "Stale", out _));
        Assert.True(registry.TryGetValue(@"HKCU:\Shell\Bags\AllFolders\Shell", "FolderType", out var type));
        Assert.Equal("NotSpecified", type);
        Assert.Equal(ActionState.Applied, engine.Detect(tweak));
    }

    [Fact]
    public void Undo_deletes_trees_and_detect_returns_NotApplied()
    {
        var registry = new FakeRegistry();
        var engine = new TweakEngine(registry, new InMemoryJournal());
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single();

        engine.Apply(tweak);
        engine.Undo(tweak);

        Assert.False(registry.TryGetValue(@"HKCU:\Shell\Bags\AllFolders\Shell", "FolderType", out _));
        Assert.Equal(ActionState.NotApplied, engine.Detect(tweak));
    }
}
