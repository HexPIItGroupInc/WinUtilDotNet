using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUtil.Core.Model;

namespace WinUtil.App.ViewModels;

public partial class TweakItemViewModel(Tweak tweak, MainViewModel parent) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(StateBrush), nameof(ChipBackground))]
    private ActionState state = ActionState.Unknown;

    public string Name => tweak.Content;

    public string Description => tweak.Description ?? "";

    public string Category => MainViewModel.CleanCategory(tweak.Category);

    public bool IsNative => tweak.IsNativelyExecutable;

    public bool IsScript => !tweak.IsNativelyExecutable;

    public bool CanAct => parent.EngineAvailable && IsNative;

    public string StateText => State switch
    {
        ActionState.Applied => "Applied",
        ActionState.NotApplied => "Not applied",
        ActionState.Drifted => "Drifted",
        _ => "—",
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
            parent.SetStatus($"{Name}: {verb} → {StateText}");
        }
        catch (Exception e) when (e is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            parent.SetStatus($"{Name}: {e.Message}");
        }
    }
}
