using System.IO;
using Jellyfin.Plugin.ThemeLoader.Repositories;

namespace Jellyfin.Plugin.ThemeLoader.Services;

public static class FileTransformationCallback
{
    public static string Transform(TransformationPayload payload)
    {
        var stateRepository = new StateRepository();

        var state = stateRepository.Get();

        if (!state.Enabled)
        {
            return payload.Contents;
        }

        var selectedTheme = state.SelectedTheme;
        var entrypointArchivePath = Path
            .GetRelativePath(selectedTheme.Directory, selectedTheme.EntrypointPath)
            .Replace('\\', '/');
        var css = CssUrlRewriter.RewriteEntryPointUrls(
            File.ReadAllText(selectedTheme.EntrypointPath),
            entrypointArchivePath,
            "ThemeLoader/Assets");

        return ThemeHtmlInjector.Inject(payload.Contents, css);
    }
}

public sealed class TransformationPayload
{
    public string Contents { get; set; } = string.Empty;
}
