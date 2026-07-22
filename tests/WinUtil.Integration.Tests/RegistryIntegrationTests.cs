using System.Diagnostics;
using WinUtil.Core.Engine;
using WinUtil.Core.Model;
using WinUtil.System;

namespace WinUtil.Integration.Tests;

/// <summary>
/// Runs against the REAL Windows registry on the CI test VM. Guarded by
/// RUN_WINDOWS_INTEGRATION=1 so `dotnet test` on a dev machine stays inert.
/// All writes stay under an HKCU test key that is removed afterwards.
/// </summary>
public sealed class RegistryIntegrationTests : IDisposable
{
    private const string TestKey = @"HKCU:\SOFTWARE\WinUtilsDotNetIntegrationTest";

    private static bool Enabled => Environment.GetEnvironmentVariable("RUN_WINDOWS_INTEGRATION") == "1";

    private static Tweak TestTweak() => new()
    {
        Id = "WPFTweaksIntegrationFixture",
        Content = "Integration fixture",
        Registry =
        [
            new RegistryChange { Path = TestKey, Name = "Enabled", Value = "0", Type = "DWord", OriginalValue = "1" },
            new RegistryChange { Path = TestKey, Name = "Mode", Value = "off", Type = "String", OriginalValue = "on" },
        ],
    };

    [SkippableFact]
    public void Apply_detect_drift_undo_on_real_registry_without_powershell()
    {
        Skip.IfNot(Enabled, "RUN_WINDOWS_INTEGRATION != 1");

        var powershellBefore = CountShellProcesses();

        var registry = new WindowsRegistry();
        var engine = new TweakEngine(registry, new InMemoryJournal());
        var tweak = TestTweak();

        // Fresh state: catalog originals are not present -> NotApplied.
        Assert.Equal(ActionState.NotApplied, engine.Detect(tweak));

        engine.Apply(tweak);
        Assert.Equal(ActionState.Applied, engine.Detect(tweak));

        // Registry ground truth, read back independently.
        Assert.True(registry.TryGetValue(TestKey, "Enabled", out var applied));
        Assert.Equal("0", applied);

        // A "Windows update" reverts one value behind our back -> Drifted.
        registry.SetValue(TestKey, "Enabled", "1", "DWord");
        Assert.Equal(ActionState.Drifted, engine.Detect(tweak));

        // Undo restores true prior state: the values did not exist -> deleted.
        engine.Undo(tweak);
        Assert.Equal(ActionState.NotApplied, engine.Detect(tweak));
        Assert.False(registry.TryGetValue(TestKey, "Mode", out _));

        // The flagship acceptance check: everything above was in-process
        // Win32 — no PowerShell was spawned at any point.
        Assert.Equal(powershellBefore, CountShellProcesses());
    }

    [SkippableFact]
    public void DeleteKeyTree_removes_real_subtrees_and_tolerates_missing_keys()
    {
        Skip.IfNot(Enabled, "RUN_WINDOWS_INTEGRATION != 1");

        var registry = new WindowsRegistry();
        registry.SetValue(TestKey + @"\Tree\Deep", "Value", "1", "DWord");

        registry.DeleteKeyTree(TestKey + @"\Tree");
        Assert.False(registry.TryGetValue(TestKey + @"\Tree\Deep", "Value", out _));

        // Missing key: no-op, no throw.
        registry.DeleteKeyTree(TestKey + @"\Tree");
    }

    [SkippableFact]
    public void Journal_survives_process_style_reload_and_restores_preexisting_values()
    {
        Skip.IfNot(Enabled, "RUN_WINDOWS_INTEGRATION != 1");

        var registry = new WindowsRegistry();
        var journalPath = Path.Combine(Path.GetTempPath(), $"winutil-it-journal-{Guid.NewGuid():N}.json");
        var tweak = TestTweak();

        try
        {
            // The machine already had a non-default value before the tweak.
            registry.SetValue(TestKey, "Enabled", "7", "DWord");

            new TweakEngine(registry, new FileJournal(journalPath)).Apply(tweak);

            // New engine + new journal instance = a later CLI invocation.
            new TweakEngine(registry, new FileJournal(journalPath)).Undo(tweak);

            Assert.True(registry.TryGetValue(TestKey, "Enabled", out var restored));
            Assert.Equal("7", restored);
        }
        finally
        {
            File.Delete(journalPath);
        }
    }

    private static int CountShellProcesses() =>
        Process.GetProcessesByName("powershell").Length + Process.GetProcessesByName("pwsh").Length;

    public void Dispose()
    {
        if (Enabled)
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\WinUtilsDotNetIntegrationTest", throwOnMissingSubKey: false);
        }
    }
}
