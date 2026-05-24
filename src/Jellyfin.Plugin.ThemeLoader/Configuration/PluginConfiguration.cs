using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ThemeLoader.Configuration;

public class PluginState : BasePluginConfiguration
{
    public bool Enabled { get; set; }

    public string? SelectedTheme { get; set; }

    public List<InstalledTheme> InstalledThemes { get; set; } = [];

    public bool InitializationFailed { get; set; }

    public InstalledTheme? GetSelectedTheme()
    {
        return string.IsNullOrWhiteSpace(SelectedTheme)
            ? null
            : InstalledThemes.FirstOrDefault(theme => theme.Slug == SelectedTheme);
    }
}

public sealed class InstalledTheme
{
    public required string Slug { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string DirectoryPath { get; init; }

    public required string CssFilePath { get; init; }

    public required DateTimeOffset UploadedAtUtc { get; init; }
}
