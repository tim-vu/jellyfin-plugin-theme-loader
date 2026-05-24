using System;

namespace Jellyfin.Plugin.ThemeLoader.Services;

internal static class ThemeHtmlInjector
{
    private const string StyleElementId = "theme-loader-css";

    public static string Inject(string html, string css)
    {
        if (string.IsNullOrWhiteSpace(css))
        {
            return html;
        }

        var style = $"<style id=\"{StyleElementId}\">{EscapeStyleContent(css)}</style>";
        var cleaned = RemoveExistingStyle(html);

        var bodyIndex = cleaned.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);

        if (bodyIndex < 0)
        {
            return cleaned;
        }

        return cleaned.Insert(bodyIndex, style);
    }

    private static string RemoveExistingStyle(string html)
    {
        var idIndex = html.IndexOf($"id=\"{StyleElementId}\"", StringComparison.OrdinalIgnoreCase);

        if (idIndex < 0)
        {
            return html;
        }

        var start = html.LastIndexOf("<style", idIndex, StringComparison.OrdinalIgnoreCase);
        var end = html.IndexOf("</style>", idIndex, StringComparison.OrdinalIgnoreCase);

        if (start < 0 || end < 0)
        {
            return html;
        }

        end += "</style>".Length;

        return html.Remove(start, end - start);
    }

    private static string EscapeStyleContent(string css)
    {
        return css.Replace("</style", "<\\/style", StringComparison.OrdinalIgnoreCase);
    }
}
