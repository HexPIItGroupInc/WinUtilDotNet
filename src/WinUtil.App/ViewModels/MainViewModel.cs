using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUtil.Core.Catalog;
using WinUtil.Core.Engine;
using WinUtil.Core.Model;

namespace WinUtil.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TweakEngine? engine;
    private readonly List<TweakItemViewModel> allTweaks = [];

    [ObservableProperty]
    private string search = "";

    [ObservableProperty]
    private string selectedCategory = "All";

    [ObservableProperty]
    private string coverageText = "";

    [ObservableProperty]
    private string statusText = "";

    public ObservableCollection<string> Categories { get; } = [];

    public ObservableCollection<TweakItemViewModel> FilteredTweaks { get; } = [];

    /// <summary>False on non-Windows hosts: browse-and-design mode, actions disabled.</summary>
    public bool EngineAvailable => engine is not null;

    public MainViewModel()
    {
        engine = CreateEngine();

        try
        {
            var (catalog, overlay) = LocateCatalogs();
            var tweaks = CatalogLoader.LoadTweaks(File.ReadAllText(catalog), overlay is null ? null : File.ReadAllText(overlay));

            var coverage = CatalogCoverage.Measure(tweaks);
            CoverageText = $"PowerShell-free {coverage.NativePercent:F1}%";

            foreach (var tweak in tweaks.OrderBy(t => CleanCategory(t.Category)).ThenBy(t => t.Content))
            {
                allTweaks.Add(new TweakItemViewModel(tweak, this));
            }

            Categories.Add("All");
            foreach (var category in allTweaks.Select(t => t.Category).Distinct().Order())
            {
                Categories.Add(category);
            }

            StatusText = engine is null
                ? $"{tweaks.Count} tweaks loaded — browse mode (apply/detect need Windows)"
                : $"{tweaks.Count} tweaks loaded";

            RefreshDetection();
            ApplyFilter();
        }
        catch (Exception e) when (e is IOException or FormatException or DirectoryNotFoundException)
        {
            StatusText = $"Catalog not found — set WINUTIL_REPO to a winutil checkout. ({e.Message})";
        }
    }

    internal TweakEngine? Engine => engine;

    internal void SetStatus(string message) => StatusText = message;

    [RelayCommand]
    private void RefreshDetection()
    {
        if (engine is null)
        {
            return;
        }

        foreach (var item in allTweaks)
        {
            item.Redetect();
        }

        StatusText = $"Detected real system state for {allTweaks.Count} tweaks";
    }

    partial void OnSearchChanged(string value) => ApplyFilter();

    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredTweaks.Clear();
        foreach (var item in allTweaks.Where(Matches))
        {
            FilteredTweaks.Add(item);
        }
    }

    private bool Matches(TweakItemViewModel item) =>
        (SelectedCategory == "All" || item.Category == SelectedCategory)
        && (Search.Length == 0
            || item.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains(Search, StringComparison.OrdinalIgnoreCase));

    internal static string CleanCategory(string? category) =>
        (category ?? "Other").Replace("z__", "", StringComparison.Ordinal);

    private static (string Catalog, string? Overlay) LocateCatalogs()
    {
        var repo = Environment.GetEnvironmentVariable("WINUTIL_REPO");
        var catalog = repo is null ? "config/tweaks.json" : Path.Combine(repo, "config", "tweaks.json");

        string? overlay = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "native", "overrides.json");
            if (File.Exists(candidate))
            {
                overlay = candidate;
                break;
            }
        }

        return (catalog, overlay);
    }

    private static TweakEngine? CreateEngine()
    {
#if WINDOWS
        var journal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinUtilsDotNet", "journal.json");
        return new TweakEngine(
            new WinUtil.System.WindowsRegistry(),
            new FileJournal(journal),
            new WinUtil.System.WindowsServices(),
            new WinUtil.System.ProcessCommandRunner(),
            new WinUtil.System.WindowsAppx(),
            new WinUtil.System.NativeFileSystem(),
            new WinUtil.System.HostsFileBlocker(),
            new WinUtil.System.WindowsTokenProvider());
#else
        return null;
#endif
    }
}
