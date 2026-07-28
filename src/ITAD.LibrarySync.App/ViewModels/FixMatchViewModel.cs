using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Models;
using ITAD.LibrarySync.Core.Services;

namespace ITAD.LibrarySync.App.ViewModels;

public sealed partial class FixMatchViewModel : ObservableObject
{
    private readonly ICustomMappingService _customMappingService;
    private readonly IUnmatchedTitlesService _unmatchedTitlesService;
    private readonly UnmatchedTitle _unmatchedTitle;

    public FixMatchViewModel(
        ICustomMappingService customMappingService,
        IUnmatchedTitlesService unmatchedTitlesService,
        UnmatchedTitle unmatchedTitle)
    {
        _customMappingService = customMappingService;
        _unmatchedTitlesService = unmatchedTitlesService;
        _unmatchedTitle = unmatchedTitle;

        LauncherName = unmatchedTitle.Launcher.ToString();
        Title = unmatchedTitle.Title;
        StoreId = unmatchedTitle.StoreId;
    }

    public string LauncherName { get; }
    public string Title { get; }
    public string StoreId { get; }

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty]
    private string _mappedId = string.Empty;

    public event EventHandler? RequestClose;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var targetId = ExtractSlugOrId(MappedId);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            MessageBox.Show(Lang["FixMatchValidation"], Lang["FixMatchValidationTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var mapping = new CustomGameMapping(
            _unmatchedTitle.Launcher,
            _unmatchedTitle.StoreId,
            targetId,
            _unmatchedTitle.Title,
            DateTime.Now);

        await _customMappingService.SetMappingAsync(mapping);
        await _unmatchedTitlesService.RemoveByStoreIdOrTitleAsync(_unmatchedTitle.Launcher, _unmatchedTitle.StoreId, _unmatchedTitle.Title);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public static string ExtractSlugOrId(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput)) return string.Empty;
        var trimmed = rawInput.Trim();

        var match = System.Text.RegularExpressions.Regex.Match(
            trimmed,
            @"(?:isthereanydeal\.com\/game\/)([^\/\?\#]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value.Trim();
        }

        return trimmed.TrimEnd('/');
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
