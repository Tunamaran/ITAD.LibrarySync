namespace ITAD.LibrarySync.Core.Launchers.Ubisoft;

internal sealed class UbisoftConfigurationFile
{
    public UbisoftConfigurationRoot? root { get; set; }

    public UbisoftConfigurationLocalizations? localizations { get; set; }
}

internal sealed class UbisoftConfigurationRoot
{
    public string? display_name { get; set; }

    public string? name { get; set; }

    public UbisoftConfigurationUplay? uplay { get; set; }

    public string? icon_image { get; set; }

    public string? thumb_image { get; set; }

    public object? third_party_platform { get; set; }
}

internal sealed class UbisoftConfigurationUplay
{
    public string? game_code { get; set; }

    public string? achievements_sync_id { get; set; }
}

internal sealed class UbisoftConfigurationLocalizations
{
    public UbisoftConfigurationLanguage? Default { get; set; }
}

internal sealed class UbisoftConfigurationLanguage
{
    public string? name { get; set; }

    public string? gamename { get; set; }

    public string? iconimage { get; set; }

    public string? thumbimage { get; set; }
}
