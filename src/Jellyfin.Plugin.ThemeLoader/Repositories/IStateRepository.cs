using Jellyfin.Plugin.ThemeLoader.Domain;

namespace Jellyfin.Plugin.ThemeLoader.Repositories;

public interface IStateRepository
{
    ThemeLoaderState Get();

    void Save();
}