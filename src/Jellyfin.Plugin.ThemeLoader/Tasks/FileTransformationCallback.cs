using System.Text.Json.Serialization;
using Jellyfin.Plugin.ThemeLoader.Repositories;
using Jellyfin.Plugin.ThemeLoader.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ThemeLoader.Tasks;

public static class FileTransformationCallback
{
    public static string? Transform(TransformationPayload payload)
    {
        using var scope = Plugin.Instance!.ScopeFactory.CreateScope();
        var stateRepository = scope.ServiceProvider.GetRequiredService<IStateRepository>();
        var state = stateRepository.Get();

        if (!state.Enabled || payload.Contents is null)
        {
            return payload.Contents;
        }

        //TODO: Put this somewhere else
        var path = "/ThemeLoader/Assets/" + state.SelectedTheme.IndexFilename;

        return ThemeHtmlInjector.InjectStylesheet(payload.Contents, path);
    }
}

public sealed class TransformationPayload
{
    [JsonPropertyName("contents")]
    public string? Contents { get; set; } = string.Empty;
}