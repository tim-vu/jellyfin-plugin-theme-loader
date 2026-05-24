namespace Jellyfin.Plugin.ThemeLoader.Models;

public sealed class ThemeUploadResult
{
    public required string ThemeSlug { get; init; }

    public required string ThemeName { get; init; }

    public required string ThemeVersion { get; init; }
}
