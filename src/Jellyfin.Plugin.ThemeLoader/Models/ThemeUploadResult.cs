using System;

namespace Jellyfin.Plugin.ThemeLoader.Models;

public sealed class ThemeUploadResult
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }
}
