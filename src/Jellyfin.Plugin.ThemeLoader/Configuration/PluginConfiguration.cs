using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ThemeLoader.Configuration;

public sealed class ThemeLoaderStateDto : BasePluginConfiguration
{
    public bool Enabled { get; set; }

    public Guid? SelectedThemeId { get; set; }

    public List<InstalledThemeDto> InstalledThemes { get; set; } = [];

    public bool InitializationFailed { get; set; }
}

public sealed class InstalledThemeDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string EntrypointPath { get; set; } = string.Empty;

    public string Directory { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }
}
