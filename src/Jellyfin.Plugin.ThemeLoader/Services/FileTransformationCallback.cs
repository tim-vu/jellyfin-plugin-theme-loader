using System.IO;

namespace Jellyfin.Plugin.ThemeLoader.Services;

public static class FileTransformationCallback
{
    public static string Transform(TransformationPayload payload)
    {
        var plugin = Plugin.Instance;

        var selectedTheme = plugin?.Configuration.GetSelectedTheme();
        if (plugin is null || !plugin.Configuration.Enabled || selectedTheme is null)
        {
            return payload.Contents;
        }

        if (!File.Exists(selectedTheme.CssFilePath))
        {
            return payload.Contents;
        }

        string css = File.ReadAllText(selectedTheme.CssFilePath);
        return ThemeHtmlInjector.Inject(payload.Contents, css);
    }
}

public sealed class TransformationPayload
{
    public string Contents { get; set; } = string.Empty;
}
