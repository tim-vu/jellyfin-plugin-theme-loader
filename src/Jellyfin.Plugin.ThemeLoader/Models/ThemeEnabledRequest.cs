namespace Jellyfin.Plugin.ThemeLoader.Models;

public sealed class ThemeEnabledRequest
{
    public required bool Enabled { get; init; }
}
