namespace ITAD.LibrarySync.Core.Launchers.Xbox;

public static class MicrosoftStoreId
{
    /// <summary>
    /// Microsoft Store product ID (BigId), e.g. 9NBLGGH4R2Q6.
    /// ITAD tracks Microsoft Store games with these IDs.
    /// </summary>
    public static bool IsProductId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length is >= 10 and <= 14 &&
        value[0] == '9' &&
        value.All(static c => char.IsLetterOrDigit(c));

    public static bool IsPackageFamilyName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains('_', StringComparison.Ordinal) &&
        !IsProductId(value);

    public static bool IsLegacyPrefixedTitleId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.StartsWith("xbox:", StringComparison.OrdinalIgnoreCase);
}
