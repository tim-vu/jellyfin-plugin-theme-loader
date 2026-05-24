using System;
using System.Linq;
using Jellyfin.Plugin.ThemeLoader.Configuration;
using Jellyfin.Plugin.ThemeLoader.Models;

namespace Jellyfin.Plugin.ThemeLoader.Repositories;

public sealed class StateRepository : IStateRepository
{
    private static Plugin Plugin => Plugin.Instance ?? throw new ApplicationException("Plugin was not initialized");
    
    private ThemeLoaderState? _cached;

    public ThemeLoaderState Get()
    {
        if (_cached is not null)
        {
            return _cached;
        }
        
        var configuration = Plugin.Configuration;
        var themes = configuration.InstalledThemes.ToDictionary(
            theme => theme.Id,
            FromDto);

        _cached = new ThemeLoaderState(
            configuration.Enabled,
            configuration.SelectedThemeId,
            themes,
            configuration.InitializationFailed);

        return _cached;
    }

    public void Save()
    {
        if (_cached is null)
        {
            return;
        }
        
        var configuration = new ThemeLoaderStateDto
        {
            Enabled = _cached.Enabled,
            SelectedThemeId = _cached.SelectedThemeId,
            InstalledThemes = _cached.InstalledThemes.Values
                .Select(ToDto)
                .ToList(),
            InitializationFailed = _cached.InitializationFailed
        };

        Plugin.UpdateConfiguration(configuration);
    }

    private static InstalledTheme FromDto(InstalledThemeDto theme)
    {
        return new InstalledTheme
        {
            Id = theme.Id,
            Name = theme.Name,
            Version = theme.Version,
            EntrypointPath = theme.EntrypointPath,
            Directory = theme.Directory,
            UploadedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(theme.UploadedAtUtc, DateTimeKind.Utc))
        };
    }

    private static InstalledThemeDto ToDto(InstalledTheme theme)
    {
        return new InstalledThemeDto
        {
            Id = theme.Id,
            Name = theme.Name,
            Version = theme.Version,
            EntrypointPath = theme.EntrypointPath,
            Directory = theme.Directory,
            UploadedAtUtc = theme.UploadedAtUtc.UtcDateTime
        };
    }
}
