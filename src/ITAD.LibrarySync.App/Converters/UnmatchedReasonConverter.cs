using System.Globalization;
using System.Windows.Data;
using ITAD.LibrarySync.App.Services;
using ITAD.LibrarySync.Core.Models;

namespace ITAD.LibrarySync.App.Converters;

/// <summary>
/// Converts an UnmatchedReason enum (or legacy string) to a localized display string
/// via LanguageManager.
/// </summary>
public sealed class UnmatchedReasonConverter : IValueConverter
{
    public static readonly UnmatchedReasonConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var lang = LanguageManager.Instance;

        if (value is UnmatchedReason reason)
        {
            return reason switch
            {
                UnmatchedReason.NotInCatalog => lang["UnmatchedReasonNotInCatalog"],
                UnmatchedReason.TrackingIdFallback => lang["UnmatchedReasonTrackingId"],
                UnmatchedReason.NoApiMatch => lang["UnmatchedReasonNoApiMatch"],
                _ => reason.ToString()
            };
        }

        // Fallback for legacy string reasons from old JSON data
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
