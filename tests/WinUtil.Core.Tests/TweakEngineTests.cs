using WinUtil.Core.Engine;
using WinUtil.Core.Model;

namespace WinUtil.Core.Tests;

public class TweakEngineTests
{
    private static Tweak RegistryTweak() => new()
    {
        Id = "WPFTweaksTest",
        Content = "Test tweak",
        Registry =
        [
            new RegistryChange { Path = @"HKLM:\A", Name = "X", Value = "0", Type = "DWord", OriginalValue = "1" },
            new RegistryChange { Path = @"HKCU:\B", Name = "Y", Value = "off", Type = "String", OriginalValue = "on" },
        ],
    };

    [Fact]
    public void Fresh_system_detects_NotApplied()
    {
        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal());

        Assert.Equal(ActionState.NotApplied, engine.Detect(RegistryTweak()));
    }

    [Fact]
    public void Apply_then_detect_reports_Applied()
    {
        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal());
        var tweak = RegistryTweak();

        engine.Apply(tweak);

        Assert.Equal(ActionState.Applied, engine.Detect(tweak));
    }

    [Fact]
    public void External_change_after_apply_reports_Drifted()
    {
        var registry = new FakeRegistry();
        var engine = new TweakEngine(registry, new InMemoryJournal());
        var tweak = RegistryTweak();
        engine.Apply(tweak);

        // A Windows update "helpfully" reverts one of the two values.
        registry.SetValue(@"HKLM:\A", "X", "1", "DWord");

        Assert.Equal(ActionState.Drifted, engine.Detect(tweak));
    }

    [Fact]
    public void Undo_restores_journaled_machine_state_not_catalog_defaults()
    {
        var registry = new FakeRegistry();
        var engine = new TweakEngine(registry, new InMemoryJournal());
        var tweak = RegistryTweak();

        // This machine had a non-default value before the tweak was applied.
        registry.SetValue(@"HKLM:\A", "X", "7", "DWord");
        engine.Apply(tweak);
        engine.Undo(tweak);

        Assert.True(registry.TryGetValue(@"HKLM:\A", "X", out var restored));
        Assert.Equal("7", restored);

        // The value that did not exist before apply is deleted again, not set to a default.
        Assert.False(registry.TryGetValue(@"HKCU:\B", "Y", out _));
    }

    [Fact]
    public void Undo_without_journal_falls_back_to_catalog_originals()
    {
        var registry = new FakeRegistry();
        var engine = new TweakEngine(registry, new InMemoryJournal());
        var tweak = RegistryTweak();

        // Simulates undoing a tweak applied by another tool or a previous install.
        registry.SetValue(@"HKLM:\A", "X", "0", "DWord");
        registry.SetValue(@"HKCU:\B", "Y", "off", "String");

        engine.Undo(tweak);

        Assert.True(registry.TryGetValue(@"HKLM:\A", "X", out var a));
        Assert.Equal("1", a);
        Assert.True(registry.TryGetValue(@"HKCU:\B", "Y", out var b));
        Assert.Equal("on", b);
    }

    [Fact]
    public void Reapplying_then_undo_restores_the_true_original_not_the_second_apply()
    {
        var registry = new FakeRegistry();
        var engine = new TweakEngine(registry, new InMemoryJournal());
        var tweak = RegistryTweak();
        registry.SetValue(@"HKLM:\A", "X", "7", "DWord");

        // Apply twice without an undo in between — must not crash and must not
        // lose the true pre-tweak value (7).
        engine.Apply(tweak);
        engine.Apply(tweak);
        engine.Undo(tweak);

        Assert.True(registry.TryGetValue(@"HKLM:\A", "X", out var restored));
        Assert.Equal("7", restored);
    }

    [Fact]
    public void Apply_records_the_source_on_journal_entries()
    {
        var journal = new InMemoryJournal();
        var engine = new TweakEngine(new FakeRegistry(), journal, source: "gui");

        engine.Apply(RegistryTweak());

        Assert.All(journal.EntriesFor("WPFTweaksTest"), e => Assert.Equal("gui", e.Source));
    }

    [Fact]
    public void Tweak_with_no_typed_changes_is_Unknown()
    {
        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal());
        var scripted = new Tweak { Id = "WPFTweaksScripted", Content = "Scripted", InvokeScript = ["Write-Host hi"] };

        Assert.Equal(ActionState.Unknown, engine.Detect(scripted));
    }
}
