using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ThemeLoader.Models;

public sealed class ThemeLoaderStatus
{
    public required bool Enabled { get; init; }

    public required Guid? SelectedThemeId { get; init; }

    public required IReadOnlyList<ThemeInfo> Themes { get; init; }

    public required bool InitializationFailed { get; init; }
}

public sealed class ThemeInfo
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required DateTimeOffset UploadedAt { get; init; }
}