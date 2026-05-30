using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.ThemeLoader.Repositories;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ThemeLoader;

public class Plugin : BasePlugin<StateDto>, IHasWebPages
{
    public Plugin(IServiceScopeFactory scopeFactory, IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        ScopeFactory = scopeFactory;
    }

    public override string Name => "Theme Loader";

    public override Guid Id => Guid.Parse("7714068a-7f34-4a14-a059-595e98e2abe4");

    public IServiceScopeFactory ScopeFactory { get; }

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Web.configPage.html",
                    GetType().Namespace)
            }
        ];
    }
}