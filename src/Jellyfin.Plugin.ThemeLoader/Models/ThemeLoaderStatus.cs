using System;

namespace Jellyfin.Plugin.ThemeLoader.Models;

public sealed class ThemeLoaderStatus
{
    public required bool Enabled { get; init; }

    public required ActiveThemeInfo? Theme { get; init; }

    public required bool InitializationFailed { get; init; }
}

public sealed class ActiveThemeInfo
{
    public required string Slug { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required DateTimeOffset UploadedAt { get; init; }
}
