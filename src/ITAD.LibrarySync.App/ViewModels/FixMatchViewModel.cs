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
    private readonly UnmatchedTitle _unmatchedTitle;

    public FixMatchViewModel(ICustomMappingService customMappingService, UnmatchedTitle unmatchedTitle)
    {
        _customMappingService = customMappingService;
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
        if (string.IsNullOrWhiteSpace(MappedId))
        {
            MessageBox.Show(Lang["FixMatchValidation"], Lang["FixMatchValidationTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var mapping = new CustomGameMapping(
            _unmatchedTitle.Launcher,
            _unmatchedTitle.StoreId,
            MappedId.Trim(),
            _unmatchedTitle.Title,
            DateTime.Now);

        await _customMappingService.SetMappingAsync(mapping);
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
