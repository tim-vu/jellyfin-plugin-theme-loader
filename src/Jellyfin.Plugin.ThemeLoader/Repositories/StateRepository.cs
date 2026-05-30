using System;
using System.Linq;
using Jellyfin.Plugin.ThemeLoader.Domain;

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

        var configuration = new StateDto
        {
            Enabled = _cached.Enabled,
            SelectedThemeId = _cached.SelectedThemeId,
            InstalledThemes = [.. _cached.InstalledThemes.Values.Select(ToDto)],
            InitializationFailed = _cached.InitializationFailed
        };

        Plugin.UpdateConfiguration(configuration);
    }

    private static Theme FromDto(StateDto.ThemeDto theme)
    {
        return new Theme
        {
            Id = theme.Id,
            Name = theme.Name,
            Version = theme.Version,
            IndexFilename = theme.IndexFilename,
            Directory = theme.Directory,
            UploadedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(theme.UploadedAtUtc, DateTimeKind.Utc))
        };
    }

    private static StateDto.ThemeDto ToDto(Theme theme)
    {
        return new StateDto.ThemeDto
        {
            Id = theme.Id,
            Name = theme.Name,
            Version = theme.Version,
            IndexFilename = theme.IndexFilename,
            Directory = theme.Directory,
            UploadedAtUtc = theme.UploadedAtUtc.UtcDateTime
        };
    }
}