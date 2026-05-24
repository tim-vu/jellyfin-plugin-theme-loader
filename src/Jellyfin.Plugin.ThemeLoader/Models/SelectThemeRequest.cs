using System;

namespace Jellyfin.Plugin.ThemeLoader.Models;

public sealed class SelectThemeRequest
{
    public required Guid Id { get; init; }
}
