using Jellyfin.Plugin.ThemeLoader.Repositories;
using Jellyfin.Plugin.ThemeLoader.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ThemeLoader;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddScoped<IStateRepository>(s => new StateRepository());
        serviceCollection.AddScoped<IThemeService, ThemeService>();
    }
}