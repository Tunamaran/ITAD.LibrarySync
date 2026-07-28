using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed partial class StatDetailsViewModel : ObservableObject
{
    public string Mode { get; }

    public string WindowTitle { get; }
    public string HeaderTitle { get; }
    public string SummaryText { get; }

    public bool IsTotalGamesMode => Mode == "TotalGames";
    public bool IsMatchRateMode => Mode == "MatchRate";
    public bool IsActivePlatformsMode => Mode == "ActivePlatforms";
    public bool IsDuplicateGamesMode => Mode == "DuplicateGames";

    public LanguageManager Lang => LanguageManager.Instance;

    // Stat Summary properties
    public int TotalCount { get; }
    public double MatchRatePct { get; }
    public int ActivePlatformCount { get; }
    public int DuplicateCount { get; }
    public int AutoMatchedCount { get; }
    public int CustomMappedCount { get; }
    public int UnmatchedCount { get; }

    // Collections
    public ObservableCollection<TotalGameItem> AllTotalGames { get; } = [];
    public ObservableCollection<TotalGameItem> FilteredTotalGames { get; } = [];

    public ObservableCollection<DuplicateGameItem> AllDuplicateGames { get; } = [];
    public ObservableCollection<DuplicateGameItem> FilteredDuplicateGames { get; } = [];

    public ObservableCollection<LauncherSettingsItem> ActivePlatformsList { get; } = [];

    public ObservableCollection<UnmatchedTitle> UnmatchedList { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public StatDetailsViewModel(
        string mode,
        IEnumerable<TotalGameItem> totalGames,
        IEnumerable<DuplicateGameItem> duplicateGames,
        IEnumerable<LauncherSettingsItem> platforms,
        IEnumerable<UnmatchedTitle> unmatchedTitles,
        int totalCount,
        double matchRatePct,
        int activePlatformCount,
        int duplicateCount,
        int autoMatchedCount,
        int customMappedCount,
        int unmatchedCount)
    {
        Mode = mode;
        TotalCount = totalCount;
        MatchRatePct = matchRatePct;
        ActivePlatformCount = activePlatformCount;
        DuplicateCount = duplicateCount;
        AutoMatchedCount = autoMatchedCount;
        CustomMappedCount = customMappedCount;
        UnmatchedCount = unmatchedCount;

        foreach (var g in totalGames) AllTotalGames.Add(g);
        foreach (var d in duplicateGames) AllDuplicateGames.Add(d);
        foreach (var p in platforms) ActivePlatformsList.Add(p);
        foreach (var u in unmatchedTitles) UnmatchedList.Add(u);

        switch (mode)
        {
            case "TotalGames":
                WindowTitle = Lang["StatTitleTotalGames"];
                HeaderTitle = Lang["StatTitleTotalGames"];
                SummaryText = string.Format(Lang["StatsHeader"], totalCount);
                break;
            case "MatchRate":
                WindowTitle = Lang["StatTitleMatchRate"];
                HeaderTitle = Lang["StatTitleMatchRate"];
                SummaryText = $"{matchRatePct}% match rate across {totalCount} games";
                break;
            case "ActivePlatforms":
                WindowTitle = Lang["StatTitleActivePlatforms"];
                HeaderTitle = Lang["StatTitleActivePlatforms"];
                SummaryText = $"{activePlatformCount} active launchers configured";
                break;
            case "DuplicateGames":
                WindowTitle = Lang["StatTitleDuplicateGames"];
                HeaderTitle = Lang["StatTitleDuplicateGames"];
                SummaryText = $"{duplicateCount} games owned across 2+ platforms";
                break;
            default:
                WindowTitle = "Statistics Details";
                HeaderTitle = "Statistics Details";
                SummaryText = string.Empty;
                break;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();

        if (IsTotalGamesMode)
        {
            FilteredTotalGames.Clear();
            var matches = string.IsNullOrWhiteSpace(q)
                ? AllTotalGames
                : AllTotalGames.Where(g =>
                    g.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    g.StoreId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    g.DisplayPlatform.Contains(q, StringComparison.OrdinalIgnoreCase));

            foreach (var m in matches) FilteredTotalGames.Add(m);
        }
        else if (IsDuplicateGamesMode)
        {
            FilteredDuplicateGames.Clear();
            var matches = string.IsNullOrWhiteSpace(q)
                ? AllDuplicateGames
                : AllDuplicateGames.Where(d =>
                    d.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    d.PlatformsList.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    d.StoreIdsList.Contains(q, StringComparison.OrdinalIgnoreCase));

            foreach (var m in matches) FilteredDuplicateGames.Add(m);
        }
    }
}
