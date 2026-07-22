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
    private void Apply() => Run("applied", e => e.Apply(tweak));

    [RelayCommand]
    private void Undo() => Run("undone", e => e.Undo(tweak));

    private void Run(string verb, Action<Core.Engine.TweakEngine> action)
    {
        if (parent.Engine is not { } engine)
        {
            return;
        }

        try
        {
            action(engine);
            Redetect();
            parent.RefreshSummary();
            parent.SetStatus($"{Name}: {verb} → {StateText}");
        }
        catch (Exception e) when (e is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            parent.SetStatus($"{Name}: {e.Message}");
        }
    }
}
