using Jellyfin.Plugin.ThemeLoader.Models;

namespace Jellyfin.Plugin.ThemeLoader.Repositories;

public interface IStateRepository
{
    ThemeLoaderState Get();

    void Save();
}
