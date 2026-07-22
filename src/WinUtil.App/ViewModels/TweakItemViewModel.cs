using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUtil.Core.Model;

namespace WinUtil.App.ViewModels;

public partial class TweakItemViewModel(Tweak tweak, MainViewModel parent) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(StateBrush), nameof(ChipBackground),
        nameof(AccentBrush), nameof(ShowApply), nameof(ShowUndo), nameof(ApplyLabel))]
    private ActionState state = ActionState.Unknown;

    /// <summary>A short explanation shown on the card after a failed action (e.g. "needs admin", "System Restore is off").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? actionMessage;

    public bool HasMessage => !string.IsNullOrEmpty(ActionMessage);

    public string Name => tweak.Content;

    public string Description => tweak.Description ?? "";

    public string Category => MainViewModel.CleanCategory(tweak.Category);

    public bool IsNative => tweak.IsNativelyExecutable;

    public bool IsScript => !tweak.IsNativelyExecutable;

    public bool CanAct => parent.EngineAvailable && IsNative;

    // Apply is offered unless the tweak is already fully applied; Undo unless it
    // is fully not-applied. Drifted shows both, with Apply relabelled "Re-apply".
    public bool ShowApply => CanAct && State != ActionState.Applied;

    public bool ShowUndo => CanAct && (State == ActionState.Applied || State == ActionState.Drifted);

    public string ApplyLabel => State == ActionState.Drifted ? "Re-apply" : "Apply";

    public string StateText => State switch
    {
        ActionState.Applied => "✓ Applied",
        ActionState.NotApplied => "Not applied",
        ActionState.Drifted => "⚠ Drifted",
        _ when IsScript => "not yet native",
        _ => "unknown",
    };

    public IBrush StateBrush => State switch
    {
        ActionState.Applied => Brushes.White,
        ActionState.Drifted => Brushes.Black,
        _ => new SolidColorBrush(Color.Parse("#9BA7B4")),
    };

    public IBrush ChipBackground => State switch
    {
        ActionState.Applied => new SolidColorBrush(Color.Parse("#15803D")),
        ActionState.Drifted => new SolidColorBrush(Color.Parse("#F59E0B")),
        ActionState.NotApplied => new SolidColorBrush(Color.Parse("#242B35")),
        _ => new SolidColorBrush(Color.Parse("#1A202A")),
    };

    /// <summary>Left-edge accent so applied/drifted rows are scannable at a glance.</summary>
    public IBrush AccentBrush => State switch
    {
        ActionState.Applied => new SolidColorBrush(Color.Parse("#22C55E")),
        ActionState.Drifted => new SolidColorBrush(Color.Parse("#F59E0B")),
        _ => Brushes.Transparent,
    };

    public void Redetect()
    {
        if (parent.Engine is { } engine)
        {
            State = engine.Detect(tweak);
        }
    }

    [RelayCommand]
    private async Task ApplyAsync() => await Run("applied", e => e.Apply(tweak));

    [RelayCommand]
    private async Task UndoAsync() => await Run("undone", e => e.Undo(tweak));

    /// <summary>Apply as part of a OneShot batch; returns whether it succeeded.
    /// Already-applied or non-actionable tweaks are a no-op success.</summary>
    public async Task<bool> ApplyForBatchAsync()
    {
        if (!CanAct || State == ActionState.Applied)
        {
            return true;
        }

        return await Run("applied", e => e.Apply(tweak));
    }

    private async Task<bool> Run(string verb, Action<Core.Engine.TweakEngine> action)
    {
        if (parent.Engine is not { } engine)
        {
            return false;
        }

        parent.SetStatus($"{Name}: {verb}…");
        ActionMessage = null;
        var succeeded = true;
        string status;
        try
        {
            // Off the UI thread: a tweak may do slow I/O (a hosts blocklist
            // fetch, DISM) and must never freeze or deadlock the window.
            await Task.Run(() => action(engine));
            status = $"{Name}: {verb}";
        }
#pragma warning disable CA1031 // A single tweak must never crash the app — any
        catch (Exception e)      // failure (network, ACL, missing tool) becomes a message.
#pragma warning restore CA1031
        {
            succeeded = false;
            var hint = e is UnauthorizedAccessException ? " This action needs administrator rights — launch WinUtil as administrator." : "";
            ActionMessage = e.Message + hint;
            status = $"{Name}: couldn't {verb.TrimEnd('d', 'e')} — see the note on the card";
        }

        // Back on the UI thread after the await: reflect true system state.
        Redetect();
        parent.RefreshSummary();
        parent.SetStatus(status);
        return succeeded;
    }
}
