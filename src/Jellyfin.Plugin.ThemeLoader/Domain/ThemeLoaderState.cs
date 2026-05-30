using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.ThemeLoader.Domain;

public sealed class ThemeLoaderState
{
    private readonly Dictionary<Guid, Theme> _installedThemes;

    internal ThemeLoaderState(
        bool enabled,
        Guid? selectedThemeId,
        Dictionary<Guid, Theme> installedThemes,
        bool initializationFailed)
    {
        Enabled = enabled;
        SelectedThemeId = selectedThemeId;
        _installedThemes = installedThemes;
        InitializationFailed = initializationFailed;
    }

    public void Enable()
    {
        if (SelectedThemeId is null)
        {
            throw new InvalidOperationException("Cannot enable the plugin, a theme must be selected");
        }

        Enabled = true;
    }

    public void Disable()
    {
        Enabled = false;
    }

    [MemberNotNullWhen(true, nameof(SelectedTheme))]
    public bool Enabled { get; private set; }

    public void SelectTheme(Guid id)
    {
        if (!_installedThemes.TryGetValue(id, out var theme))
        {
            throw new InvalidOperationException("No theme with Id found");
        }

        SelectedThemeId = theme.Id;
    }

    public Theme? SelectedTheme => _installedThemes.GetValueOrDefault(SelectedThemeId ?? Guid.Empty);

    public Guid? SelectedThemeId { get; private set; }

    public IReadOnlyDictionary<Guid, Theme> InstalledThemes => _installedThemes;

    public bool InitializationFailed { get; private set; }

    public Theme AddTheme(
        Guid id,
        string name,
        string version,
        string entrypointPath,
        string directory)
    {
        var theme = new Theme
        {
            Id = id,
            Name = name,
            Version = version,
            IndexFilename = entrypointPath,
            Directory = directory,
            UploadedAtUtc = DateTimeOffset.UtcNow
        };
        _installedThemes[id] = theme;

        return theme;
    }

    public void RemoveTheme(Guid id)
    {
        var removed = _installedThemes.Remove(id);

        if (!removed)
        {
            throw new ArgumentException("No theme with Id found");
        }

        if (SelectedThemeId == id)
        {
            SelectedThemeId = null;
            Enabled = false;
        }
    }

    public void SetInitializationFailed(bool initializationFailed)
    {
        InitializationFailed = initializationFailed;
    }
}

public sealed class Theme
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string IndexFilename { get; init; }

    public required string Directory { get; init; }

    public required DateTimeOffset UploadedAtUtc { get; init; }
}