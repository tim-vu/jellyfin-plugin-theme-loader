using System;
using System.Collections.Generic;
using Jellyfin.Plugin.ThemeLoader.Domain;
using Jellyfin.Plugin.ThemeLoader.Repositories;

namespace Jellyfin.Plugin.ThemeLoader.Tests;

public sealed class StateRepositoryFake : IStateRepository
{
    public ThemeLoaderState State { get; private set; } = CreateState();

    public ThemeLoaderState Get()
    {
        return State;
    }

    public void Save()
    {
    }

    public void Reset()
    {
        State = CreateState();
    }

    private static ThemeLoaderState CreateState()
    {
        return new ThemeLoaderState(
            enabled: false,
            selectedThemeId: null,
            installedThemes: [],
            initializationFailed: false);
    }
}