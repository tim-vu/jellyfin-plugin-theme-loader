using System;

namespace Jellyfin.Plugin.ThemeLoader.Services;

internal static class ThemeHtmlInjector
{
    private const string StylesheetElementId = "theme-loader-css";

    public static string InjectStylesheet(string html, string href)
    {
        var link = $"""<link id="{StylesheetElementId}" rel="stylesheet" href="{href}" />""";
        var cleaned = RemoveExistingStylesheet(html);

        var bodyIndex = cleaned.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);

        return bodyIndex < 0 ? cleaned : cleaned.Insert(bodyIndex, link);
    }

    private static string RemoveExistingStylesheet(string html)
    {
        var idIndex = html.IndexOf($"id=\"{StylesheetElementId}\"", StringComparison.OrdinalIgnoreCase);

        if (idIndex < 0)
        {
            return html;
        }

        var start = html.LastIndexOf("<link", idIndex, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return html;
        }

        const string tagEnd = "/>";
        var end = html.IndexOf(tagEnd, idIndex);
        if (end < 0)
        {
            return html;
        }

        return html.Remove(start, end + tagEnd.Length - start);
    }
}
