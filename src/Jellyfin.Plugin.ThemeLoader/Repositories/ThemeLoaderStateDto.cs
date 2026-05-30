using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ThemeLoader.Repositories;

public sealed class StateDto : BasePluginConfiguration
{
    public bool Enabled { get; set; }

    public Guid? SelectedThemeId { get; set; }

    public List<ThemeDto> InstalledThemes { get; set; } = [];

    public bool InitializationFailed { get; set; }

    public sealed class ThemeDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string IndexFilename { get; set; } = string.Empty;

        public string Directory { get; set; } = string.Empty;

        public DateTime UploadedAtUtc { get; set; }
    }
}
