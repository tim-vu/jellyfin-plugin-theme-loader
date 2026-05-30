namespace Jellyfin.Plugin.ThemeLoader.Models;

public sealed class ThemeManifest
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string Entrypoint { get; init; }
}